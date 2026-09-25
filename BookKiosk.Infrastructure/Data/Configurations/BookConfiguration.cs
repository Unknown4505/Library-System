using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BookKiosk.Domain.Entities;

namespace BookKiosk.Infrastructure.Data.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("Books");
        builder.HasKey(b => b.BookId);

        // Barcode là varchar(50) và Unique
        builder.Property(b => b.Barcode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.HasIndex(b => b.Barcode).IsUnique();

        builder.Property(b => b.Title).HasMaxLength(255).IsRequired();
        builder.Property(b => b.Author).HasMaxLength(255).IsRequired();
        builder.Property(b => b.Publisher).HasMaxLength(255);
        builder.Property(b => b.ImageUrl).HasMaxLength(500);

        // Giá tiền luôn là decimal(18,0) (VNĐ)
        builder.Property(b => b.CostPrice).HasColumnType("decimal(18,0)");
        builder.Property(b => b.SellingPrice).HasColumnType("decimal(18,0)");

        // Ignore cột AvailableStock để EF không map vào Database
        builder.Ignore(b => b.AvailableStock);

        // Quan hệ 1-N: 1 Category có nhiều Books
        builder.HasOne(b => b.Category)
            .WithMany(c => c.Books)
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict); // Không cho xóa Category nếu vẫn còn Sách

        // Quan hệ 1-N: 1 Area có nhiều Books
        builder.HasOne(b => b.Area)
            .WithMany(a => a.Books)
            .HasForeignKey(b => b.AreaId)
            .OnDelete(DeleteBehavior.SetNull); // Cho phép xóa Area, Sách sẽ bị mất vị trí (AreaId = null)
    }
}
