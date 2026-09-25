using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class PromotionProductDiscountConfiguration : IEntityTypeConfiguration<PromotionProductDiscount>
{
    public void Configure(EntityTypeBuilder<PromotionProductDiscount> builder)
    {
        builder.HasKey(x => new { x.PromotionId, x.BookId });

        builder.HasOne(x => x.Promotion)
            .WithMany(p => p.ProductDiscounts)
            .HasForeignKey(x => x.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.BookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}