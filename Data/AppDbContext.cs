using backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<AppUser>(options)
{
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<TransactionEntry> Transactions => Set<TransactionEntry>();
    public DbSet<TaxRecord> TaxRecords => Set<TaxRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<TaxConfiguration> TaxConfigurations => Set<TaxConfiguration>();
    public DbSet<PayrollBatch> PayrollBatches => Set<PayrollBatch>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<Notice> Notices => Set<Notice>();
    public DbSet<SubscriptionPackage> SubscriptionPackages => Set<SubscriptionPackage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Remove unused Identity tables
        builder.Ignore<IdentityUserClaim<string>>();
        builder.Ignore<IdentityUserLogin<string>>();
        builder.Ignore<IdentityUserToken<string>>();

        builder.Entity<Business>()
            .HasIndex(b => b.TRN)
            .IsUnique();

        builder.Entity<AppUser>()
            .HasOne(u => u.Business)
            .WithMany()
            .HasForeignKey(u => u.BusinessId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SubscriptionPackage>()
            .HasIndex(p => p.Key)
            .IsUnique();

        builder.Entity<Employee>()
            .HasOne(e => e.Business)
            .WithMany(b => b.Employees)
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TransactionEntry>()
            .HasOne(t => t.Business)
            .WithMany(b => b.Transactions)
            .HasForeignKey(t => t.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TaxRecord>()
            .HasOne(t => t.Business)
            .WithMany(b => b.TaxRecords)
            .HasForeignKey(t => t.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LeaveRequest>()
            .HasOne(lr => lr.Employee)
            .WithMany(e => e.LeaveRequests)
            .HasForeignKey(lr => lr.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Employee>()
            .HasIndex(e => e.Email)
            .IsUnique();

        // TaxConfiguration: one per business
        builder.Entity<TaxConfiguration>()
            .HasOne(t => t.Business)
            .WithMany()
            .HasForeignKey(t => t.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TaxConfiguration>()
            .HasIndex(t => t.BusinessId)
            .IsUnique();

        // PayrollBatch
        builder.Entity<PayrollBatch>()
            .HasOne(b => b.Business)
            .WithMany()
            .HasForeignKey(b => b.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        // PayrollEntry
        builder.Entity<PayrollEntry>()
            .HasOne(e => e.Batch)
            .WithMany(b => b.Entries)
            .HasForeignKey(e => e.PayrollBatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PayrollEntry>()
            .HasOne(e => e.Employee)
            .WithMany()
            .HasForeignKey(e => e.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notice
        builder.Entity<Notice>()
            .HasOne(n => n.Business)
            .WithMany()
            .HasForeignKey(n => n.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
