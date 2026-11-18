using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class WorkShiftConfiguration : IEntityTypeConfiguration<WorkShift>
{
    public void Configure(EntityTypeBuilder<WorkShift> builder)
    {
        builder.HasKey(s => s.WorkShiftId);
        builder.Property(s => s.ShiftName).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.WorkDate, s.Status });
        builder.ToTable("WorkShifts", t => t.HasCheckConstraint("CK_WorkShifts_Status", "[Status] IN (1, 2, 3)"));
        builder.HasOne(s => s.FinalizedByUser).WithMany().HasForeignKey(s => s.FinalizedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
