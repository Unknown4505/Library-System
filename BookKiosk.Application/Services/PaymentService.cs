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
    Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload, string? expectedAccountNumber = null, string? expectedSubAccount = null);
    Task<(bool Success, string Status, string? Message, Order? Order)> SimulateDevSuccessAsync(string orderCode);
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

    public async Task<bool> HandleSePayWebhookAsync(PaymentWebhookPayload payload, string? expectedAccountNumber = null, string? expectedSubAccount = null)
    {
        var refCode = payload.EffectiveReferenceCode;
        if (string.IsNullOrWhiteSpace(refCode))
            return false;

        // BE-NEW-10: Bắt buộc giao dịch tiền vào (transferType = "in") - Không cho phép null/blank
        if (string.IsNullOrWhiteSpace(payload.transferType) || 
            !string.Equals(payload.transferType.Trim(), "in", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // BE-NEW-10: Bắt buộc phải có thông tin tài khoản nhận tiền (accountNumber hoặc subAccount)
        if (string.IsNullOrWhiteSpace(payload.accountNumber) && string.IsNullOrWhiteSpace(payload.subAccount))
        {
            return false;
        }

        // BE-NEW-10: Đối chiếu tài khoản ngân hàng thụ hưởng nếu có cấu hình - Không cho phép null/blank vượt qua
        if (!string.IsNullOrWhiteSpace(expectedAccountNumber))
        {
            if (string.IsNullOrWhiteSpace(payload.accountNumber) ||
                !string.Equals(payload.accountNumber.Trim(), expectedAccountNumber.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(expectedSubAccount))
        {
            if (string.IsNullOrWhiteSpace(payload.subAccount) ||
                !string.Equals(payload.subAccount.Trim(), expectedSubAccount.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Trích xuất OrderCode linh hoạt
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

        // BE-NEW-12: Lấy thông tin đơn hàng bất kể trạng thái để không bỏ sót late webhook
        var order = await _orderRepository.GetOrderByCodeAsync(extractedOrderCode);
        if (order == null)
        {
            // Kiểm tra xem giao dịch đã được xử lý trước đó chưa (idempotent replay)
            bool exists = await _paymentRepository.CheckTransactionExistsAsync(refCode);
            return exists;
        }

        // BE-NEW-12: Trường hợp webhook đến sau khi đơn hàng đã bị hủy (Cancelled)
        if (order.OrderStatus == OrderStatus.Cancelled)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                bool isDuplicate = await _paymentRepository.CheckTransactionExistsAsync(refCode);
                if (!isDuplicate)
                {
                    // Lưu vết giao dịch để đối soát và xử lý hoàn tiền thủ công
                    var latePaymentTx = new PaymentTransaction
                    {
                        OrderId = order.OrderId,
                        ReferenceCode = refCode,
                        Amount = payload.EffectiveAmount,
                        Gateway = payload.gateway ?? "SePay"
                    };
                    await _paymentRepository.AddTransactionAsync(latePaymentTx);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                }
                else
                {
                    await _unitOfWork.RollbackAsync();
                }

                // Không chuyển lại sang Paid, không trừ kho, không đụng điểm
                return true;
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                bool exists = await _paymentRepository.CheckTransactionExistsAsync(refCode);
                if (exists) return true;
                throw;
            }
        }

        // B06: Đơn hàng đã Paid trước đó -> Lưu giao dịch nếu có ReferenceCode mới để đối soát, không trừ kho / cộng điểm lần hai
        if (order.OrderStatus == OrderStatus.Paid)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                bool isDuplicate = await _paymentRepository.CheckTransactionExistsAsync(refCode);
                if (!isDuplicate)
                {
                    var additionalTx = new PaymentTransaction
                    {
                        OrderId = order.OrderId,
                        ReferenceCode = refCode,
                        Amount = payload.EffectiveAmount,
                        Gateway = payload.gateway ?? "SePay"
                    };
                    await _paymentRepository.AddTransactionAsync(additionalTx);
                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();
                }
                else
                {
                    await _unitOfWork.RollbackAsync();
                }

                // Không trừ kho, không cộng điểm lần hai
                return true;
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                bool exists = await _paymentRepository.CheckTransactionExistsAsync(refCode);
                if (exists) return true;
                throw;
            }
        }

        // Đơn hàng đang Pending -> Hoàn tất thanh toán an toàn
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

            // Điểm sử dụng đã được trừ nguyên tử lúc Checkout (BE-NEW-13)
            // Tại đây tích điểm thưởng từ giá trị đơn hàng thực tế: TotalAmount / 10000
            if (order.MemberId.HasValue)
            {
                int pointsEarned = (int)(order.TotalAmount / 10000);
                if (pointsEarned > 0)
                {
                    await _memberRepository.AddPointsAsync(order.MemberId.Value, pointsEarned);
                    await _memberRepository.AddPointTransactionAsync(new PointTransaction
                    {
                        MemberId = order.MemberId.Value,
                        OrderId = order.OrderId,
                        Points = pointsEarned, // Luôn dương theo Check Constraint
                        Type = PointTransactionType.Earned,
                        Description = $"Tích {pointsEarned} điểm từ đơn hàng {order.OrderCode}"
                    });
                }
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            // BE-NEW-11: Sau rollback, chỉ coi là duplicate khi transaction thực sự tồn tại trong DB
            bool exists = await _paymentRepository.CheckTransactionExistsAsync(refCode);
            if (exists)
            {
                return true;
            }
            // Lỗi database thực sự (FK, Check Constraint, data invalid) -> tuyệt đối không nuốt lỗi
            throw;
        }
    }

    public async Task<(bool Success, string Status, string? Message, Order? Order)> SimulateDevSuccessAsync(string orderCode)
    {
        var order = await _orderRepository.GetOrderByCodeAsync(orderCode);
        if (order == null)
            return (false, "NotFound", "Không tìm thấy đơn hàng.", null);

        if (order.OrderStatus == OrderStatus.Cancelled)
            return (false, "Conflict", "Đơn hàng đã bị hủy, không thể mô phỏng thanh toán.", order);

        if (order.OrderStatus == OrderStatus.Paid)
            return (true, "Paid", "Đơn hàng đã được thanh toán trước đó.", order);

        // Đơn đang Pending -> Hoàn tất đúng theo luồng giao dịch an toàn
        var devRefCode = $"DEV_{order.OrderId}_{DateTime.UtcNow.Ticks}";
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var completedAt = DateTime.UtcNow;
            var transitioned = await _orderRepository.TryTransitionOrderStatusAsync(order.OrderId, OrderStatus.Pending, OrderStatus.Paid, completedAt);
            if (!transitioned)
            {
                await _unitOfWork.RollbackAsync();
                var currentOrder = await _orderRepository.GetOrderByCodeAsync(orderCode);
                if (currentOrder?.OrderStatus == OrderStatus.Cancelled)
                {
                    return (false, "Conflict", "Đơn hàng đã bị hủy bởi tiến trình khác, không thể thanh toán.", currentOrder);
                }
                return (true, currentOrder?.OrderStatus.ToString() ?? "Paid", "Đơn hàng đã được xử lý bởi tiến trình khác.", currentOrder);
            }

            var paymentTx = new PaymentTransaction
            {
                OrderId = order.OrderId,
                ReferenceCode = devRefCode,
                Amount = order.TotalAmount,
                Gateway = "DEV_SIMULATION"
            };
            await _paymentRepository.AddTransactionAsync(paymentTx);

            foreach (var item in order.OrderDetails)
            {
                await _orderRepository.DecreaseStockAndReservedQuantityAsync(item.BookId, item.Quantity);
            }

            if (order.MemberId.HasValue)
            {
                int pointsEarned = (int)(order.TotalAmount / 10000);
                if (pointsEarned > 0)
                {
                    await _memberRepository.AddPointsAsync(order.MemberId.Value, pointsEarned);
                    await _memberRepository.AddPointTransactionAsync(new PointTransaction
                    {
                        MemberId = order.MemberId.Value,
                        OrderId = order.OrderId,
                        Points = pointsEarned,
                        Type = PointTransactionType.Earned,
                        Description = $"Tích {pointsEarned} điểm từ đơn hàng {order.OrderCode} (DEV)"
                    });
                }
            }

            order.OrderStatus = OrderStatus.Paid;
            order.CompletedAt = completedAt;

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            return (true, "Paid", "Thanh toán mô phỏng thành công.", order);
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
