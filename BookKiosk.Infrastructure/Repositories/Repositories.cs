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
}

public class PromotionRepository : IPromotionRepository
{
    private readonly ApplicationDbContext _context;
    public PromotionRepository(ApplicationDbContext context) => _context = context;
    public async Task<IEnumerable<Promotion>> GetAllActiveAsync() => await _context.Promotions.Include(p => p.OrderDiscount).Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now).ToListAsync();
    public async Task<IEnumerable<Promotion>> GetAllAsync() => await _context.Promotions.Include(p => p.OrderDiscount).OrderByDescending(p => p.PromotionId).ToListAsync();
    public async Task<Promotion?> GetByIdAsync(int id) => await _context.Promotions.FindAsync(id);
    public async Task AddAsync(Promotion promotion) => await _context.Promotions.AddAsync(promotion);
}

public class ReportRepository : IReportRepository
{
    private readonly ApplicationDbContext _context;
    public ReportRepository(ApplicationDbContext context) => _context = context;
    public async Task<object> GetRevenueByMonthAsync(int year)
    {
        var rawData = await _context.Orders.Where(o => o.OrderStatus == OrderStatus.Paid && o.CompletedAt.HasValue && o.CompletedAt.Value.Year == year).GroupBy(o => o.CompletedAt.Value.Month).Select(g => new { Month = g.Key, Revenue = g.Sum(o => o.TotalAmount), TotalOrders = g.Count() }).ToListAsync();
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
}

public class OrderRepository : IOrderRepository
{
    private readonly ApplicationDbContext _context;
    public OrderRepository(ApplicationDbContext context) => _context = context;
    public async Task<Dictionary<int, Book>> GetBooksByIdsAsync(IEnumerable<int> bookIds) => await _context.Books.Where(b => bookIds.Contains(b.BookId)).ToDictionaryAsync(b => b.BookId);
    public async Task IncreaseReservedQuantityAsync(int bookId, int quantity) => await _context.Books.Where(b => b.BookId == bookId).ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity + quantity));
    public async Task DecreaseStockAndReservedQuantityAsync(int bookId, int quantity) => await _context.Books.Where(b => b.BookId == bookId).ExecuteUpdateAsync(s => s.SetProperty(b => b.StockQuantity, b => b.StockQuantity - quantity).SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - quantity));
    public async Task AddOrderAsync(Order order) => await _context.Orders.AddAsync(order);
    public async Task<Order?> GetPendingOrderByCodeAsync(string orderCode) => await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.OrderStatus == OrderStatus.Pending);
    public async Task<Order?> GetOrderByIdAsync(int orderId) => await _context.Orders.Include(o => o.OrderDetails).FirstOrDefaultAsync(o => o.OrderId == orderId);
}

public class PaymentRepository : IPaymentRepository
{
    private readonly ApplicationDbContext _context;
    public PaymentRepository(ApplicationDbContext context) => _context = context;
    public async Task<bool> CheckTransactionExistsAsync(string referenceCode) => await _context.PaymentTransactions.AnyAsync(pt => pt.ReferenceCode == referenceCode);
    public async Task AddTransactionAsync(PaymentTransaction transaction) => await _context.PaymentTransactions.AddAsync(transaction);
}
