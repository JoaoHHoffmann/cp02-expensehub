using System;
using ExpenseHub.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseHub.Api.Data;

internal sealed class ExpenseHubDbContext : IdentityDbContext<IdentityUser>
{
    public ExpenseHubDbContext(DbContextOptions<ExpenseHubDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<ExpenseHistory> ExpenseHistories => Set<ExpenseHistory>();

    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(c => c.Name).IsUnique();
            entity.HasData(
                new ExpenseCategory { Id = 1, Name = "Alimentação" },
                new ExpenseCategory { Id = 2, Name = "Transporte" },
                new ExpenseCategory { Id = 3, Name = "Hospedagem" },
                new ExpenseCategory { Id = 4, Name = "Outros" });
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.Property(e => e.OwnerId).HasMaxLength(450).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.History).WithOne().HasForeignKey(h => h.ExpenseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Payment).WithOne().HasForeignKey<PaymentRecord>(p => p.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExpenseHistory>(entity =>
        {
            entity.Property(h => h.Action).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.ActorId).HasMaxLength(450).IsRequired();
            entity.Property(h => h.PreviousStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.NewStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(h => h.Justification).HasMaxLength(500);
            entity.Property(h => h.Changes).HasMaxLength(2000);
        });

        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.Property(p => p.PaidById).HasMaxLength(450).IsRequired();
            entity.Property(p => p.Amount).HasPrecision(18, 2);
        });
    }
}
