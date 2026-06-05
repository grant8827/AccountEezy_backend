namespace backend.DTOs.Payments;

public class CreateCheckoutSessionRequest
{
    public string Plan { get; set; } = string.Empty;
    public string Billing { get; set; } = "monthly";
    public string? CustomerEmail { get; set; }
    public string? BusinessName { get; set; }
    public int? BusinessId { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CancelUrl { get; set; }
}
