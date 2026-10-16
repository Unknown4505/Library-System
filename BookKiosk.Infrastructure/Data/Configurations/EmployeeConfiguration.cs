using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Employees_CurrentBaseSalary",
                "[CurrentBaseSalary] IS NULL OR [CurrentBaseSalary] >= 0");
        });

        builder.HasKey(e => e.EmployeeId);

        builder.Property(e => e.EmployeeCode)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();
        builder.HasIndex(e => e.EmployeeCode).IsUnique();

        builder.Property(e => e.FullName).HasMaxLength(255).IsRequired();
        builder.Property(e => e.PhoneNumber).HasMaxLength(20).IsUnicode(false);
        builder.Property(e => e.Email).HasMaxLength(255).IsUnicode(false);
        builder.Property(e => e.Position).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CurrentBaseSalary).HasColumnType("decimal(18,0)");

        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        builder.HasOne(e => e.User)
            .WithOne()
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
