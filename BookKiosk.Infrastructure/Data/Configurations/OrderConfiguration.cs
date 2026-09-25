using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(x => x.OrderCode).IsUnique();
        builder.Property(x => x.OrderCode).HasColumnType("varchar(50)").IsRequired();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Member)
            .WithMany(m => m.Orders)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Promotion)
            .WithMany()
            .HasForeignKey(x => x.PromotionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}