using backend.Data;
using backend.DTOs.Payments;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace backend.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(IConfiguration configuration, IHttpClientFactory httpClientFactory, AppDbContext dbContext) : BaseApiController
{
    [HttpPost("create-checkout-session")]
    public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession(
        CreateCheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.SubscriptionPackages
            .FirstOrDefaultAsync(p => p.Key == request.Plan, cancellationToken);

        if (package is null)
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
        var amountJmd = billing == "yearly"
            ? GetEffectiveYearlyPrice(package)
            : GetEffectiveMonthlyPrice(package);
        var amount = amountJmd * 100;
        var saleActive = billing == "yearly"
            ? package.YearlySaleEnabled && package.YearlySalePriceJmd is > 0
            : package.MonthlySaleEnabled && package.MonthlySalePriceJmd is > 0;
        var freeTrialDays = Math.Max(0, package.FreeTrialDays);

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
            // Keep users on the hosted Stripe card form instead of Link-first flow.
            new("payment_method_types[0]", "card"),
            new("payment_method_collection", "always"),
            new("line_items[0][quantity]", "1"),
            new("metadata[plan]", request.Plan),
            new("metadata[billing]", billing),
            new("metadata[sale_active]", saleActive ? "true" : "false"),
            new("metadata[free_trial_days]", freeTrialDays.ToString()),
            new("metadata[price_jmd]", amountJmd.ToString()),
            new("metadata[monthly_price_jmd]", GetEffectiveMonthlyPrice(package).ToString()),
            new("metadata[yearly_price_jmd]", GetEffectiveYearlyPrice(package).ToString())
        };

        if (request.BusinessId is not null)
        {
            form.Add(new("metadata[business_id]", request.BusinessId.Value.ToString()));
        }

        form.AddRange([
            new("line_items[0][price_data][currency]", "jmd"),
            new("line_items[0][price_data][product_data][name]", saleActive
                ? $"HRBooks360 {package.Name} Sale"
                : $"HRBooks360 {package.Name}"),
            new("line_items[0][price_data][unit_amount]", amount.ToString()),
            new("line_items[0][price_data][recurring][interval]", interval)
        ]);

        if (freeTrialDays > 0)
        {
            form.Add(new("subscription_data[trial_period_days]", freeTrialDays.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
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

        business.Status = BusinessStatus.Active;
        business.PaymentStatus = PaymentStatus.Paid;
        business.SubscriptionStatus = SubscriptionStatus.Active;
        business.SelectedPlan = GetMetadataValue(metadata, "plan");
        business.BillingPeriod = GetMetadataValue(metadata, "billing");
        business.StripeCustomerId = GetString(session, "customer");
        business.StripeSubscriptionId = GetString(session, "subscription");
        business.PaymentCompletedAt = DateTime.UtcNow;
        business.LastPaymentMethod = "Stripe";
        ApplyPaymentWindow(business, business.BillingPeriod, DateTime.UtcNow);

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
        business.SubscriptionStatus = status.ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Active,
            "canceled" => SubscriptionStatus.Canceled,
            "unpaid" => SubscriptionStatus.Unpaid,
            "past_due" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.Incomplete // Default or unknown status
        };
        business.PaymentStatus = business.SubscriptionStatus is SubscriptionStatus.Active ? PaymentStatus.Paid : PaymentStatus.Unpaid;

        if (business.SubscriptionStatus is SubscriptionStatus.Active)
        {
            business.Status = BusinessStatus.Active;
            business.PaymentCompletedAt = DateTime.UtcNow;
            business.LastPaymentMethod = "Stripe";
            ApplyPaymentWindow(business, business.BillingPeriod, DateTime.UtcNow);
        }
        else if (business.SubscriptionStatus is SubscriptionStatus.Canceled or SubscriptionStatus.Unpaid or SubscriptionStatus.PastDue)
        {
            business.Status = BusinessStatus.Suspended;
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

        business.PaymentStatus = PaymentStatus.PaymentFailed;
        business.SubscriptionStatus = SubscriptionStatus.PastDue;
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

    private static long CalculateDiscountedPrice(long monthlyPrice, bool enabled, decimal percent)
    {
        if (!enabled || percent <= 0)
        {
            return monthlyPrice;
        }

        return Math.Max(0, (long)Math.Round(monthlyPrice * (1 - percent / 100m)));
    }

    private static long GetRegularYearlyPrice(SubscriptionPackage package) =>
        package.YearlyPriceJmd ?? (long)Math.Round(package.MonthlyPriceJmd * 12 * 0.8m);

    private static long GetEffectiveMonthlyPrice(SubscriptionPackage package)
    {
        if (package.MonthlySaleEnabled && package.MonthlySalePriceJmd is > 0)
        {
            return package.MonthlySalePriceJmd.Value;
        }

        return CalculateDiscountedPrice(package.MonthlyPriceJmd, package.DiscountEnabled, package.DiscountPercent);
    }

    private static long GetEffectiveYearlyPrice(SubscriptionPackage package)
    {
        if (package.YearlySaleEnabled && package.YearlySalePriceJmd is > 0)
        {
            return package.YearlySalePriceJmd.Value;
        }

        var regularYearlyPrice = GetRegularYearlyPrice(package);
        return package.DiscountEnabled && package.DiscountPercent > 0
            ? (long)Math.Round(regularYearlyPrice * (1 - package.DiscountPercent / 100m))
            : regularYearlyPrice;
    }

    private static void ApplyPaymentWindow(Models.Business business, string? billingPeriod, DateTime paidAtUtc)
    {
        var normalizedBilling = string.Equals(billingPeriod, "yearly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(billingPeriod, "Yearly", StringComparison.Ordinal)
                ? "Yearly"
                : "Monthly";

        business.BillingPeriod = normalizedBilling;
        business.SubscriptionStartedAt ??= paidAtUtc;
        business.NextPaymentDueAt = normalizedBilling == "Yearly"
            ? paidAtUtc.AddYears(1)
            : paidAtUtc.AddDays(30);
        business.GracePeriodEndsAt = business.NextPaymentDueAt.Value.AddDays(7);
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
}

public sealed record CreateCheckoutSessionResponse(string SessionId, string Url);
