using Microsoft.EntityFrameworkCore;
using BookKiosk.Domain.Entities;

namespace BookKiosk.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Area> Areas { get; set; } = null!;
    public DbSet<Book> Books { get; set; } = null!;
    
    // Users & Members
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<PointTransaction> PointTransactions { get; set; } = null!;
    
    // Inventory
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<ImportReceipt> ImportReceipts { get; set; } = null!;
    public DbSet<ImportReceiptDetail> ImportReceiptDetails { get; set; } = null!;
    
    // Orders & Payments
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderDetail> OrderDetails { get; set; } = null!;
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
    
    // Promotions
    public DbSet<Promotion> Promotions { get; set; } = null!;
    public DbSet<PromotionOrderDiscount> PromotionOrderDiscounts { get; set; } = null!;
    public DbSet<PromotionProductDiscount> PromotionProductDiscounts { get; set; } = null!;
    
    // Kiosks
    public DbSet<Kiosk> Kiosks { get; set; } = null!;
    public DbSet<KioskIncident> KioskIncidents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Tự động quét và áp dụng toàn bộ các cấu hình IEntityTypeConfiguration có trong project này
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
