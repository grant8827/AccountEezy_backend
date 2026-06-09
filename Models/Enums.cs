namespace backend.Models;

public enum BusinessStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Deactivated = 3
}

public enum SubscriptionStatus
{
    Incomplete = 0,
    Active = 1,
    Canceled = 2,
    Unpaid = 3,
    PastDue = 4
}

public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    PaymentFailed = 2
}

public enum LeaveRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum PayCycle
{
    Weekly = 0,
    Fortnightly = 1,
    Monthly = 2
}

// Enums for TransactionEntry (moved from TransactionEntry.cs for centralization)
public enum TransactionType { Income = 1, Expense = 2 }
public enum TransactionFrequency { Daily = 1, Weekly = 2, Monthly = 3 }
public enum TransactionStatus { Pending = 1, Cleared = 2 }
