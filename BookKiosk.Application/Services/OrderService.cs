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

            Promotion? selectedPromo = null;
            decimal selectedBenefit = 0m;
            Dictionary<int, decimal> selectedBookDiscounts = new();

            var candidatePromotions = new List<(Promotion Promo, decimal Benefit, Dictionary<int, decimal> Discounts)>();
            var cartItems = sortedItems.Select(i => (i.BookId, i.Quantity)).ToList();

            foreach (var promo in activePromotions)
            {
                var (benefit, discounts) = CalculatePromotionBenefit(promo, subTotal, books, cartItems);
                if (benefit > 0)
                {
                    candidatePromotions.Add((promo, benefit, discounts));
                }
            }

            if (candidatePromotions.Count > 0)
            {
                var bestCandidate = candidatePromotions
                    .OrderByDescending(c => c.Benefit)
                    .ThenBy(c => c.Promo.PromotionId)
                    .First();

                selectedPromo = bestCandidate.Promo;
                selectedBenefit = bestCandidate.Benefit;
                selectedBookDiscounts = bestCandidate.Discounts;
            }

            decimal totalAmountWithoutPoints = subTotal - selectedBenefit;
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

            // Zero-payable check: TotalAmount <= 0 ném ArgumentException để trả HTTP 400 BAD_REQUEST và rollback kho/điểm
            if (totalAmount <= 0)
            {
                throw new ArgumentException("Tổng tiền thanh toán của đơn hàng sau giảm giá phải lớn hơn 0.");
            }

            var tempOrderCode = $"ORD-{DateTime.UtcNow:yyyyMMdd}-TEMP-{Guid.NewGuid():N}";
            var order = new Order
            {
                OrderCode = tempOrderCode,
                SaleChannel = SaleChannel.Kiosk,
                OrderStatus = OrderStatus.Pending,
                PaymentMethod = PaymentMethod.QR,
                MemberId = request.MemberId,
                PromotionId = selectedPromo?.PromotionId,
                PromotionNameAtTime = selectedPromo?.Name,
                PromotionTypeAtTime = selectedPromo?.PromotionType,
                SubTotal = subTotal,
                DiscountAmount = selectedBenefit,
                PointsUsed = request.PointsToUse,
                PointsUsedAmountAtTime = pointsUsedAmount,
                TotalAmount = totalAmount,
            };

            foreach (var item in sortedItems)
            {
                var book = books[item.BookId];
                decimal discountPerUnit = selectedBookDiscounts.TryGetValue(item.BookId, out var disc) ? disc : 0m;
                order.OrderDetails.Add(new OrderDetail
                {
                    BookId = item.BookId,
                    BookTitleAtTime = book.Title,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    DiscountPerUnitAtTime = discountPerUnit,
                    LineTotal = book.SellingPrice * item.Quantity
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

            var checkoutItems = sortedItems.Select(item =>
            {
                var book = books[item.BookId];
                decimal discountPerUnit = selectedBookDiscounts.TryGetValue(item.BookId, out var disc) ? disc : 0m;
                decimal lineTotal = book.SellingPrice * item.Quantity;
                decimal lineDiscountAmount = discountPerUnit * item.Quantity;
                decimal netLineTotal = lineTotal - lineDiscountAmount;
                return new CheckoutItemDiscountDto
                {
                    BookId = item.BookId,
                    BookTitle = book.Title,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    DiscountPerUnit = discountPerUnit,
                    LineTotal = lineTotal,
                    LineDiscountAmount = lineDiscountAmount,
                    NetLineTotal = netLineTotal
                };
            }).ToList();

            return new CheckoutResponseDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                SubTotal = subTotal,
                PromotionId = order.PromotionId,
                PromotionName = order.PromotionNameAtTime,
                PromotionType = order.PromotionTypeAtTime,
                DiscountAmount = order.DiscountAmount,
                PointsUsedAmount = pointsUsedAmount,
                TotalAmount = totalAmount,
                SepayQrCodeUrl = sepayQrCodeUrl,
                Items = checkoutItems
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

        string? promotionName = null;
        PromotionType? promotionType = null;

        if (!string.IsNullOrWhiteSpace(order.PromotionNameAtTime))
        {
            promotionName = order.PromotionNameAtTime;
            promotionType = order.PromotionTypeAtTime;
        }
        else if (order.PromotionId.HasValue || order.DiscountAmount > 0)
        {
            promotionName = order.Promotion?.Name ?? "Khuyến mãi";
            promotionType = order.Promotion?.PromotionType;
        }

        decimal pointsUsedAmount = order.PointsUsedAmountAtTime ?? (order.PointsUsed * 1000m);

        return new ReceiptDto
        {
            OrderId = order.OrderId,
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt.Value,
            SubTotal = order.SubTotal,
            PromotionId = order.PromotionId,
            PromotionName = promotionName,
            PromotionType = promotionType,
            DiscountAmount = order.DiscountAmount,
            PointsUsed = order.PointsUsed,
            PointsUsedAmount = pointsUsedAmount,
            TotalAmount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod,
            Items = order.OrderDetails.Select(od =>
            {
                var bookTitle = !string.IsNullOrWhiteSpace(od.BookTitleAtTime)
                    ? od.BookTitleAtTime
                    : (od.Book?.Title ?? $"Book #{od.BookId}");

                var lineDiscount = od.DiscountPerUnitAtTime * od.Quantity;
                return new ReceiptLineDto
                {
                    BookId = od.BookId,
                    BookTitle = bookTitle,
                    Quantity = od.Quantity,
                    UnitPriceAtTime = od.UnitPriceAtTime,
                    DiscountPerUnit = od.DiscountPerUnitAtTime,
                    LineTotal = od.LineTotal,
                    LineDiscountAmount = lineDiscount,
                    NetLineTotal = od.LineTotal - lineDiscount
                };
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
            }

            Promotion? appliedPromo = null;
            decimal appliedDiscountAmount = 0m;
            Dictionary<int, decimal> appliedBookDiscounts = new();

            var cartItems = sortedItems.Select(i => (i.BookId, i.Quantity)).ToList();

            if (request.PromotionId.HasValue && request.PromotionId.Value > 0)
            {
                var promo = await _promotionRepository.GetByIdAsync(request.PromotionId.Value);
                if (promo == null || !promo.IsActive)
                {
                    throw new ArgumentException("Chương trình khuyến mãi không tồn tại hoặc đã ngừng hoạt động.");
                }

                var now = DateTime.UtcNow;
                if (promo.StartDate > now || promo.EndDate < now)
                {
                    throw new ArgumentException("Chương trình khuyến mãi đã hết hạn hoặc chưa tới thời gian áp dụng.");
                }

                var (promoBenefit, bookDiscounts) = CalculatePromotionBenefit(promo, subTotal, books, cartItems);
                if (promoBenefit <= 0)
                {
                    throw new ArgumentException("Đơn hàng không đủ điều kiện áp dụng chương trình khuyến mãi đã chọn.");
                }

                appliedPromo = promo;
                appliedDiscountAmount = promoBenefit;
                appliedBookDiscounts = bookDiscounts;
            }

            decimal totalAmountWithoutPoints = subTotal - appliedDiscountAmount;
            if (totalAmountWithoutPoints < 0) totalAmountWithoutPoints = 0;

            decimal pointsUsedAmount = 0m;
            if (request.PointsToUse > 0 && request.MemberId.HasValue)
            {
                var member = await _memberRepository.GetByIdAsync(request.MemberId.Value);
                if (member == null)
                    throw new System.Collections.Generic.KeyNotFoundException("Không tìm thấy thông tin thành viên.");

                if (member.Points < request.PointsToUse)
                    throw new InvalidOperationException("Thành viên không đủ số điểm để sử dụng.");

                decimal requestedPointsDiscount = request.PointsToUse * 1000m;
                if (requestedPointsDiscount > totalAmountWithoutPoints)
                    throw new InvalidOperationException("Số điểm sử dụng vượt quá giá trị thanh toán của đơn hàng.");

                var deducted = await _memberRepository.TryDeductPointsAsync(member.MemberId, request.PointsToUse);
                if (!deducted)
                    throw new InvalidOperationException("Thành viên không đủ số điểm để sử dụng.");

                pointsUsedAmount = requestedPointsDiscount;
            }
            else
            {
                request.PointsToUse = 0;
            }

            decimal totalAmount = totalAmountWithoutPoints - pointsUsedAmount;

            // Zero-payable check: TotalAmount <= 0 ném ArgumentException để trả HTTP 400 BAD_REQUEST và rollback kho/điểm
            if (totalAmount <= 0)
            {
                throw new ArgumentException("Tổng tiền thanh toán của đơn hàng sau giảm giá phải lớn hơn 0.");
            }

            var order = new Order
            {
                SaleChannel = SaleChannel.Counter,
                OrderStatus = OrderStatus.Pending,
                PaymentMethod = paymentMethod,
                SubTotal = subTotal,
                PromotionId = appliedPromo?.PromotionId,
                PromotionNameAtTime = appliedPromo?.Name,
                PromotionTypeAtTime = appliedPromo?.PromotionType,
                DiscountAmount = appliedDiscountAmount,
                PointsUsed = request.PointsToUse,
                PointsUsedAmountAtTime = pointsUsedAmount,
                TotalAmount = totalAmount,
                MemberId = request.MemberId,
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

            var orderItemDtos = new List<CounterOrderItemDto>();

            foreach (var item in sortedItems)
            {
                var book = books[item.BookId];
                decimal discountPerUnit = appliedBookDiscounts.TryGetValue(item.BookId, out var disc) ? disc : 0m;
                decimal lineTotal = book.SellingPrice * item.Quantity;
                decimal lineDiscount = discountPerUnit * item.Quantity;
                decimal netLine = lineTotal - lineDiscount;

                order.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    BookId = item.BookId,
                    BookTitleAtTime = book.Title,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    DiscountPerUnitAtTime = discountPerUnit,
                    LineTotal = lineTotal
                });

                orderItemDtos.Add(new CounterOrderItemDto
                {
                    BookId = item.BookId,
                    Title = book.Title,
                    BookTitle = book.Title,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    DiscountPerUnit = discountPerUnit,
                    LineTotal = lineTotal,
                    LineDiscountAmount = lineDiscount,
                    NetLineTotal = netLine
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
                PromotionId = order.PromotionId,
                PromotionName = order.PromotionNameAtTime,
                PromotionType = order.PromotionTypeAtTime,
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

        string? promotionName = null;
        if (!string.IsNullOrWhiteSpace(order.PromotionNameAtTime))
        {
            promotionName = order.PromotionNameAtTime;
        }
        else if (order.PromotionId.HasValue || order.DiscountAmount > 0)
        {
            promotionName = order.Promotion?.Name ?? "Khuyến mãi";
        }

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
            PromotionName = promotionName,
            PointsUsed = order.PointsUsed,
            PointsUsedAmount = order.PointsUsedAmountAtTime ?? (order.PointsUsed * 1000m),
            TotalAmount = order.TotalAmount,
            CashierName = order.CreatedByUser?.FullName,
            MemberName = order.Member?.FullName,
            MemberPhone = order.Member?.PhoneNumber,
            ServerTime = DateTime.UtcNow,
            Items = order.OrderDetails.Select(od => new OrderDetailItemDto
            {
                BookId = od.BookId,
                BookTitle = !string.IsNullOrWhiteSpace(od.BookTitleAtTime) ? od.BookTitleAtTime : (od.Book?.Title ?? $"Book #{od.BookId}"),
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
        string? promotionName = null;
        PromotionType? promotionType = null;

        if (!string.IsNullOrWhiteSpace(order.PromotionNameAtTime))
        {
            promotionName = order.PromotionNameAtTime;
            promotionType = order.PromotionTypeAtTime;
        }
        else if (order.PromotionId.HasValue || order.DiscountAmount > 0)
        {
            promotionName = order.Promotion?.Name ?? "Khuyến mãi";
            promotionType = order.Promotion?.PromotionType;
        }

        decimal pointsUsedAmount = order.PointsUsedAmountAtTime ?? (order.PointsUsed * 1000m);

        return new PosReceiptDto
        {
            OrderCode = order.OrderCode,
            CreatedAt = order.CreatedAt,
            CompletedAt = order.CompletedAt ?? DateTime.UtcNow,
            CashierName = order.CreatedByUser?.FullName ?? "Thu ngân",
            MemberDisplayName = !string.IsNullOrWhiteSpace(order.Member?.FullName) ? order.Member.FullName : "Thành viên",
            SubTotal = order.SubTotal,
            PromotionId = order.PromotionId,
            PromotionName = promotionName,
            PromotionType = promotionType,
            DiscountAmount = order.DiscountAmount,
            PointsUsed = order.PointsUsed,
            PointsUsedAmount = pointsUsedAmount,
            TotalAmount = order.TotalAmount,
            PaymentMethod = order.PaymentMethod.ToString(),
            Items = order.OrderDetails.Select(od =>
            {
                var bookTitle = !string.IsNullOrWhiteSpace(od.BookTitleAtTime)
                    ? od.BookTitleAtTime
                    : (od.Book?.Title ?? $"Book #{od.BookId}");

                var lineDiscount = od.DiscountPerUnitAtTime * od.Quantity;
                return new PosReceiptItemDto
                {
                    BookId = od.BookId,
                    Title = bookTitle,
                    BookTitle = bookTitle,
                    Quantity = od.Quantity,
                    UnitPrice = od.UnitPriceAtTime,
                    UnitPriceAtTime = od.UnitPriceAtTime,
                    DiscountPerUnit = od.DiscountPerUnitAtTime,
                    LineTotal = od.LineTotal,
                    LineDiscountAmount = lineDiscount,
                    NetLineTotal = od.LineTotal - lineDiscount
                };
            }).ToList()
        };
    }

    private static (decimal TotalBenefit, Dictionary<int, decimal> BookDiscountsPerUnit) CalculatePromotionBenefit(
        Promotion promo,
        decimal subTotal,
        Dictionary<int, Book> books,
        IEnumerable<(int BookId, int Quantity)> items)
    {
        var bookDiscounts = new Dictionary<int, decimal>();
        if (!promo.IsActive)
        {
            return (0m, bookDiscounts);
        }

        var now = DateTime.UtcNow;
        if (promo.StartDate > now || promo.EndDate < now)
        {
            return (0m, bookDiscounts);
        }

        if (promo.PromotionType == PromotionType.OrderDiscount)
        {
            if (promo.OrderDiscount != null && subTotal >= promo.OrderDiscount.MinOrderValue)
            {
                decimal benefit = Math.Min(promo.OrderDiscount.DiscountAmount, subTotal);
                return (benefit, bookDiscounts);
            }
            return (0m, bookDiscounts);
        }
        else if (promo.PromotionType == PromotionType.ProductDiscount)
        {
            decimal totalBenefit = 0m;
            var promoProductDiscounts = promo.ProductDiscounts.ToDictionary(pd => pd.BookId, pd => pd.DiscountAmount);

            foreach (var (bookId, quantity) in items)
            {
                if (promoProductDiscounts.TryGetValue(bookId, out var configuredDiscount))
                {
                    if (books.TryGetValue(bookId, out var book))
                    {
                        decimal discountPerUnit = Math.Min(configuredDiscount, book.SellingPrice);
                        if (discountPerUnit > 0)
                        {
                            bookDiscounts[bookId] = discountPerUnit;
                            totalBenefit += discountPerUnit * quantity;
                        }
                    }
                }
            }
            return (totalBenefit, bookDiscounts);
        }

        return (0m, bookDiscounts);
    }
}

