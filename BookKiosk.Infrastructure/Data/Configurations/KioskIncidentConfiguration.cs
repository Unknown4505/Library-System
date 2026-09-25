using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class KioskIncidentConfiguration : IEntityTypeConfiguration<KioskIncident>
{
    public void Configure(EntityTypeBuilder<KioskIncident> builder)
    {
        builder.HasKey(x => x.IncidentId);

        builder.HasOne(x => x.Kiosk)
            .WithMany(k => k.Incidents)
            .HasForeignKey(x => x.KioskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}