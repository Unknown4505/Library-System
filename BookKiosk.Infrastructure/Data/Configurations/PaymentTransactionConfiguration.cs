using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.HasKey(x => x.TransactionId);

        builder.HasIndex(x => x.ReferenceCode).IsUnique();
        builder.Property(x => x.ReferenceCode).HasColumnType("varchar(50)").IsRequired();

        builder.HasOne(x => x.Order)
            .WithMany(o => o.PaymentTransactions)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}