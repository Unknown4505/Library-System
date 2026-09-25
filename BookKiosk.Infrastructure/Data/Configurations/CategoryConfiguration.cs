using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookKiosk.Domain.Entities;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.CategoryId);

        builder.Property(c => c.Name).HasMaxLength(255).IsRequired();
        
        // Description có thể null, default là nvarchar(max)
    }
}
