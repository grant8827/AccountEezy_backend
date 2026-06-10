namespace backend.DTOs.Auth;

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public AuthData? Data { get; set; }
}

public class AuthData
{
    public UserData User { get; set; } = null!;
    public string Token { get; set; } = string.Empty;
}

public class UserData
{
    public string Email { get; set; } = string.Empty;
    public int? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime TrialStartDate { get; set; }
    public DateTime TrialExpiresAt { get; set; }
    public bool IsTrialExpired { get; set; }
    public bool IsEmployee { get; set; }
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public bool RequiresPayment { get; set; }
    public bool IsSuspended { get; set; }
    public string? SelectedPlan { get; set; }
    public string? BillingPeriod { get; set; }
    public string? PaymentStatus { get; set; }
    public string? SubscriptionStatus { get; set; }
    public DateTime? NextPaymentDueAt { get; set; }
    public DateTime? GracePeriodEndsAt { get; set; }
}
