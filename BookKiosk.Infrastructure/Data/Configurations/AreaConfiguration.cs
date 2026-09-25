using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookKiosk.Domain.Entities;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas");
        builder.HasKey(a => a.AreaId);

        builder.Property(a => a.Name).HasMaxLength(255).IsRequired();
        
        // IsUnicode(false) ép kiểu dữ liệu thành varchar thay vì nvarchar
        builder.Property(a => a.MapCoordinates).HasMaxLength(500).IsUnicode(false);
    }
}
