using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;

    public UnitOfWork(ApplicationDbContext context) => _context = context;

    public async Task BeginTransactionAsync() => _transaction = await _context.Database.BeginTransactionAsync();
    public async Task CommitAsync() { if (_transaction != null) await _transaction.CommitAsync(); }
    public async Task RollbackAsync() { if (_transaction != null) await _transaction.RollbackAsync(); }
    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
    public void Dispose() => _transaction?.Dispose();
}

public class MemberRepository : IMemberRepository
{
    private readonly ApplicationDbContext _context;
    public MemberRepository(ApplicationDbContext context) => _context = context;
    public async Task<Member?> GetByPhoneAsync(string phone) => await _context.Members.FirstOrDefaultAsync(m => m.PhoneNumber == phone);
    public async Task<Member?> GetByIdAsync(int id) => await _context.Members.FindAsync(id);
    public async Task<bool> CheckPhoneExistsAsync(string phone) => await _context.Members.AnyAsync(m => m.PhoneNumber == phone);
    public async Task AddAsync(Member member) => await _context.Members.AddAsync(member);
    public async Task<IEnumerable<PointTransaction>> GetPointHistoryAsync(int memberId) => await _context.PointTransactions.Where(p => p.MemberId == memberId).OrderByDescending(p => p.CreatedAt).ToListAsync();
    public async Task AddPointTransactionAsync(PointTransaction transaction) => await _context.PointTransactions.AddAsync(transaction);
    public async Task<bool> TryDeductPointsAsync(int memberId, int points)
    {
        var affected = await _context.Members
            .Where(m => m.MemberId == memberId && m.Points >= points)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Points, m => m.Points - points));
        return affected > 0;
    }
    public async Task AddPointsAsync(int memberId, int points)
    {
        await _context.Members
            .Where(m => m.MemberId == memberId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Points, m => m.Points + points));
    }
}

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;
    public UserRepository(ApplicationDbContext context) => _context = context;
    public async Task<User?> GetByUsernameAsync(string username) => await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
    public async Task<User?> GetByIdAsync(int id) => await _context.Users.FindAsync(id);
}

public class SupplierRepository : ISupplierRepository
{
    private readonly ApplicationDbContext _context;
    public SupplierRepository(ApplicationDbContext context) => _context = context;
    public async Task<IEnumerable<Supplier>> GetAllAsync() => await _context.Suppliers.OrderByDescending(s => s.SupplierId).ToListAsync();
    public async Task<Supplier?> GetByIdAsync(int id) => await _context.Suppliers.FindAsync(id);
    public async Task<bool> HasLinkedReceiptsAsync(int id) => await _context.ImportReceipts.AnyAsync(r => r.SupplierId == id);
    public async Task AddAsync(Supplier supplier) => await _context.Suppliers.AddAsync(supplier);
    public Task UpdateAsync(Supplier supplier)
    {
        _context.Suppliers.Update(supplier);
        return Task.CompletedTask;
    }
    public Task DeleteAsync(Supplier supplier)
    {
        _context.Suppliers.Remove(supplier);
        return Task.CompletedTask;
    }
}

public class PromotionRepository : IPromotionRepository
{
    private readonly ApplicationDbContext _context;
    public PromotionRepository(ApplicationDbContext context) => _context = context;
    public async Task<IEnumerable<Promotion>> GetAllActiveAsync() => await _context.Promotions.Include(p => p.OrderDiscount).Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now).ToListAsync();
    public async Task<IEnumerable<Promotion>> GetAllAsync() => await _context.Promotions.Include(p => p.OrderDiscount).OrderByDescending(p => p.PromotionId).ToListAsync();
    public async Task<Promotion?> GetByIdAsync(int id) => await _context.Promotions.FindAsync(id);
    public async Task AddAsync(Promotion promotion) => await _context.Promotions.AddAsync(promotion);
    public async Task<bool> ToggleActiveAsync(int id, bool isActive)
    {
        var affected = await _context.Promotions
            .Where(p => p.PromotionId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, isActive));
        return affected > 0;
    }
}

public class ReportRepository : IReportRepository
{
    private readonly ApplicationDbContext _context;
    public ReportRepository(ApplicationDbContext context) => _context = context;
    public async Task<object> GetRevenueByMonthAsync(int year)
    {
        var rawData = await _context.Orders.Where(o => o.OrderStatus == OrderStatus.Paid && o.CompletedAt.HasValue && o.CompletedAt.Value.Year == year).GroupBy(o => o.CompletedAt!.Value.Month).Select(g => new { Month = g.Key, Revenue = g.Sum(o => o.TotalAmount), TotalOrders = g.Count() }).ToListAsync();
        return Enumerable.Range(1, 12).Select(m => new { Month = m, Revenue = rawData.FirstOrDefault(d => d.Month == m)?.Revenue ?? 0, TotalOrders = rawData.FirstOrDefault(d => d.Month == m)?.TotalOrders ?? 0 }).ToList();
    }
    public async Task<object> GetTopSellingBooksAsync(int top)
    {
        return await _context.OrderDetails.Include(od => od.Order).Include(od => od.Book).Where(od => od.Order != null && od.Order.OrderStatus == OrderStatus.Paid).GroupBy(od => new { od.BookId, od.Book!.Title, od.Book.ImageUrl }).Select(g => new { BookId = g.Key.BookId, Title = g.Key.Title, ImageUrl = g.Key.ImageUrl, TotalSold = g.Sum(od => od.Quantity), TotalRevenue = g.Sum(od => od.LineTotal) }).OrderByDescending(x => x.TotalSold).Take(top).ToListAsync();
    }
}

public class InventoryRepository : IInventoryRepository
{
    private readonly ApplicationDbContext _context;
    public InventoryRepository(ApplicationDbContext context) => _context = context;
    public async Task AddImportReceiptAsync(ImportReceipt receipt) => await _context.ImportReceipts.AddAsync(receipt);
    public async Task<Book?> GetBookByIdAsync(int bookId) => await _context.Books.FindAsync(bookId);
    public async Task<IEnumerable<Book>> GetStockAsync(bool lowOnly = false, string? keyword = null)
    {
        var query = _context.Books
            .Include(b => b.Category)
            .Include(b => b.Area)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(b => b.Title.ToLower().Contains(kw) || b.Author.ToLower().Contains(kw) || b.Barcode.ToLower().Contains(kw));
        }

        if (lowOnly)
        {
            query = query.Where(b => (b.StockQuantity - b.ReservedQuantity) <= 5);
        }

        return await query.OrderByDescending(b => b.BookId).ToListAsync();
    }
    public async Task<bool> AdjustStockAsync(int bookId, int actualQuantity)
    {
        var book = await _context.Books.FindAsync(bookId);
        if (book == null) return false;
        book.StockQuantity = actualQuantity;
        await _context.SaveChangesAsync();
        return true;
    }
    public async Task<(IEnumerable<ImportReceipt> Items, int TotalCount)> GetReceiptsPagedAsync(int? supplierId, DateTime? fromDate, DateTime? toDate, int page, int pageSize)
    {
        var query = _context.ImportReceipts
            .Include(r => r.Supplier)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ImportReceiptDetails)
            .AsNoTracking()
            .AsQueryable();

        if (supplierId.HasValue && supplierId.Value > 0)
        {
            query = query.Where(r => r.SupplierId == supplierId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= toDate.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ImportReceiptId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
    public async Task<ImportReceipt?> GetReceiptDetailAsync(int id)
    {
        return await _context.ImportReceipts
            .Include(r => r.Supplier)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ImportReceiptDetails)
                .ThenInclude(d => d.Book)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ImportReceiptId == id);
    }
}

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;
    public OrderRepository(ApplicationDbContext context) => _context = context;
    public async Task<Dictionary<int, Book>> GetBooksByIdsAsync(IEnumerable<int> bookIds) => await _context.Books.Where(b => bookIds.Contains(b.BookId)).ToDictionaryAsync(b => b.BookId);
    public async Task IncreaseReservedQuantityAsync(int bookId, int quantity) => await _context.Books.Where(b => b.BookId == bookId).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity + quantity));
    public async Task DecreaseStockAndReservedQuantityAsync(int bookId, int quantity) => await _context.Books.Where(b => b.BookId == bookId).ExecuteUpdateAsync(s => s.SetProperty(b => b.StockQuantity, b => b.StockQuantity - quantity).SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - quantity));
    public async Task AddOrderAsync(Order order) => await _context.Orders.AddAsync(order);
    public async Task<bool> TryIncreaseReservedQuantityAsync(int bookId, int quantity)
    {
        var affected = await _context.Books
            .Where(b => b.BookId == bookId && b.IsActive && (b.StockQuantity - b.ReservedQuantity) >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity + quantity));
        return affected > 0;
    }
    public async Task<bool> TryTransitionOrderStatusAsync(int orderId, OrderStatus fromStatus, OrderStatus toStatus, DateTime? completedAt = null)
    {
        var query = _context.Orders.Where(o => o.OrderId == orderId && o.OrderStatus == fromStatus);
        int affected;
        if (completedAt.HasValue)
        {
            affected = await query.ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, toStatus).SetProperty(o => o.CompletedAt, completedAt.Value));
        }
        else
        {
            affected = await query.ExecuteUpdateAsync(s => s.SetProperty(o => o.OrderStatus, toStatus));
        }
        return affected > 0;
    }
    public async Task<Order?> GetPendingOrderByCodeAsync(string orderCode) => await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.OrderStatus == OrderStatus.Pending);
    public async Task<Order?> GetOrderByCodeAsync(string orderCode) => await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderCode == orderCode);
    public async Task<Order?> GetOrderByIdAsync(int orderId) => await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderId == orderId);
    // F01: Đọc trực tiếp từ DB bỏ qua EF tracking cache, dùng sau rollback để xác định trạng thái thật
    public async Task<Order?> GetOrderByCodeNoTrackingAsync(string orderCode) => await _context.Orders.AsNoTracking().Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderCode == orderCode);
    public async Task<Order?> GetReceiptOrderByIdAsync(int orderId) => await _context.Orders.AsNoTracking().Include(o => o.OrderDetails).ThenInclude(od => od.Book).FirstOrDefaultAsync(o => o.OrderId == orderId);
    public async Task ReleaseReservedQuantityAsync(int bookId, int quantity)
    {
        await _context.Books
            .Where(b => b.BookId == bookId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - quantity));
    }
    public async Task<(IEnumerable<Order> Items, int TotalCount)> GetOrdersPagedAsync(DateTime? fromDate, DateTime? toDate, OrderStatus? status, SaleChannel? channel, PaymentMethod? paymentMethod, string? keyword, bool? needsSupportOnly, int page, int pageSize)
    {
        var query = _context.Orders
            .Include(o => o.OrderDetails)
            .Include(o => o.PaymentTransactions)
            .Include(o => o.Member)
            .Include(o => o.CreatedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(o => o.OrderCode.ToLower().Contains(kw));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.OrderStatus == status.Value);
        }

        if (channel.HasValue)
        {
            query = query.Where(o => o.SaleChannel == channel.Value);
        }

        if (paymentMethod.HasValue)
        {
            query = query.Where(o => o.PaymentMethod == paymentMethod.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(o => o.CreatedAt <= toDate.Value);
        }

        if (needsSupportOnly.HasValue && needsSupportOnly.Value)
        {
            query = query.Where(o => o.OrderStatus != OrderStatus.Paid && o.PaymentTransactions.Any(pt => pt.Amount > 0));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }
    public async Task<Order?> GetOrderDetailWithRelationsAsync(int orderId)
    {
        return await _context.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Book)
            .Include(o => o.PaymentTransactions)
            .Include(o => o.Member)
            .Include(o => o.CreatedByUser)
            .Include(o => o.Promotion)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrderId == orderId);
    }
}

public class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _context;
    public PaymentRepository(ApplicationDbContext context) => _context = context;
    public async Task<bool> CheckTransactionExistsAsync(string referenceCode) => await _context.PaymentTransactions.AnyAsync(pt => pt.ReferenceCode == referenceCode);
    public async Task<PaymentTransaction?> GetByReferenceCodeAsync(string referenceCode) => await _context.PaymentTransactions.FirstOrDefaultAsync(pt => pt.ReferenceCode == referenceCode);
    public async Task AddTransactionAsync(PaymentTransaction transaction) => await _context.PaymentTransactions.AddAsync(transaction);
}

public class KioskRepository : IKioskRepository
{
    private readonly ApplicationDbContext _context;
    public KioskRepository(ApplicationDbContext context) => _context = context;
    public async Task<Kiosk?> GetByIdAsync(int id) => await _context.Kiosks.FindAsync(id);
    public async Task AddIncidentAsync(KioskIncident incident) => await _context.KioskIncidents.AddAsync(incident);
    public async Task<KioskIncident?> GetOpenIncidentAsync(int kioskId, string errorCode) => await _context.KioskIncidents.FirstOrDefaultAsync(i => i.KioskId == kioskId && i.ErrorCode == errorCode && i.ResolvedAt == null);
}

