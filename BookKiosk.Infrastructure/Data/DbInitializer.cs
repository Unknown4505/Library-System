using BookKiosk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BookKiosk.Infrastructure.Data;

public static class DbInitializer
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using var context = new ApplicationDbContext(
            serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

        // Chạy migration tự động hoặc tạo database nếu chưa có (Chỉ chạy nếu là DB thật, không chạy nếu là InMemory test)
        if (context.Database.IsRelational())
        {
            context.Database.Migrate();
        }
        else
        {
            context.Database.EnsureCreated();
        }

        // Kiểm tra xem đã có dữ liệu mẫu chưa (Nếu có Category thì return)
        if (context.Categories.Any())
        {
            return;
        }

        // 1. Seed Categories
        var categories = new Category[]
        {
            new Category { Name = "Sách Thiếu Nhi", Description = "Sách dành cho độ tuổi thiếu nhi" },
            new Category { Name = "Công Nghệ Thông Tin", Description = "Lập trình, mạng máy tính, phần mềm" },
            new Category { Name = "Văn Học", Description = "Tiểu thuyết, truyện ngắn, tản văn" }
        };
        context.Categories.AddRange(categories);
        context.SaveChanges();

        // 2. Seed Areas
        var areas = new Area[]
        {
            new Area { Name = "Kệ A1 - Tầng 1" },
            new Area { Name = "Kệ B2 - Tầng 2" }
        };
        context.Areas.AddRange(areas);
        context.SaveChanges();

        // 3. Seed Books
        var books = new Book[]
        {
            new Book
            {
                Title = "Dế Mèn Phiêu Lưu Ký",
                Author = "Tô Hoài",
                Barcode = "8935244878235",
                CostPrice = 30000,
                SellingPrice = 50000,
                StockQuantity = 20,
                ReservedQuantity = 0,
                CategoryId = categories[0].CategoryId,
                AreaId = areas[0].AreaId,
                IsActive = true
            },
            new Book
            {
                Title = "Clean Code",
                Author = "Robert C. Martin",
                Barcode = "9780132350884",
                CostPrice = 200000,
                SellingPrice = 350000,
                StockQuantity = 5,
                ReservedQuantity = 1,
                CategoryId = categories[1].CategoryId,
                AreaId = areas[1].AreaId,
                IsActive = true
            },
            new Book
            {
                Title = "Số Đỏ",
                Author = "Vũ Trọng Phụng",
                Barcode = "8936049520011",
                CostPrice = 45000,
                SellingPrice = 80000,
                StockQuantity = 0, // Test trường hợp HẾT HÀNG
                ReservedQuantity = 0,
                CategoryId = categories[2].CategoryId,
                AreaId = areas[0].AreaId,
                IsActive = true
            }
        };
        context.Books.AddRange(books);
        context.SaveChanges();
        // 4. Seed Users (Admin)
        if (!context.Users.Any())
        {
            context.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = "$2a$11$0.mYpM.2n9FzR/VfU.5y5eF2FwFk3ZfR5eT5vC5hQ/kS9wP9nU8Uq", // Hash for 'admin123'
                FullName = "Administrator",
                Role = BookKiosk.Domain.Enums.UserRole.Admin,
                IsActive = true
            });
            context.SaveChanges();
        }

        // 5. Seed Kiosks
        if (!context.Kiosks.Any())
        {
            context.Kiosks.Add(new Kiosk
            {
                KioskName = "Kiosk Tầng 1 - Sảnh chính",
                MacAddress = "00-14-22-01-23-45",
                Status = BookKiosk.Domain.Enums.KioskStatus.Online,
                LastPingAt = DateTime.UtcNow,
                AreaId = areas[0].AreaId
            });
            context.SaveChanges();
        }

        // 6. Seed Promotions
        if (!context.Promotions.Any())
        {
            var promo = new Promotion
            {
                Name = "Khai trương Kiosk",
                Description = "Giảm 20K cho hóa đơn từ 100K",
                PromotionType = BookKiosk.Domain.Enums.PromotionType.OrderDiscount,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddMonths(1),
                IsActive = true
            };
            context.Promotions.Add(promo);
            context.SaveChanges();

            context.PromotionOrderDiscounts.Add(new PromotionOrderDiscount
            {
                PromotionId = promo.PromotionId,
                MinOrderValue = 100000,
                DiscountAmount = 20000
            });
            context.SaveChanges();
        }
    }
}
