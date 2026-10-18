using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public interface IOrderService
{
    Task<CheckoutResponseDto> CheckoutKioskAsync(CheckoutRequestDto request);
    Task<bool> CancelOrderAsync(int orderId);
    Task<PaymentStatusDto> GetPaymentStatusAsync(int orderId);
    Task<ReceiptDto> GetReceiptAsync(int orderId);

    // POS Counter & Orders CMS
    Task<CounterCheckoutResponseDto> CheckoutCounterAsync(CounterCheckoutRequestDto request, int cashierUserId);
    Task<PosReceiptDto> ConfirmCounterPaymentAsync(int orderId, int cashierUserId);
    Task<bool> CancelCounterOrderAsync(int orderId, int cashierUserId);
    Task<OrderDetailDto> GetOrderDetailAsync(int orderId);
    Task<PaginatedResultDto<OrderListItemDto>> GetOrdersAsync(OrderFilterDto filter);
}

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderRepository _orderRepository;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IPaymentRepository _paymentRepository;
    private readonly IConfiguration? _configuration;

    public OrderService(
        IUnitOfWork unitOfWork,
        IOrderRepository orderRepository,
        IPromotionRepository promotionRepository,
        IMemberRepository memberRepository,
        IPaymentRepository paymentRepository,
        IConfiguration? configuration = null)
    {
        _unitOfWork = unitOfWork;
        _orderRepository = orderRepository;
        _promotionRepository = promotionRepository;
        _memberRepository = memberRepository;
        _paymentRepository = paymentRepository;
        _configuration = configuration;
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

    public async Task<ReceiptDto> GetReceiptAsync(int orderId)
    {
        var order = await _orderRepository.GetReceiptOrderByIdAsync(orderId);
        if (order == null)
            throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy đơn hàng.");

        if (order.OrderStatus != OrderStatus.Paid || !order.CompletedAt.HasValue)
            throw new InvalidOperationException("Đơn hàng chưa hoàn tất thanh toán hoặc đã bị hủy.");

        return new ReceiptDto
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt.Value,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            PointsUsed = order.PointsUsed,
            PointsUsedAmount = order.PointsUsed * 1000m,
            TotalAmount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod,
            Items = order.OrderDetails.Select(od => new ReceiptLineDto
            {
                BookId = od.BookId,
                BookTitle = od.Book?.Title ?? string.Empty,
                Quantity = od.Quantity,
                UnitPriceAtTime = od.UnitPriceAtTime,
                LineTotal = od.LineTotal
            }).ToList()
        };
    }

    public async Task<CounterCheckoutResponseDto> CheckoutCounterAsync(CounterCheckoutRequestDto request, int cashierUserId)
    {
        if (request.Items == null || !request.Items.Any())
            throw new ArgumentException("Danh sách sản phẩm không được để trống.");

        var duplicateBookIds = request.Items
            .GroupBy(i => i.BookId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateBookIds.Any())
        {
            throw new ArgumentException($"Danh sách sản phẩm không được chứa các mã sách trùng lặp (BookId: {string.Join(", ", duplicateBookIds)}). Vui lòng gộp số lượng trước khi tạo đơn.");
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Số lượng sách (ID: {item.BookId}) phải lớn hơn 0.");
        }

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

        var paymentMethod = string.Equals(request.PaymentMethod, "QR", StringComparison.OrdinalIgnoreCase)
            ? PaymentMethod.QR
            : PaymentMethod.Cash;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var books = await _orderRepository.GetBooksByIdsAsync(bookIds);

            decimal subTotal = 0;
            var orderItemDtos = new List<CounterOrderItemDto>();

            foreach (var item in sortedItems)
            {
                if (!books.TryGetValue(item.BookId, out var book))
                    throw new System.Collections.Generic.KeyNotFoundException($"Không tìm thấy sách ID: {item.BookId}");

                if (!book.IsActive)
                    throw new InvalidOperationException($"Sách '{book.Title}' đã ngừng kinh doanh.");

                var reserved = await _orderRepository.TryIncreaseReservedQuantityAsync(item.BookId, item.Quantity);
                if (!reserved)
                    throw new InvalidOperationException($"Sách '{book.Title}' đã hết hàng hoặc không đủ số lượng tồn kho khả dụng.");

                var lineTotal = book.SellingPrice * item.Quantity;
                subTotal += lineTotal;

                orderItemDtos.Add(new CounterOrderItemDto
                {
                    BookId = item.BookId,
                    Title = book.Title,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    LineTotal = lineTotal
                });
            }

            decimal discountAmount = 0;
            int? appliedPromotionId = null;

            if (request.PromotionId.HasValue && request.PromotionId.Value > 0)
            {
                var promo = await _promotionRepository.GetByIdAsync(request.PromotionId.Value);
                if (promo != null && promo.IsActive && promo.StartDate <= DateTime.Now && promo.EndDate >= DateTime.Now)
                {
                    if (promo.OrderDiscount == null || subTotal >= promo.OrderDiscount.MinOrderValue)
                    {
                        discountAmount = promo.OrderDiscount?.DiscountAmount ?? 0;
                        appliedPromotionId = promo.PromotionId;
                    }
                }
            }

            decimal pointsUsedAmount = 0;
            if (request.PointsToUse > 0 && request.MemberId.HasValue)
            {
                var member = await _memberRepository.GetByIdAsync(request.MemberId.Value);
                if (member == null)
                    throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy thông tin thành viên.");

                if (member.Points < request.PointsToUse)
                    throw new InvalidOperationException("Thành viên không đủ số điểm để sử dụng.");

                var deducted = await _memberRepository.TryDeductPointsAsync(member.MemberId, request.PointsToUse);
                if (!deducted)
                    throw new InvalidOperationException("Thành viên không đủ số điểm để sử dụng.");

                pointsUsedAmount = request.PointsToUse * 1000m;
            }

            decimal totalAmount = Math.Max(0, subTotal - discountAmount - pointsUsedAmount);

            var order = new Order
            {
                SaleChannel = SaleChannel.Counter,
                OrderStatus = OrderStatus.Pending,
                PaymentMethod = paymentMethod,
                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                PointsUsed = request.PointsToUse,
                TotalAmount = totalAmount,
                MemberId = request.MemberId,
                PromotionId = appliedPromotionId,
                UserId = cashierUserId > 0 ? cashierUserId : null,
                CreatedAt = DateTime.UtcNow
            };

            await _orderRepository.AddOrderAsync(order);
            await _unitOfWork.SaveChangesAsync();

            order.OrderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{order.OrderId:D4}";

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

            foreach (var item in sortedItems)
            {
                var book = books[item.BookId];
                order.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    BookId = item.BookId,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    LineTotal = book.SellingPrice * item.Quantity
                });
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            DateTime? expiresAt = null;
            string? sepayQrUrl = null;
            if (paymentMethod == PaymentMethod.QR)
            {
                int timeoutMinutes = 4;
                if (_configuration?["OrderSettings:OrderTimeoutMinutes"] != null &&
                    int.TryParse(_configuration["OrderSettings:OrderTimeoutMinutes"], out var parsedTimeout))
                {
                    timeoutMinutes = parsedTimeout;
                }
                expiresAt = order.CreatedAt.AddMinutes(timeoutMinutes);
                var accountNumber = _configuration?["SePay:AccountNumber"] ?? "0366994409";
                var bankCode = _configuration?["SePay:BankCode"] ?? _configuration?["SePay:BankName"] ?? "MB";
                sepayQrUrl = $"https://qr.sepay.vn/img?acc={accountNumber}&bank={bankCode}&amount={(long)order.TotalAmount}&des={order.OrderCode}";
            }

            return new CounterCheckoutResponseDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                OrderStatus = order.OrderStatus,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                PointsUsed = order.PointsUsed,
                PointsUsedAmount = pointsUsedAmount,
                TotalAmount = order.TotalAmount,
                SepayQrCodeUrl = sepayQrUrl,
                ExpiresAt = expiresAt,
                CreatedAt = order.CreatedAt,
                ServerTime = DateTime.UtcNow,
                RemainingSeconds = expiresAt.HasValue ? Math.Max(0, (int)(expiresAt.Value - DateTime.UtcNow).TotalSeconds) : null,
                Items = orderItemDtos
            };
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<PosReceiptDto> ConfirmCounterPaymentAsync(int orderId, int cashierUserId)
    {
        var order = await _orderRepository.GetOrderDetailWithRelationsAsync(orderId);
        if (order == null)
            throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy thông tin đơn hàng.");

        if (order.SaleChannel != SaleChannel.Counter)
            throw new InvalidOperationException("Chỉ cho phép xác nhận thanh toán tiền mặt cho đơn hàng tại quầy thu ngân.");

        // Idempotency: nếu đơn đã Paid, trả về hóa đơn hiện tại
        if (order.OrderStatus == OrderStatus.Paid && order.CompletedAt.HasValue)
        {
            return BuildPosReceipt(order);
        }

        // Chặn thẳng đơn QR tại luồng thanh toán tiền mặt
        if (order.PaymentMethod == PaymentMethod.QR)
        {
            if (order.OrderStatus != OrderStatus.Paid)
            {
                throw new InvalidOperationException("Đơn hàng thanh toán bằng mã QR chưa hoàn tất thanh toán qua hệ thống. Không thể chốt thanh toán tiền mặt tại quầy.");
            }
            return BuildPosReceipt(order);
        }

        if (order.OrderStatus == OrderStatus.Cancelled)
            throw new InvalidOperationException("Không thể xác nhận thanh toán cho đơn hàng đã bị hủy.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transitioned = await _orderRepository.TryTransitionOrderStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Paid, DateTime.UtcNow);
            if (!transitioned)
            {
                var checkOrder = await _orderRepository.GetOrderByCodeNoTrackingAsync(order.OrderCode);
                if (checkOrder?.OrderStatus == OrderStatus.Paid)
                {
                    return BuildPosReceipt(order);
                }
                throw new InvalidOperationException("Xác nhận thanh toán thất bại do trạng thái đơn hàng đã thay đổi.");
            }

            foreach (var item in order.OrderDetails)
            {
                await _orderRepository.DecreaseStockAndReservedQuantityAsync(item.BookId, item.Quantity);
            }

            int pointsEarned = 0;
            if (order.MemberId.HasValue && order.TotalAmount >= 10000)
            {
                pointsEarned = (int)(order.TotalAmount / 10000);
                if (pointsEarned > 0)
                {
                    await _memberRepository.AddPointsAsync(order.MemberId.Value, pointsEarned);
                    await _memberRepository.AddPointTransactionAsync(new PointTransaction
                    {
                        MemberId = order.MemberId.Value,
                        Points = pointsEarned,
                        Type = PointTransactionType.Earned,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            var payment = new PaymentTransaction
            {
                OrderId = order.OrderId,
                Gateway = "Cash",
                Amount = order.TotalAmount,
                ReferenceCode = $"CASH-{order.OrderCode}",
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepository.AddTransactionAsync(payment);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitAsync();

            order.OrderStatus = OrderStatus.Paid;
            order.CompletedAt = DateTime.UtcNow;

            var receipt = BuildPosReceipt(order);
            receipt.PointsEarned = pointsEarned > 0 ? pointsEarned : null;
            return receipt;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> CancelCounterOrderAsync(int orderId, int cashierUserId)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        if (order == null)
            throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy thông tin đơn hàng.");

        if (order.SaleChannel != SaleChannel.Counter)
            throw new InvalidOperationException("Chỉ cho phép hủy đơn hàng tại quầy thu ngân.");

        if (order.OrderStatus == OrderStatus.Paid)
            throw new InvalidOperationException("Không thể hủy đơn hàng đã hoàn tất thanh toán.");

        if (order.OrderStatus == OrderStatus.Cancelled)
            return true;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var transitioned = await _orderRepository.TryTransitionOrderStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Cancelled);
            if (!transitioned)
            {
                var checkOrder = await _orderRepository.GetOrderByCodeNoTrackingAsync(order.OrderCode);
                if (checkOrder?.OrderStatus == OrderStatus.Paid)
                    throw new InvalidOperationException("Không thể hủy đơn hàng đã hoàn tất thanh toán.");
                return true;
            }

            foreach (var item in order.OrderDetails)
            {
                await _orderRepository.ReleaseReservedQuantityAsync(item.BookId, item.Quantity);
            }

            if (order.MemberId.HasValue && order.PointsUsed > 0)
            {
                await _memberRepository.AddPointsAsync(order.MemberId.Value, order.PointsUsed);
                await _memberRepository.AddPointTransactionAsync(new PointTransaction
                {
                    MemberId = order.MemberId.Value,
                    OrderId = order.OrderId,
                    Points = order.PointsUsed,
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

    public async Task<OrderDetailDto> GetOrderDetailAsync(int orderId)
    {
        var order = await _orderRepository.GetOrderDetailWithRelationsAsync(orderId);
        if (order == null)
            throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy thông tin đơn hàng.");

        return new OrderDetailDto
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt,
            SaleChannel = order.SaleChannel,
            OrderStatus = order.OrderStatus,
            PaymentMethod = order.PaymentMethod,
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            PromotionName = order.Promotion?.Name,
            PointsUsed = order.PointsUsed,
            PointsUsedAmount = order.PointsUsed * 1000m,
            TotalAmount = order.TotalAmount,
            CashierName = order.CreatedByUser?.FullName,
            MemberName = order.Member?.FullName,
            MemberPhone = order.Member?.PhoneNumber,
            ServerTime = DateTime.UtcNow,
            Items = order.OrderDetails.Select(od => new OrderDetailItemDto
            {
                BookId = od.BookId,
                BookTitle = od.Book?.Title ?? string.Empty,
                Quantity = od.Quantity,
                UnitPriceAtTime = od.UnitPriceAtTime,
                LineTotal = od.LineTotal
            }).ToList(),
            Payments = order.PaymentTransactions.Select(pt => new OrderPaymentTransactionDto
            {
                TransactionId = pt.TransactionId,
                ReferenceCode = pt.ReferenceCode,
                Amount = pt.Amount,
                Gateway = pt.Gateway,
                CreatedAt = pt.CreatedAt
            }).ToList()
        };
    }

    public async Task<PaginatedResultDto<OrderListItemDto>> GetOrdersAsync(OrderFilterDto filter)
    {
        var keyword = !string.IsNullOrWhiteSpace(filter.Keyword) ? filter.Keyword : filter.OrderCode;

        var (orders, totalCount) = await _orderRepository.GetOrdersPagedAsync(
            filter.FromDate,
            filter.ToDate,
            filter.Status,
            filter.Channel,
            filter.PaymentMethod,
            keyword,
            filter.NeedsSupportOnly,
            Math.Max(1, filter.Page),
            Math.Clamp(filter.PageSize, 1, 100));

        var items = orders.Select(o => new OrderListItemDto
        {
            OrderId = o.OrderId,
            OrderCode = o.OrderCode,
            CreatedAt = o.CreatedAt,
            CompletedAt = o.CompletedAt,
            SaleChannel = o.SaleChannel,
            OrderStatus = o.OrderStatus,
            PaymentMethod = o.PaymentMethod,
            SubTotal = o.SubTotal,
            DiscountAmount = o.DiscountAmount,
            PointsUsed = o.PointsUsed,
            TotalAmount = o.TotalAmount,
            CashierName = o.CreatedByUser?.FullName,
            HasMember = o.MemberId.HasValue,
            NeedsSupport = o.OrderStatus != OrderStatus.Paid && o.PaymentTransactions.Any(pt => pt.Amount > 0)
        }).ToList();

        return new PaginatedResultDto<OrderListItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    private static PosReceiptDto BuildPosReceipt(Order order)
    {
        return new PosReceiptDto
        {
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt ?? DateTime.UtcNow,
            CashierName = order.CreatedByUser?.FullName ?? "Thu ngân",
            MemberDisplayName = !string.IsNullOrWhiteSpace(order.Member?.FullName) ? order.Member.FullName : "Thành viên",
            SubTotal = order.SubTotal,
            DiscountAmount = order.DiscountAmount,
            PointsUsed = order.PointsUsed,
            PointsUsedAmount = order.PointsUsed * 1000m,
            TotalAmount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod.ToString(),
            Items = order.OrderDetails.Select(od => new PosReceiptItemDto
            {
                BookId = od.BookId,
                Title = od.Book?.Title ?? string.Empty,
                Quantity = od.Quantity,
                UnitPrice = od.UnitPriceAtTime,
                LineTotal = od.LineTotal
            }).ToList()
        };
    }
}

