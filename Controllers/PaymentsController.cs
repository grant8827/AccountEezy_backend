using backend.Data;
using backend.DTOs.Payments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace backend.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    AppDbContext dbContext) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, PlanDefinition> Plans =
        new Dictionary<string, PlanDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["lite"] = new("Lite", 3500),
            ["starter"] = new("Starter", 6500),
            ["growth"] = new("Growth", 12500)
        };

    [HttpPost("create-checkout-session")]
    public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (!Plans.TryGetValue(request.Plan, out var plan))
        {
            return BadRequest(new { message = "Select a valid paid plan before starting checkout." });
        }

        var secretKey = configuration["Stripe:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Stripe is not configured. Set Stripe:SecretKey and try again."
            });
        }

        var billing = string.Equals(request.Billing, "yearly", StringComparison.OrdinalIgnoreCase)
            ? "yearly"
            : "monthly";
        var interval = billing == "yearly" ? "year" : "month";
        var amount = billing == "yearly"
            ? (long)Math.Round(plan.MonthlyPriceJmd * 12 * 0.8m * 100m)
            : plan.MonthlyPriceJmd * 100;
        var configuredPriceId = configuration[$"Stripe:Prices:{request.Plan}:{billing}"];

        var frontendUrl = configuration["FrontendUrl"]?.TrimEnd('/')
            ?? $"{Request.Scheme}://{Request.Host}";
        var successUrl = string.IsNullOrWhiteSpace(request.SuccessUrl)
            ? $"{frontendUrl}/login?registered=1&payment=success"
            : request.SuccessUrl;
        var cancelUrl = string.IsNullOrWhiteSpace(request.CancelUrl)
            ? $"{frontendUrl}/payment?plan={Uri.EscapeDataString(request.Plan)}&billing={billing}&payment=cancelled"
            : request.CancelUrl;

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("success_url", successUrl),
            new("cancel_url", cancelUrl),
            new("line_items[0][quantity]", "1"),
            new("metadata[plan]", request.Plan),
            new("metadata[billing]", billing)
        };

        if (request.BusinessId is not null)
        {
            form.Add(new("metadata[business_id]", request.BusinessId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(configuredPriceId))
        {
            form.Add(new("line_items[0][price]", configuredPriceId));
        }
        else
        {
            form.AddRange([
                new("line_items[0][price_data][currency]", "jmd"),
                new("line_items[0][price_data][product_data][name]", $"HRBooks360 {plan.Name}"),
                new("line_items[0][price_data][unit_amount]", amount.ToString()),
                new("line_items[0][price_data][recurring][interval]", interval)
            ]);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            form.Add(new("customer_email", request.CustomerEmail));
            form.Add(new("metadata[email]", request.CustomerEmail));
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessName))
        {
            form.Add(new("metadata[business_name]", request.BusinessName));
        }

        var client = httpClientFactory.CreateClient();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions")
        {
            Content = new FormUrlEncodedContent(form)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new
            {
                message = "Stripe checkout session could not be created.",
                detail = TryReadStripeError(body)
            });
        }

        using var json = JsonDocument.Parse(body);
        var sessionId = json.RootElement.GetProperty("id").GetString() ?? string.Empty;
        var url = json.RootElement.GetProperty("url").GetString() ?? string.Empty;

        return Ok(new CreateCheckoutSessionResponse(sessionId, url));
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        var webhookSecret = configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(webhookSecret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Stripe webhook is not configured.");
        }

        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
        if (!IsValidStripeSignature(payload, signatureHeader, webhookSecret))
        {
            return BadRequest("Invalid Stripe signature.");
        }

        using var json = JsonDocument.Parse(payload);
        var eventType = json.RootElement.GetProperty("type").GetString();

        if (eventType == "checkout.session.completed")
        {
            await HandleCheckoutCompleted(json.RootElement, cancellationToken);
        }
        else if (eventType is "customer.subscription.updated" or "customer.subscription.deleted")
        {
            await HandleSubscriptionChanged(json.RootElement, cancellationToken);
        }
        else if (eventType == "invoice.payment_failed")
        {
            await HandleInvoicePaymentFailed(json.RootElement, cancellationToken);
        }

        return Ok();
    }

    private async Task HandleCheckoutCompleted(JsonElement stripeEvent, CancellationToken cancellationToken)
    {
        var session = stripeEvent.GetProperty("data").GetProperty("object");
        var metadata = session.GetProperty("metadata");
        var business = await FindBusinessFromMetadata(metadata, cancellationToken);
        if (business is null)
        {
            return;
        }

        business.Status = "Active";
        business.PaymentStatus = "Paid";
        business.SubscriptionStatus = "Active";
        business.SelectedPlan = GetMetadataValue(metadata, "plan");
        business.BillingPeriod = GetMetadataValue(metadata, "billing");
        business.StripeCustomerId = GetString(session, "customer");
        business.StripeSubscriptionId = GetString(session, "subscription");
        business.PaymentCompletedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleSubscriptionChanged(JsonElement stripeEvent, CancellationToken cancellationToken)
    {
        var subscription = stripeEvent.GetProperty("data").GetProperty("object");
        var subscriptionId = GetString(subscription, "id");
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(b => b.StripeSubscriptionId == subscriptionId, cancellationToken);
        if (business is null)
        {
            return;
        }

        var status = GetString(subscription, "status") ?? "unknown";
        business.SubscriptionStatus = status;
        business.PaymentStatus = status is "active" or "trialing" ? "Paid" : "Unpaid";

        if (status is "active" or "trialing")
        {
            business.Status = "Active";
        }
        else if (status is "canceled" or "unpaid" or "past_due")
        {
            business.Status = "Suspended";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleInvoicePaymentFailed(JsonElement stripeEvent, CancellationToken cancellationToken)
    {
        var invoice = stripeEvent.GetProperty("data").GetProperty("object");
        var subscriptionId = GetString(invoice, "subscription");
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return;
        }

        var business = await dbContext.Businesses
            .FirstOrDefaultAsync(b => b.StripeSubscriptionId == subscriptionId, cancellationToken);
        if (business is null)
        {
            return;
        }

        business.PaymentStatus = "PaymentFailed";
        business.SubscriptionStatus = "PastDue";
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Models.Business?> FindBusinessFromMetadata(JsonElement metadata, CancellationToken cancellationToken)
    {
        var businessIdValue = GetMetadataValue(metadata, "business_id");
        if (int.TryParse(businessIdValue, out var businessId))
        {
            var business = await dbContext.Businesses.FindAsync([businessId], cancellationToken);
            if (business is not null)
            {
                return business;
            }
        }

        var email = GetMetadataValue(metadata, "email");
        return string.IsNullOrWhiteSpace(email)
            ? null
            : await dbContext.Businesses.FirstOrDefaultAsync(b => b.BusinessEmail == email, cancellationToken);
    }

    private static bool IsValidStripeSignature(string payload, string signatureHeader, string webhookSecret)
    {
        var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var timestamp = parts.FirstOrDefault(p => p.StartsWith("t=", StringComparison.Ordinal))?[2..];
        var signature = parts.FirstOrDefault(p => p.StartsWith("v1=", StringComparison.Ordinal))?[3..];

        if (string.IsNullOrWhiteSpace(timestamp) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var computedSignature = Convert.ToHexString(computedHash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signature));
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static string? GetMetadataValue(JsonElement metadata, string key)
    {
        return metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty(key, out var property)
            ? property.GetString()
            : null;
    }

    private static string TryReadStripeError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("error").GetProperty("message").GetString()
                ?? "Stripe returned an error.";
        }
        catch
        {
            return "Stripe returned an error.";
        }
    }

    private sealed record PlanDefinition(string Name, long MonthlyPriceJmd);
}

public sealed record CreateCheckoutSessionResponse(string SessionId, string Url);
