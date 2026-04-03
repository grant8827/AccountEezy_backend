namespace backend.DTOs.Auth;

public class RegisterRequest
{
    // Personal Information
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string Phone { get; set; }
    
    // Business Information
    public required string BusinessName { get; set; }
    public string? RegistrationNumber { get; set; }
    public required string TRN { get; set; }
    public required string NIS { get; set; }
    public required string BusinessType { get; set; }
    public required string Industry { get; set; }
    public DateTime? FiscalYearEnd { get; set; }
    
    // Address Information
    public required string Street { get; set; }
    public required string City { get; set; }
    public required string Parish { get; set; }
    public string? PostalCode { get; set; }
    public string Country { get; set; } = "Jamaica";
    
    // Contact Information
    public required string BusinessPhone { get; set; }
    public required string BusinessEmail { get; set; }
    public string? Website { get; set; }
}
