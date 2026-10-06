using BookKiosk.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookKiosk.Application.Interfaces.Repositories;

public interface IUnitOfWork : System.IDisposable
{
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
    Task<int> SaveChangesAsync();
}

public interface IMemberRepository
{
    Task<Member?> GetByPhoneAsync(string phone);
    Task<Member?> GetByIdAsync(int id);
    Task<bool> CheckPhoneExistsAsync(string phone);
    Task AddAsync(Member member);
    Task<IEnumerable<PointTransaction>> GetPointHistoryAsync(int memberId);
    Task AddPointTransactionAsync(PointTransaction transaction);
}

public interface IPromotionRepository
{
    Task<IEnumerable<Promotion>> GetAllActiveAsync();
    Task<IEnumerable<Promotion>> GetAllAsync();
    Task<Promotion?> GetByIdAsync(int id);
    Task AddAsync(Promotion promotion);
}

public interface IReportRepository
{
    Task<object> GetRevenueByMonthAsync(int year);
    Task<object> GetTopSellingBooksAsync(int top);
}

public interface IInventoryRepository
{
    Task AddImportReceiptAsync(ImportReceipt receipt);
    Task<Book?> GetBookByIdAsync(int bookId);
}

public interface IOrderRepository
{
    Task<Dictionary<int, Book>> GetBooksByIdsAsync(IEnumerable<int> bookIds);
    Task IncreaseReservedQuantityAsync(int bookId, int quantity);
    Task DecreaseStockAndReservedQuantityAsync(int bookId, int quantity);
    Task AddOrderAsync(Order order);
    Task<Order?> GetPendingOrderByCodeAsync(string orderCode);
    Task<Order?> GetOrderByIdAsync(int orderId);
}

public interface IPaymentRepository
{
    Task<bool> CheckTransactionExistsAsync(string referenceCode);
    Task AddTransactionAsync(PaymentTransaction transaction);
}
