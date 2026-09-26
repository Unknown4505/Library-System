using BookKiosk.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.Services;

public class PaymentWebhookPayload
{
    public string referenceCode { get; set; } = string.Empty;
    public decimal amountIn { get; set; }
    public string transactionContent { get; set; } = string.Empty;
}

public interface IPaymentService
{
    Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload);
}

public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Xử lý Webhook thanh toán từ SePay
    /// Chống webhook gọi lặp bằng Idempotency Key (ReferenceCode).
    /// </summary>
    public async Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload)
    {
        // 1. Idempotency Check: Nếu ReferenceCode đã có trong PaymentTransactions -> Webhook trùng lặp -> Return true luôn để OK
        bool isDuplicate = await _context.PaymentTransactions.AnyAsync(pt => pt.ReferenceCode == payload.referenceCode);
        if (isDuplicate)
            return true; 

        // 2. Tìm OrderCode trong transactionContent bằng Regex
        // Giả sử mã đơn hàng bắt đầu bằng "ORD" theo sau là 14 chữ số (định dạng: yyyyMMddHHmmss)
        var regex = new System.Text.RegularExpressions.Regex(@"ORD\d{14}");
        var match = regex.Match(payload.transactionContent);
        
        if (!match.Success) return false;
        var extractedOrderCode = match.Value;

        var order = await _context.Orders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderCode == extractedOrderCode && o.OrderStatus == OrderStatus.Pending);

        if (order == null) return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var paymentTx = new PaymentTransaction
            {
                OrderId = order.OrderId,
                ReferenceCode = payload.referenceCode,
                Amount = payload.amountIn,
                Gateway = "SePay"
            };
            _context.PaymentTransactions.Add(paymentTx);

            // 3. Đối soát tiền
            if (payload.amountIn != order.TotalAmount)
            {
                // LỆCH TIỀN: Lưu log giao dịch lệch, giữ status Pending, KHÔNG trừ kho
                // TODO: Bắn cảnh báo cho Admin
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true; // Vẫn trả về True cho SePay để nó không gửi lại
            }

            // 4. Nếu ĐÚNG TIỀN: Cập nhật Status = Paid
            order.OrderStatus = OrderStatus.Paid;
            order.CompletedAt = DateTime.Now;

            // 5. Chốt kho thực tế & Tích điểm
            foreach (var item in order.OrderDetails)
            {
                // Trừ cả tồn kho vật lý và nhả tồn kho giữ chỗ
                await _context.Books
                    .Where(b => b.BookId == item.BookId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(b => b.StockQuantity, b => b.StockQuantity - item.Quantity)
                        .SetProperty(b => b.ReservedQuantity, b => b.ReservedQuantity - item.Quantity));
            }

            if (order.MemberId.HasValue)
            {
                var member = await _context.Members.FindAsync(order.MemberId);
                if (member != null)
                {
                    // Trừ điểm đã dùng
                    if (order.PointsUsed > 0)
                    {
                        member.Points -= order.PointsUsed;
                        _context.PointTransactions.Add(new PointTransaction
                        {
                            MemberId = member.MemberId,
                            Points = -order.PointsUsed,
                            Type = PointTransactionType.Redeemed
                        });
                    }
                    
                    // Cộng điểm thưởng mới (10.000đ = 1đ)
                    int pointsEarned = (int)(order.TotalAmount / 10000);
                    if (pointsEarned > 0)
                    {
                        member.Points += pointsEarned;
                        _context.PointTransactions.Add(new PointTransaction
                        {
                            MemberId = member.MemberId,
                            Points = pointsEarned,
                            Type = PointTransactionType.Earned
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
