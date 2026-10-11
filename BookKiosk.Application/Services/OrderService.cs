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

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Số lượng sách (ID: {item.BookId}) phải lớn hơn 0.");
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
                if (request.PointsToUse > 100)
                    request.PointsToUse = 100;

                var member = await _memberRepository.GetByIdAsync(request.MemberId.Value);
                if (member != null && member.Points > 0)
                {
                    int maxPointsByAmount = (int)Math.Floor(totalAmountWithoutPoints / 1000m);
                    int maxPointsAllowed = Math.Min(100, Math.Min(member.Points, maxPointsByAmount));

                    request.PointsToUse = Math.Max(0, Math.Min(request.PointsToUse, maxPointsAllowed));
                    pointsUsedAmount = request.PointsToUse * 1000m;
                }
                else
                {
                    request.PointsToUse = 0;
                }
            }
            else
            {
                request.PointsToUse = 0;
            }

            decimal totalAmount = totalAmountWithoutPoints - pointsUsedAmount;
            if (totalAmount < 0) totalAmount = 0;

            var orderCode = $"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
            var order = new Order
            {
                OrderCode = orderCode,
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
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

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
