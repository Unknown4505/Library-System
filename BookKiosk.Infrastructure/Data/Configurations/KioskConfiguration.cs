using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class KioskConfiguration : IEntityTypeConfiguration<Kiosk>
{
    public void Configure(EntityTypeBuilder<Kiosk> builder)
    {
        builder.HasIndex(x => x.MacAddress).IsUnique();
        builder.Property(x => x.MacAddress).HasColumnType("varchar(50)").IsRequired();

        builder.HasOne(x => x.Area)
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}