using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public class PaymentWebhookPayload
{
    public long id { get; set; }
    public string? gateway { get; set; }
    public string? transactionDate { get; set; }
    public string? accountNumber { get; set; }
    public string? subAccount { get; set; }
    public string? code { get; set; }
    public string? content { get; set; }
    public string? transferType { get; set; }
    public string? description { get; set; }
    public decimal transferAmount { get; set; }
    public decimal accumulated { get; set; }
    public string? referenceCode { get; set; }

    // Backward compatibility cho mock và test suite cũ
    public decimal amountIn { get; set; }
    public string transactionContent { get; set; } = string.Empty;

    public decimal EffectiveAmount => transferAmount != 0 ? transferAmount : amountIn;
    public string EffectiveContent => !string.IsNullOrWhiteSpace(content) ? content : (!string.IsNullOrWhiteSpace(code) ? code : transactionContent);
    public string EffectiveReferenceCode => !string.IsNullOrWhiteSpace(referenceCode) ? referenceCode : (id > 0 ? $"SEPAY_{id}" : string.Empty);
}

public interface IPaymentService
{
    Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload);
}

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IMemberRepository _memberRepository;

    public PaymentService(IUnitOfWork unitOfWork, IPaymentRepository paymentRepository, IOrderRepository orderRepository, IMemberRepository memberRepository)
    {
        _unitOfWork = unitOfWork;
        _paymentRepository = paymentRepository;
        _orderRepository = orderRepository;
        _memberRepository = memberRepository;
    }

    public async Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload)
    {
        var refCode = payload.EffectiveReferenceCode;
        if (string.IsNullOrWhiteSpace(refCode))
            return false;

        // Trích xuất OrderCode
        string? extractedOrderCode = null;
        if (!string.IsNullOrWhiteSpace(payload.code) && payload.code.StartsWith("ORD", StringComparison.OrdinalIgnoreCase))
        {
            extractedOrderCode = payload.code.Trim();
        }
        else
        {
            var regex = new System.Text.RegularExpressions.Regex(@"ORD[-\w\d]+");
            var match = regex.Match(payload.EffectiveContent);
            if (match.Success)
            {
                extractedOrderCode = match.Value;
            }
            else if (!string.IsNullOrWhiteSpace(payload.description))
            {
                var descMatch = regex.Match(payload.description);
                if (descMatch.Success) extractedOrderCode = descMatch.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(extractedOrderCode)) return false;

        var order = await _orderRepository.GetPendingOrderByCodeAsync(extractedOrderCode);
        if (order == null)
        {
            // Kiểm tra xem đơn đã được xử lý Paid trước đó chưa (idempotent replay)
            bool exists = await _paymentRepository.CheckTransactionExistsAsync(refCode);
            return exists;
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            bool isDuplicate = await _paymentRepository.CheckTransactionExistsAsync(refCode);
            if (isDuplicate)
            {
                await _unitOfWork.RollbackAsync();
                return true; 
            }

            var actualAmount = payload.EffectiveAmount;
            var paymentTx = new PaymentTransaction
            {
                OrderId = order.OrderId,
                ReferenceCode = refCode,
                Amount = actualAmount,
                Gateway = payload.gateway ?? "SePay"
            };
            await _paymentRepository.AddTransactionAsync(paymentTx);

            if (actualAmount != order.TotalAmount)
            {
                // Sai số tiền: ghi nhận đối soát, giữ đơn Pending, không đổi trạng thái, không trừ kho
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return true; 
            }

            // Atomic state transition sang Paid (BE-NEW-02)
            var completedAt = DateTime.UtcNow;
            var transitioned = await _orderRepository.TryTransitionOrderStatusAsync(order.OrderId, OrderStatus.Pending, OrderStatus.Paid, completedAt);
            if (!transitioned)
            {
                // Đơn hàng đã bị hủy hoặc chuyển trạng thái bởi tiến trình khác
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return true;
            }

            order.OrderStatus = OrderStatus.Paid;
            order.CompletedAt = completedAt;

            foreach (var item in order.OrderDetails)
            {
                await _orderRepository.DecreaseStockAndReservedQuantityAsync(item.BookId, item.Quantity);
            }

            if (order.MemberId.HasValue)
            {
                var member = await _memberRepository.GetByIdAsync(order.MemberId.Value);
                if (member != null)
                {
                    if (order.PointsUsed > 0)
                    {
                        member.Points -= order.PointsUsed;
                        await _memberRepository.AddPointTransactionAsync(new PointTransaction
                        {
                            MemberId = member.MemberId,
                            Points = -order.PointsUsed,
                            Type = PointTransactionType.Redeemed
                        });
                    }
                    
                    int pointsEarned = (int)(order.TotalAmount / 10000);
                    if (pointsEarned > 0)
                    {
                        member.Points += pointsEarned;
                        await _memberRepository.AddPointTransactionAsync(new PointTransaction
                        {
                            MemberId = member.MemberId,
                            Points = pointsEarned,
                            Type = PointTransactionType.Earned
                        });
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            // Xung đột unique index referenceCode dưới concurrency -> xử lý idempotent thành công
            if (ex.GetType().Name.Contains("DbUpdateException") || 
                (ex.InnerException != null && ex.InnerException.GetType().Name.Contains("DbUpdateException")) ||
                ex.Message.Contains("ReferenceCode", StringComparison.OrdinalIgnoreCase) ||
                (ex.InnerException != null && ex.InnerException.Message.Contains("ReferenceCode", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            throw;
        }
    }
}
