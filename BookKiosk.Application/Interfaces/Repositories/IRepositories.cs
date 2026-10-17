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
    Task<bool> TryDeductPointsAsync(int memberId, int points);
    Task AddPointsAsync(int memberId, int points);
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
    Task<bool> TryIncreaseReservedQuantityAsync(int bookId, int quantity);
    Task<bool> TryTransitionOrderStatusAsync(int orderId, BookKiosk.Domain.Enums.OrderStatus fromStatus, BookKiosk.Domain.Enums.OrderStatus toStatus, System.DateTime? completedAt = null);
    Task<Order?> GetPendingOrderByCodeAsync(string orderCode);
    Task<Order?> GetOrderByCodeAsync(string orderCode);
    Task<Order?> GetOrderByIdAsync(int orderId);
    /// <summary>Đọc trạng thái order mới nhất từ DB, bỏ qua EF tracking cache. Dùng sau rollback để xác định trạng thái thật (F01).</summary>
    Task<Order?> GetOrderByCodeNoTrackingAsync(string orderCode);
    /// <summary>Đọc dữ liệu hóa đơn snapshot cho đơn hàng đã thanh toán kèm OrderDetails và Book (AsNoTracking).</summary>
    Task<Order?> GetReceiptOrderByIdAsync(int orderId);
}

public interface IPaymentRepository
{
    Task<bool> CheckTransactionExistsAsync(string referenceCode);
    Task<PaymentTransaction?> GetByReferenceCodeAsync(string referenceCode);
    Task AddTransactionAsync(PaymentTransaction transaction);
}

public interface IKioskRepository
{
    Task<Kiosk?> GetByIdAsync(int id);
    Task AddIncidentAsync(KioskIncident incident);
    Task<KioskIncident?> GetOpenIncidentAsync(int kioskId, string errorCode);
}
