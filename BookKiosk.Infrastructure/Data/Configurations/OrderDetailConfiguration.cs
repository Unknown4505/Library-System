using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.HasKey(x => new { x.OrderId, x.BookId });

        builder.Property(x => x.BookTitleAtTime)
            .HasMaxLength(255);

        builder.Property(x => x.DiscountPerUnitAtTime)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.HasOne(x => x.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
