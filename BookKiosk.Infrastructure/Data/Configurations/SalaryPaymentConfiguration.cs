using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class SalaryPaymentConfiguration : IEntityTypeConfiguration<SalaryPayment>
{
    public void Configure(EntityTypeBuilder<SalaryPayment> builder)
    {
        builder.ToTable("SalaryPayments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_SalaryPayments_PayMonth",
                "[PayMonth] >= 1 AND [PayMonth] <= 12");
            tableBuilder.HasCheckConstraint(
                "CK_SalaryPayments_Amounts",
                "[BaseSalarySnapshot] >= 0 AND [AllowanceAmount] >= 0 AND [DeductionAmount] >= 0 AND [NetSalary] >= 0");
            tableBuilder.HasCheckConstraint(
                "CK_SalaryPayments_Status",
                "[Status] IN (1, 2)");
        });

        builder.HasKey(s => s.SalaryPaymentId);
        builder.HasIndex(s => new { s.EmployeeId, s.PayYear, s.PayMonth }).IsUnique();

        builder.Property(s => s.BaseSalarySnapshot).HasColumnType("decimal(18,0)");
        builder.Property(s => s.AllowanceAmount).HasColumnType("decimal(18,0)");
        builder.Property(s => s.DeductionAmount).HasColumnType("decimal(18,0)");
        builder.Property(s => s.NetSalary).HasColumnType("decimal(18,0)");
        builder.Property(s => s.Note).HasMaxLength(1000);

        builder.HasOne(s => s.Employee)
            .WithMany(e => e.SalaryPayments)
            .HasForeignKey(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.PaidByUser)
            .WithMany()
            .HasForeignKey(s => s.PaidByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
