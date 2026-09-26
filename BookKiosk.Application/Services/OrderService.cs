using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Application.Services;

public interface IOrderService
{
    Task<CheckoutResponseDto> CheckoutKioskAsync(CheckoutRequestDto request);
}

public class OrderService : IOrderService
{
    private readonly ApplicationDbContext _context;

    public OrderService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Xử lý thanh toán giỏ hàng từ Kiosk.
    /// Bao gồm: Validate tồn kho khả dụng, tính toán khuyến mãi, trừ điểm, lưu Database (Status: Pending)
    /// và giữ chỗ kho (cộng vào ReservedQuantity) bằng ExecuteUpdate để tránh deadlock.
    /// </summary>
    /// <param name="request">Thông tin giỏ hàng</param>
    /// <returns>Order Code, Total Amount và QRCode url</returns>
    public async Task<CheckoutResponseDto> CheckoutKioskAsync(CheckoutRequestDto request)
    {
        // 1. Lock / Sort mảng items theo BookId tăng dần để chống Deadlock DB
        var sortedItems = request.Items.OrderBy(i => i.BookId).ToList();
        var bookIds = sortedItems.Select(i => i.BookId).ToList();

        // Mở transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var books = await _context.Books
                .Where(b => bookIds.Contains(b.BookId))
                .ToDictionaryAsync(b => b.BookId);

            decimal subTotal = 0;

            // 2. Validate Giỏ hàng (Check AvailableStock)
            foreach (var item in sortedItems)
            {
                if (!books.TryGetValue(item.BookId, out var book))
                    throw new Exception($"Không tìm thấy sách ID: {item.BookId}");

                var availableStock = book.StockQuantity - book.ReservedQuantity;
                if (availableStock < item.Quantity)
                    throw new Exception($"Sách '{book.Title}' đã hết hàng hoặc không đủ số lượng (Chỉ còn {availableStock}).");

                subTotal += book.SellingPrice * item.Quantity;
                
                // Update giữ kho bằng ExecuteUpdate (Atomic) thay vì SaveChanges thông thường để chống Race Condition
                await _context.Books
                    .Where(b => b.BookId == item.BookId)
                    .ExecuteUpdateAsync(s => s.SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity + item.Quantity));
            }

            // 3. Áp dụng Khuyến mãi (Chọn CTKM có lợi nhất)
            var activePromotions = await _context.Promotions
                .Include(p => p.OrderDiscount)
                .Where(p => p.IsActive && p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                .ToListAsync();

            decimal maxDiscountAmount = 0;
            int? appliedPromotionId = null;

            foreach (var promo in activePromotions)
            {
                if (promo.OrderDiscount != null && subTotal >= promo.OrderDiscount.MinOrderValue)
                {
                    if (promo.OrderDiscount.DiscountAmount > maxDiscountAmount)
                    {
                        maxDiscountAmount = promo.OrderDiscount.DiscountAmount;
                        appliedPromotionId = promo.PromotionId;
                    }
                }
            }

            // 4. Tính toán Điểm & Tổng tiền
            int pointsUsedAmount = 0;
            if (request.MemberId.HasValue && request.PointsToUse > 0)
            {
                var member = await _context.Members.FindAsync(request.MemberId.Value);
                if (member != null && member.Points >= request.PointsToUse)
                {
                    pointsUsedAmount = request.PointsToUse * 1000; // 1 điểm = 1000 VNĐ
                }
                else
                {
                    request.PointsToUse = 0; // Reset nếu không đủ điểm
                }
            }

            decimal totalAmount = subTotal - maxDiscountAmount - pointsUsedAmount;
            if (totalAmount < 0) totalAmount = 0;

            // 5. Lưu Order (Pending)
            var orderCode = "ORD" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var order = new Order
            {
                OrderCode = orderCode,
                SaleChannel = SaleChannel.Kiosk,
                OrderStatus = OrderStatus.Pending,
                PaymentMethod = PaymentMethod.BankTransfer,
                MemberId = request.MemberId,
                PromotionId = appliedPromotionId,
                SubTotal = subTotal,
                DiscountAmount = maxDiscountAmount,
                PointsUsed = request.PointsToUse,
                TotalAmount = totalAmount,
                // Không set CompletedAt lúc này
            };

            foreach (var item in sortedItems)
            {
                order.OrderDetails.Add(new OrderDetail
                {
                    BookId = item.BookId,
                    Quantity = item.Quantity,
                    UnitPrice = books[item.BookId].SellingPrice,
                    TotalPrice = books[item.BookId].SellingPrice * item.Quantity
                });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Giả lập sinh link QR SePay (Thực tế sẽ gọi API SePay hoặc ghép chuỗi VietQR)
            var sepayQrCodeUrl = $"https://qr.sepay.vn/img?acc=0366994409&bank=MB&amount={(int)totalAmount}&des={orderCode}";

            return new CheckoutResponseDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                SubTotal = subTotal,
                DiscountAmount = maxDiscountAmount,
                PointsUsedAmount = pointsUsedAmount,
                TotalAmount = totalAmount,
                SepayQrCodeUrl = sepayQrCodeUrl
            };
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
