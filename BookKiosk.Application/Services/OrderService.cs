using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IOrderService
{
    Task<CheckoutResponseDto> CheckoutKioskAsync(CheckoutRequestDto request);
    Task<bool> CancelOrderAsync(int orderId);
    Task<PaymentStatusDto> GetPaymentStatusAsync(int orderId);
}

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IMemberRepository _memberRepository;

    public OrderService(IUnitOfWork unitOfWork, IOrderRepository orderRepository, IPromotionRepository promotionRepository, IMemberRepository memberRepository)
    {
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _promotionRepository = promotionRepository;
        _memberRepository = memberRepository;
    }

    public async Task<CheckoutResponseDto> CheckoutKioskAsync(CheckoutRequestDto request)
    {
        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("Danh sách sản phẩm không được để trống.");

        // A04: Validate BookId trùng lặp trong Items
        var duplicateBookIds = request.Items
            .GroupBy(i => i.BookId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateBookIds.Any())
        {
            throw new ArgumentException($"Danh sách sản phẩm không được chứa các mã sách trùng lặp (BookId: {string.Join(", ", duplicateBookIds)}). Vui lòng gộp số lượng trước khi đặt hàng.");
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Số lượng sách (ID: {item.BookId}) phải lớn hơn 0.");
        }

        // D01: Validate điểm sử dụng theo API contract (tối đa 100 điểm, không cho phép số âm hoặc vượt quá 100)
        if (request.PointsToUse < 0 || request.PointsToUse > 100)
        {
            throw new ArgumentException("Số điểm sử dụng phải từ 0 đến 100 điểm.");
        }

        if (request.PointsToUse > 0 && !request.MemberId.HasValue)
        {
            throw new ArgumentException("Cần cung cấp thông tin thành viên để sử dụng điểm.");
        }

        var sortedItems = request.Items.OrderBy(i => i.BookId).ToList();
        var bookIds = sortedItems.Select(i => i.BookId).ToList();

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var books = await _orderRepository.GetBooksByIdsAsync(bookIds);

            decimal subTotal = 0;

            foreach (var item in sortedItems)
            {
                if (!books.TryGetValue(item.BookId, out var book))
                    throw new System.Collections.Generic.KeyNotFoundException($"Không tìm thấy sách ID: {item.BookId}");

                if (!book.IsActive)
                    throw new InvalidOperationException($"Sách '{book.Title}' đã ngừng kinh doanh.");

                // Conditional atomic reserve ngay tại DB
                var reserved = await _orderRepository.TryIncreaseReservedQuantityAsync(item.BookId, item.Quantity);
                if (!reserved)
                    throw new InvalidOperationException($"Sách '{book.Title}' đã hết hàng hoặc không đủ số lượng tồn kho khả dụng.");

                subTotal += book.SellingPrice * item.Quantity;
            }

            var activePromotions = await _promotionRepository.GetAllActiveAsync();

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

            decimal totalAmountWithoutPoints = subTotal - maxDiscountAmount;
            if (totalAmountWithoutPoints < 0) totalAmountWithoutPoints = 0;

            decimal pointsUsedAmount = 0m;
            if (request.MemberId.HasValue && request.PointsToUse > 0)
            {
                var member = await _memberRepository.GetByIdAsync(request.MemberId.Value);
                if (member == null)
                    throw new System.Collections.Generic.KeyNotFoundException($"Không tìm thấy thành viên ID: {request.MemberId.Value}");

                // D01/D03: Không âm thầm giảm điểm, nếu không đủ điểm khả dụng phải báo lỗi
                if (member.Points < request.PointsToUse)
                    throw new InvalidOperationException($"Thành viên không đủ điểm khả dụng. Số điểm hiện có: {member.Points}, yêu cầu: {request.PointsToUse}.");

                // Quy đổi 1 điểm = 1.000 VNĐ. Số tiền giảm từ điểm không vượt quá giá trị đơn hàng
                decimal requestedPointsDiscount = request.PointsToUse * 1000m;
                if (requestedPointsDiscount > totalAmountWithoutPoints)
                    throw new InvalidOperationException("Số điểm sử dụng vượt quá giá trị thanh toán của đơn hàng.");

                // Giữ điểm nguyên tử tại DB chống race condition 2 đơn cùng member
                var pointsDeducted = await _memberRepository.TryDeductPointsAsync(request.MemberId.Value, request.PointsToUse);
                if (!pointsDeducted)
                    throw new InvalidOperationException("Thành viên không đủ điểm khả dụng để sử dụng.");

                pointsUsedAmount = requestedPointsDiscount;
            }
            else
            {
                request.PointsToUse = 0;
            }

            decimal totalAmount = totalAmountWithoutPoints - pointsUsedAmount;
            if (totalAmount < 0) totalAmount = 0;

            var tempOrderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-TEMP-{Guid.NewGuid():N}";
            var order = new Order
            {
                OrderCode = tempOrderCode,
                SaleChannel = SaleChannel.Kiosk,
                OrderStatus = OrderStatus.Pending,
                PaymentMethod = PaymentMethod.QR,
                MemberId = request.MemberId,
                PromotionId = appliedPromotionId,
                SubTotal = subTotal,
                DiscountAmount = maxDiscountAmount,
                PointsUsed = request.PointsToUse,
                TotalAmount = totalAmount,
            };

            foreach (var item in sortedItems)
            {
                order.OrderDetails.Add(new OrderDetail
                {
                    BookId = item.BookId,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = books[item.BookId].SellingPrice,
                    LineTotal = books[item.BookId].SellingPrice * item.Quantity
                });
            }

            await _orderRepository.AddOrderAsync(order);
            await _unitOfWork.SaveChangesAsync(); // SQL Server tự sinh OrderId duy nhất

            // Định dạng OrderCode chuẩn ORD-yyyyMMdd-XXXX dựa trên OrderId để triệt tiêu collision 100%
            order.OrderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D4}";

            // Ghi nhận giao dịch trừ điểm (Points > 0 tuân thủ Check Constraint CK_PointTransactions_Points)
            if (request.MemberId.HasValue && request.PointsToUse > 0)
            {
                await _memberRepository.AddPointTransactionAsync(new PointTransaction
                {
                    MemberId = request.MemberId.Value,
                    OrderId = order.OrderId,
                    Points = request.PointsToUse,
                    Type = PointTransactionType.Redeemed,
                    Description = $"Sử dụng {request.PointsToUse} điểm cho đơn {order.OrderCode}"
                });
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            var sepayQrCodeUrl = $"https://qr.sepay.vn/img?acc=0366994409&bank=MB&amount={(int)totalAmount}&des={order.OrderCode}";

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
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> CancelOrderAsync(int orderId)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        if (order == null) throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy đơn hàng.");
        
        if (order.OrderStatus != OrderStatus.Pending)
            throw new InvalidOperationException("Đơn hàng không ở trạng thái chờ thanh toán để hủy.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transitioned = await _orderRepository.TryTransitionOrderStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Cancelled);
            if (!transitioned)
                throw new InvalidOperationException("Đơn hàng đã được thanh toán hoặc đã bị hủy bởi tiến trình khác.");

            // Nhả lại sách (trừ đi phần đã Reserved)
            foreach (var detail in order.OrderDetails)
            {
                await _orderRepository.IncreaseReservedQuantityAsync(detail.BookId, -detail.Quantity);
            }

            // Hoàn lại điểm thành viên nếu đơn hàng có sử dụng điểm
            if (order.MemberId.HasValue && order.PointsUsed > 0)
            {
                await _memberRepository.AddPointsAsync(order.MemberId.Value, order.PointsUsed);
                await _memberRepository.AddPointTransactionAsync(new PointTransaction
                {
                    MemberId = order.MemberId.Value,
                    OrderId = order.OrderId,
                    Points = order.PointsUsed, // Luôn dương theo constraint
                    Type = PointTransactionType.Earned,
                    Description = $"Hoàn {order.PointsUsed} điểm do đơn hàng {order.OrderCode} bị hủy"
                });
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<PaymentStatusDto> GetPaymentStatusAsync(int orderId)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        if (order == null) throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy đơn hàng.");
        
        return new PaymentStatusDto
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            Status = order.OrderStatus.ToString()
        };
    }
}
