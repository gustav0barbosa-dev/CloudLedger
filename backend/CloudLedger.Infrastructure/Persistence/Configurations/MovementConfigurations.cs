using CloudLedger.Domain.Accounts;
using CloudLedger.Domain.Categories;
using CloudLedger.Domain.Transactions;
using CloudLedger.Domain.Transfers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CloudLedger.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", table => table.HasCheckConstraint("ck_transactions_amount_positive", "amount > 0"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.OccurredAt });
        builder.HasIndex(x => new { x.UserId, x.Type, x.OccurredAt });
        builder.HasIndex(x => new { x.UserId, x.AccountId, x.OccurredAt });
        builder.HasIndex(x => new { x.UserId, x.CategoryId, x.OccurredAt });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers", table =>
        {
            table.HasCheckConstraint("ck_transfers_amount_positive", "amount > 0");
            table.HasCheckConstraint("ck_transfers_distinct_accounts", "source_account_id <> destination_account_id");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.OccurredAt).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.OccurredAt });
        builder.HasIndex(x => new { x.UserId, x.SourceAccountId });
        builder.HasIndex(x => new { x.UserId, x.DestinationAccountId });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.SourceAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.DestinationAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
