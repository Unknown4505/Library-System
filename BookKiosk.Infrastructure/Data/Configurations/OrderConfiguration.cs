using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.Property(o => o.OrderCode)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(o => o.OrderCode).IsUnique();

        builder.Property(o => o.PromotionNameAtTime)
            .HasColumnType("nvarchar(max)");

        builder.Property(o => o.PointsUsedAmountAtTime)
            .HasPrecision(18, 2);
        builder.Property(o => o.EmployeeCodeAtTime).HasMaxLength(50).IsUnicode(false);
        builder.Property(o => o.EmployeeNameAtTime).HasMaxLength(255);
        builder.HasIndex(o => o.EmployeeShiftAssignmentId);
        builder.HasIndex(o => o.KioskId);
        builder.HasOne(o => o.EmployeeShiftAssignment).WithMany(a => a.Orders).HasForeignKey(o => o.EmployeeShiftAssignmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Kiosk).WithMany().HasForeignKey(o => o.KioskId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.CreatedByUser)
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Member)
            .WithMany(m => m.Orders)
            .HasForeignKey(o => o.MemberId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Promotion)
            .WithMany()
            .HasForeignKey(o => o.PromotionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
