namespace backend.DTOs.Auth;

public class ProfileResponse
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TRN { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? NIS { get; set; }
    public string? BusinessType { get; set; }
    public DateTime? FiscalYearEnd { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Parish { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Jamaica";
    public string? BusinessPhone { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Website { get; set; }
    public string? LogoUrl { get; set; }
}

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string? Sector { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? NIS { get; set; }
    public string? BusinessType { get; set; }
    public DateTime? FiscalYearEnd { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Parish { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessEmail { get; set; }
    public string? Website { get; set; }
}

public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }
    public required string NewPassword { get; set; }
}
