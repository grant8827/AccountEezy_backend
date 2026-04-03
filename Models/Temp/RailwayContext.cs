using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace backend.Models.Temp;

public partial class RailwayContext : DbContext
{
    public RailwayContext(DbContextOptions<RailwayContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<Business> Businesses { get; set; }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<LeaveRequest> LeaveRequests { get; set; }

    public virtual DbSet<TaxRecord> TaxRecords { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.BusinessId, "IX_AspNetUsers_BusinessId");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex").IsUnique();

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasOne(d => d.Business).WithMany(p => p.AspNetUsers)
                .HasForeignKey(d => d.BusinessId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Business>(entity =>
        {
            entity.HasIndex(e => e.Trn, "IX_Businesses_TRN").IsUnique();

            entity.Property(e => e.CompanyName).HasMaxLength(160);
            entity.Property(e => e.Sector).HasMaxLength(80);
            entity.Property(e => e.Trn)
                .HasMaxLength(20)
                .HasColumnName("TRN");
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(e => e.BusinessId, "IX_Employees_BusinessId");

            entity.HasIndex(e => e.Email, "IX_Employees_Email").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.BankAccountNumber).HasMaxLength(100);
            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.EmployeeIdNumber).HasMaxLength(50);
            entity.Property(e => e.Name).HasMaxLength(140);
            entity.Property(e => e.Nisnumber)
                .HasMaxLength(20)
                .HasColumnName("NISNumber");
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PayCycle).HasMaxLength(20);
            entity.Property(e => e.Trn)
                .HasMaxLength(20)
                .HasColumnName("TRN");

            entity.HasOne(d => d.Business).WithMany(p => p.Employees).HasForeignKey(d => d.BusinessId);
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasIndex(e => e.EmployeeId, "IX_LeaveRequests_EmployeeId");

            entity.Property(e => e.AdminNotes).HasMaxLength(250);
            entity.Property(e => e.DocumentPath).HasMaxLength(500);
            entity.Property(e => e.LeaveType).HasMaxLength(50);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20);

            entity.HasOne(d => d.Employee).WithMany(p => p.LeaveRequests).HasForeignKey(d => d.EmployeeId);
        });

        modelBuilder.Entity<TaxRecord>(entity =>
        {
            entity.HasIndex(e => e.BusinessId, "IX_TaxRecords_BusinessId");

            entity.HasOne(d => d.Business).WithMany(p => p.TaxRecords).HasForeignKey(d => d.BusinessId);
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasIndex(e => e.BusinessId, "IX_Transactions_BusinessId");

            entity.Property(e => e.Category).HasMaxLength(80);

            entity.HasOne(d => d.Business).WithMany(p => p.Transactions).HasForeignKey(d => d.BusinessId);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
