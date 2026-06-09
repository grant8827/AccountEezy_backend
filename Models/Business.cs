using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class Business
{
    public int Id { get; set; }

    [MaxLength(160)]
    public required string CompanyName { get; set; }

    [MaxLength(20)]
    public required string TRN { get; set; }

    [MaxLength(80)]
    public required string Sector { get; set; }

    public DateTime TrialStartDate { get; set; } = DateTime.UtcNow;

    // Owner / personal info
    [MaxLength(80)]
    public string? FirstName { get; set; }

    [MaxLength(80)]
    public string? LastName { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    // Business details
    [MaxLength(40)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(20)]
    public string? NIS { get; set; }

    [MaxLength(40)]
    public string? BusinessType { get; set; }

    public DateTime? FiscalYearEnd { get; set; }

    // Address
    [MaxLength(200)]
    public string? Street { get; set; }

    [MaxLength(80)]
    public string? City { get; set; }

    [MaxLength(80)]
    public string? Parish { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(80)]
    public string Country { get; set; } = "Jamaica";

    // Contact
    [MaxLength(30)]
    public string? BusinessPhone { get; set; }

    [MaxLength(160)]
    public string? BusinessEmail { get; set; }

    [MaxLength(200)]
    public string? Website { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    // Platform status: "Pending", "Active", "Suspended"
    public BusinessStatus Status { get; set; } = BusinessStatus.Pending;

    // Subscription status: "Incomplete", "Active", "Canceled", "Unpaid", "PastDue"
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Incomplete;

    // Payment status: "Unpaid", "Paid", "PaymentFailed"
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    [MaxLength(80)]
    // Stores the Key of the selected SubscriptionPackage
    public string? SelectedPlan { get; set; }

    [MaxLength(20)]
    public string? BillingPeriod { get; set; }

    [MaxLength(120)]
    public string? StripeCustomerId { get; set; }

    [MaxLength(120)]
    public string? StripeSubscriptionId { get; set; }

    public DateTime? PaymentCompletedAt { get; set; }

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<TransactionEntry> Transactions { get; set; } = new List<TransactionEntry>();
    public ICollection<TaxRecord> TaxRecords { get; set; } = new List<TaxRecord>();
}
