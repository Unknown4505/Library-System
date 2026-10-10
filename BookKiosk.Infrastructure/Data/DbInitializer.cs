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

        // Mỗi nhóm seed được kiểm tra theo natural key riêng để có thể chạy lặp lại
        // và bổ sung đúng bản ghi còn thiếu mà không ghi đè dữ liệu đang có.
        EnsureCategory(context, "Sách Thiếu Nhi", "Sách dành cho độ tuổi thiếu nhi");
        EnsureCategory(context, "Công Nghệ Thông Tin", "Lập trình, mạng máy tính, phần mềm");
        EnsureCategory(context, "Văn Học", "Tiểu thuyết, truyện ngắn, tản văn");

        EnsureArea(context, "Kệ A1 - Tầng 1");
        EnsureArea(context, "Kệ B2 - Tầng 2");
        context.SaveChanges();

        var catThieuNhi = context.Categories.First(c => c.Name == "Sách Thiếu Nhi");
        var catCntt = context.Categories.First(c => c.Name == "Công Nghệ Thông Tin");
        var catVanHoc = context.Categories.First(c => c.Name == "Văn Học");
        var areaA1 = context.Areas.First(a => a.Name == "Kệ A1 - Tầng 1");
        var areaB2 = context.Areas.First(a => a.Name == "Kệ B2 - Tầng 2");

        EnsureBook(context, new Book
        {
            Title = "Dế Mèn Phiêu Lưu Ký",
            Author = "Tô Hoài",
            Barcode = "8935244878235",
            CostPrice = 30000,
            SellingPrice = 50000,
            StockQuantity = 20,
            ReservedQuantity = 0,
            CategoryId = catThieuNhi.CategoryId,
            AreaId = areaA1.AreaId,
            IsActive = true
        });
        EnsureBook(context, new Book
        {
            Title = "Clean Code",
            Author = "Robert C. Martin",
            Barcode = "9780132350884",
            CostPrice = 200000,
            SellingPrice = 350000,
            StockQuantity = 5,
            ReservedQuantity = 1,
            CategoryId = catCntt.CategoryId,
            AreaId = areaB2.AreaId,
            IsActive = true
        });
        EnsureBook(context, new Book
        {
            Title = "Số Đỏ",
            Author = "Vũ Trọng Phụng",
            Barcode = "8936049520011",
            CostPrice = 45000,
            SellingPrice = 80000,
            StockQuantity = 0,
            ReservedQuantity = 0,
            CategoryId = catVanHoc.CategoryId,
            AreaId = areaA1.AreaId,
            IsActive = true
        });
        context.SaveChanges();

        // 4. Seed Users (Admin)
        if (!context.Users.Any(u => u.Username == "admin"))
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
        const string demoKioskMacAddress = "00-14-22-01-23-45";
        if (!context.Kiosks.Any(k => k.MacAddress == demoKioskMacAddress))
        {
            context.Kiosks.Add(new Kiosk
            {
                KioskName = "Kiosk Tầng 1 - Sảnh chính",
                MacAddress = demoKioskMacAddress,
                Status = BookKiosk.Domain.Enums.KioskStatus.Online,
                LastPingAt = DateTime.UtcNow,
                AreaId = areaA1.AreaId
            });
            context.SaveChanges();
        }

        // 6. Seed Promotions
        const string demoPromotionName = "Khai trương Kiosk";
        var promo = context.Promotions.FirstOrDefault(p => p.Name == demoPromotionName);
        if (promo == null)
        {
            promo = new Promotion
            {
                Name = demoPromotionName,
                Description = "Giảm 20K cho hóa đơn từ 100K",
                PromotionType = BookKiosk.Domain.Enums.PromotionType.OrderDiscount,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow.AddMonths(1),
                IsActive = true
            };
            context.Promotions.Add(promo);
            context.SaveChanges();
        }

        if (!context.PromotionOrderDiscounts.Any(d => d.PromotionId == promo.PromotionId))
        {
            context.PromotionOrderDiscounts.Add(new PromotionOrderDiscount
            {
                PromotionId = promo.PromotionId,
                MinOrderValue = 100000,
                DiscountAmount = 20000
            });
            context.SaveChanges();
        }
    }

    private static void EnsureCategory(ApplicationDbContext context, string name, string description)
    {
        if (!context.Categories.Any(c => c.Name == name))
        {
            context.Categories.Add(new Category { Name = name, Description = description });
        }
    }

    private static void EnsureArea(ApplicationDbContext context, string name)
    {
        if (!context.Areas.Any(a => a.Name == name))
        {
            context.Areas.Add(new Area { Name = name });
        }
    }

    private static void EnsureBook(ApplicationDbContext context, Book book)
    {
        if (!context.Books.Any(b => b.Barcode == book.Barcode))
        {
            context.Books.Add(book);
        }
    }
}
