using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class PromotionOrderDiscountConfiguration : IEntityTypeConfiguration<PromotionOrderDiscount>
{
    public void Configure(EntityTypeBuilder<PromotionOrderDiscount> builder)
    {
        builder.HasKey(x => x.PromotionId);

        builder.HasOne(x => x.Promotion)
            .WithOne(p => p.OrderDiscount)
            .HasForeignKey<PromotionOrderDiscount>(x => x.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}