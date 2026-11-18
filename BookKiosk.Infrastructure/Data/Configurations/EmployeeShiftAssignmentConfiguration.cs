using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class EmployeeShiftAssignmentConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> builder)
    {
        builder.HasKey(a => a.AssignmentId);
        builder.Property(a => a.WorkedHours).HasColumnType("decimal(7,2)");
        builder.Property(a => a.Note).HasMaxLength(1000);
        builder.ToTable("EmployeeShiftAssignments", t => t.HasCheckConstraint("CK_EmployeeShiftAssignments_WorkedHours", "[WorkedHours] IS NULL OR ([WorkedHours] >= 0 AND [WorkedHours] <= 744)"));
        builder.HasIndex(a => new { a.WorkShiftId, a.EmployeeId }).IsUnique();
        builder.HasIndex(a => new { a.WorkShiftId, a.AssignedKioskId }).IsUnique().HasFilter("[AssignedKioskId] IS NOT NULL AND [IsActive] = 1");
        builder.HasIndex(a => a.EmployeeId);
        builder.HasOne(a => a.WorkShift).WithMany(s => s.Assignments).HasForeignKey(a => a.WorkShiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Employee).WithMany(e => e.ShiftAssignments).HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AssignedKiosk).WithMany().HasForeignKey(a => a.AssignedKioskId).OnDelete(DeleteBehavior.Restrict);
    }
}
