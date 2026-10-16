using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using BookKiosk.CMS.Models.Api.Orders;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Services
{
    /// <summary>
    /// MockOrderApi: Triển khai giả lập IOrderApi theo chuẩn Gate H5.
    /// Cung cấp dữ liệu thử nghiệm ngoại tuyến bao gồm:
    /// - Đơn hàng Kiosk & Quầy (Paid, Pending, Cancelled)
    /// - Kịch bản Đơn hàng Cần hỗ trợ (Lệch tiền chuyển khoản / Pending & Cancelled có giao dịch SePay)
    /// </summary>
    public class MockOrderApi : IOrderApi
    {
        private readonly ConcurrentDictionary<int, OrderDetailDto> _orders = new();
        private readonly IConfiguration _configuration;

        public MockOrderApi(IConfiguration configuration)
        {
            _configuration = configuration;
            SeedData();
        }

        private void SeedData()
        {
            var baseDate = DateTime.UtcNow.Date;

            var mockList = new List<OrderDetailDto>
            {
                // 1. Đơn Kiosk - Paid thành công (Chuẩn)
                new OrderDetailDto
                {
                    OrderId = 1001,
                    OrderCode = "ORD-20261101-0101",
                    SaleChannel = SaleChannel.Kiosk,
                    OrderStatus = OrderStatus.Paid,
                    PaymentMethod = PaymentMethod.QR,
                    SubTotal = 150000,
                    DiscountAmount = 0,
                    PointsUsed = 0,
                    PointsUsedAmount = 0,
                    TotalAmount = 150000,
                    CashierName = "Kiosk 01 (Tự phục vụ)",
                    MemberName = "Trần Thị Bích",
                    MemberPhone = "0981110002",
                    PointsEarned = 15,
                    CreatedAt = baseDate.AddHours(8).AddMinutes(15),
                    CompletedAt = baseDate.AddHours(8).AddMinutes(16),
                    Items = new List<OrderDetailItemDto>
                    {
                        new() { BookId = 9301, BookTitle = "Dế Mèn Phiêu Lưu Ký", Quantity = 1, UnitPriceAtTime = 50000, LineTotal = 50000 },
                        new() { BookId = 9305, BookTitle = "Nhà Giả Kim", Quantity = 1, UnitPriceAtTime = 90000, LineTotal = 90000 }
                    },
                    Payments = new List<OrderPaymentTransactionDto>
                    {
                        new() { TransactionId = 5001, ReferenceCode = "VCB.881923", Gateway = "Vietcombank (SePay)", Amount = 150000, CreatedAt = baseDate.AddHours(8).AddMinutes(16) }
                    }
                },

                // 2. Đơn Quầy - Paid Tiền mặt (Chuẩn)
                new OrderDetailDto
                {
                    OrderId = 1002,
                    OrderCode = "ORD-20261101-0102",
                    SaleChannel = SaleChannel.Counter,
                    OrderStatus = OrderStatus.Paid,
                    PaymentMethod = PaymentMethod.Cash,
                    SubTotal = 200000,
                    DiscountAmount = 30000,
                    PromotionName = "KM Giảm 30K đơn từ 200K",
                    PointsUsed = 20,
                    PointsUsedAmount = 20000,
                    TotalAmount = 150000,
                    CashierName = "Thu ngân 01",
                    MemberName = "Nguyễn Văn An",
                    MemberPhone = "0981110001",
                    PointsEarned = 15,
                    CreatedAt = baseDate.AddHours(9).AddMinutes(30),
                    CompletedAt = baseDate.AddHours(9).AddMinutes(32),
                    Items = new List<OrderDetailItemDto>
                    {
                        new() { BookId = 9304, BookTitle = "Đắc Nhân Tâm", Quantity = 2, UnitPriceAtTime = 85000, LineTotal = 170000 },
                        new() { BookId = 9303, BookTitle = "Số Đỏ", Quantity = 1, UnitPriceAtTime = 60000, LineTotal = 60000 }
                    },
                    Payments = new List<OrderPaymentTransactionDto>()
                },

                // 3. Đơn Kiosk - LỆCH TIỀN (Cần hỗ trợ 1: Khách chuyển thiếu tiền, đơn Pending)
                new OrderDetailDto
                {
                    OrderId = 1003,
                    OrderCode = "ORD-20261101-0105",
                    SaleChannel = SaleChannel.Kiosk,
                    OrderStatus = OrderStatus.Pending,
                    PaymentMethod = PaymentMethod.QR,
                    SubTotal = 215000,
                    DiscountAmount = 0,
                    PointsUsed = 0,
                    PointsUsedAmount = 0,
                    TotalAmount = 215000,
                    CashierName = "Kiosk 02",
                    MemberName = null,
                    MemberPhone = null,
                    PointsEarned = null,
                    CreatedAt = baseDate.AddHours(10).AddMinutes(5),
                    CompletedAt = null,
                    Items = new List<OrderDetailItemDto>
                    {
                        new() { BookId = 9302, BookTitle = "Đất Rừng Phương Nam", Quantity = 1, UnitPriceAtTime = 75000, LineTotal = 75000 },
                        new() { BookId = 9305, BookTitle = "Nhà Giả Kim", Quantity = 1, UnitPriceAtTime = 90000, LineTotal = 90000 },
                        new() { BookId = 9301, BookTitle = "Dế Mèn Phiêu Lưu Ký", Quantity = 1, UnitPriceAtTime = 50000, LineTotal = 50000 }
                    },
                    // Khách tự sửa số tiền chuyển khoản thành 200,000 ₫ (Thiếu 15,000 ₫)
                    Payments = new List<OrderPaymentTransactionDto>
                    {
                        new() { TransactionId = 5002, ReferenceCode = "MB.774411", Gateway = "MB Bank (SePay)", Amount = 200000, CreatedAt = baseDate.AddHours(10).AddMinutes(7) }
                    }
                },

                // 4. Đơn Kiosk - LỆCH TIỀN ĐÃ BỊ WORKER 4 PHÚT HỦY (Cần hỗ trợ 2: Khách chuyển tiền nhưng worker hủy đơn)
                new OrderDetailDto
                {
                    OrderId = 1004,
                    OrderCode = "ORD-20261101-0108",
                    SaleChannel = SaleChannel.Kiosk,
                    OrderStatus = OrderStatus.Cancelled,
                    PaymentMethod = PaymentMethod.QR,
                    SubTotal = 350000,
                    DiscountAmount = 50000,
                    PromotionName = "KM Khủng Giảm 50K",
                    PointsUsed = 0,
                    PointsUsedAmount = 0,
                    TotalAmount = 300000,
                    CashierName = "Kiosk 01",
                    MemberName = "Lê Hoàng Cường",
                    MemberPhone = "0981110003",
                    PointsEarned = null,
                    CreatedAt = baseDate.AddHours(11).AddMinutes(0),
                    CompletedAt = null,
                    Items = new List<OrderDetailItemDto>
                    {
                        new() { BookId = 9304, BookTitle = "Đắc Nhân Tâm", Quantity = 3, UnitPriceAtTime = 85000, LineTotal = 255000 },
                        new() { BookId = 9305, BookTitle = "Nhà Giả Kim", Quantity = 1, UnitPriceAtTime = 95000, LineTotal = 95000 }
                    },
                    // Đã có tiền vào 250,000 ₫ (lệch -50,000 ₫), sau 4 phút worker hủy đơn thành Cancelled
                    Payments = new List<OrderPaymentTransactionDto>
                    {
                        new() { TransactionId = 5003, ReferenceCode = "TCB.992211", Gateway = "Techcombank (SePay)", Amount = 250000, CreatedAt = baseDate.AddHours(11).AddMinutes(2) }
                    }
                },

                // 5. Đơn Quầy - Pending (Khách đang đếm tiền, chưa có thanh toán)
                new OrderDetailDto
                {
                    OrderId = 1005,
                    OrderCode = "ORD-20261101-0109",
                    SaleChannel = SaleChannel.Counter,
                    OrderStatus = OrderStatus.Pending,
                    PaymentMethod = PaymentMethod.Cash,
                    SubTotal = 75000,
                    DiscountAmount = 0,
                    PointsUsed = 0,
                    PointsUsedAmount = 0,
                    TotalAmount = 75000,
                    CashierName = "Thu ngân 02",
                    CreatedAt = baseDate.AddHours(14).AddMinutes(20),
                    CompletedAt = null,
                    Items = new List<OrderDetailItemDto>
                    {
                        new() { BookId = 9302, BookTitle = "Đất Rừng Phương Nam", Quantity = 1, UnitPriceAtTime = 75000, LineTotal = 75000 }
                    },
                    Payments = new List<OrderPaymentTransactionDto>()
                },

                // 6. Đơn Quầy - Cancelled (Khách đổi ý hủy tại quầy)
                new OrderDetailDto
                {
                    OrderId = 1006,
                    OrderCode = "ORD-20261101-0110",
                    SaleChannel = SaleChannel.Counter,
                    OrderStatus = OrderStatus.Cancelled,
                    PaymentMethod = PaymentMethod.Cash,
                    SubTotal = 135000,
                    DiscountAmount = 0,
                    PointsUsed = 0,
                    PointsUsedAmount = 0,
                    TotalAmount = 135000,
                    CashierName = "Thu ngân 01",
                    CreatedAt = baseDate.AddHours(15).AddMinutes(10),
                    CompletedAt = null,
                    Items = new List<OrderDetailItemDto>
                    {
                        new() { BookId = 9301, BookTitle = "Dế Mèn Phiêu Lưu Ký", Quantity = 1, UnitPriceAtTime = 50000, LineTotal = 50000 },
                        new() { BookId = 9304, BookTitle = "Đắc Nhân Tâm", Quantity = 1, UnitPriceAtTime = 85000, LineTotal = 85000 }
                    },
                    Payments = new List<OrderPaymentTransactionDto>()
                }
            };

            foreach (var order in mockList)
            {
                _orders[order.OrderId] = order;
            }
        }

        public async Task<ApiResult<PaginatedResult<OrderListItemDto>>> GetOrdersAsync(OrderFilterDto filter)
        {
            await Task.Delay(100); // Giả lập độ trễ mạng nhẹ

            var query = _orders.Values.AsEnumerable();

            // 1. Lọc tab "Cần hỗ trợ": Đơn Pending hoặc Cancelled nhưng đã có tiền vào mà không hoàn tất Paid
            if (filter.NeedsSupportOnly.HasValue && filter.NeedsSupportOnly.Value)
            {
                query = query.Where(o => o.IsPaymentMismatch);
            }

            // 2. Tìm kiếm chuẩn: Chỉ tìm theo mã đơn hàng ORD-yyyyMMdd-XXXX
            if (!string.IsNullOrWhiteSpace(filter.OrderCode))
            {
                var code = filter.OrderCode.Trim().ToLower();
                query = query.Where(o => o.OrderCode.ToLower().Contains(code));
            }

            // 3. Lọc theo trạng thái đơn
            if (filter.Status.HasValue)
            {
                query = query.Where(o => o.OrderStatus == filter.Status.Value);
            }

            // 4. Lọc theo kênh bán
            if (filter.Channel.HasValue)
            {
                query = query.Where(o => o.SaleChannel == filter.Channel.Value);
            }

            // 5. Lọc theo phương thức thanh toán
            if (filter.PaymentMethod.HasValue)
            {
                query = query.Where(o => o.PaymentMethod == filter.PaymentMethod.Value);
            }

            // 6. Lọc khoảng ngày tạo
            if (filter.FromDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt >= filter.FromDate.Value);
            }
            if (filter.ToDate.HasValue)
            {
                query = query.Where(o => o.CreatedAt <= filter.ToDate.Value);
            }

            var allList = query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new OrderListItemDto
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
                    CashierName = o.CashierName,
                    HasMember = !string.IsNullOrWhiteSpace(o.MemberName),
                    NeedsSupport = o.IsPaymentMismatch
                })
                .ToList();

            var totalCount = allList.Count;
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var items = allList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var paginated = new PaginatedResult<OrderListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return ApiResult<PaginatedResult<OrderListItemDto>>.Ok(paginated);
        }

        public async Task<ApiResult<OrderDetailDto>> GetOrderByIdAsync(int orderId)
        {
            await Task.Delay(80);

            if (_orders.TryGetValue(orderId, out var order))
            {
                return ApiResult<OrderDetailDto>.Ok(order);
            }

            return ApiResult<OrderDetailDto>.Fail($"Không tìm thấy đơn hàng #{orderId}.", 404);
        }

        public async Task<ApiResult<PosReceiptDto>> GetOrderReceiptAsync(int orderId)
        {
            await Task.Delay(80);

            if (!_orders.TryGetValue(orderId, out var order))
            {
                return ApiResult<PosReceiptDto>.Fail($"Không tìm thấy đơn hàng #{orderId}.", 404);
            }

            if (order.OrderStatus != OrderStatus.Paid)
            {
                return ApiResult<PosReceiptDto>.Fail(
                    $"Chỉ có thể in lại hóa đơn cho đơn hàng đã thanh toán thành công (Paid). Trạng thái hiện tại: {order.OrderStatus}",
                    409);
            }

            var showMemberName = _configuration.GetValue<bool>("Pos:ShowMemberNameOnReceipt", false);
            var memberDisplayName = "Thành viên";
            if (showMemberName && !string.IsNullOrWhiteSpace(order.MemberName))
            {
                memberDisplayName = order.MemberName;
            }

            var receipt = new PosReceiptDto
            {
                OrderCode = order.OrderCode,
                CreatedAt = order.CreatedAt,
                CompletedAt = order.CompletedAt ?? order.CreatedAt,
                CashierName = order.CashierName ?? "Thu ngân",
                MemberDisplayName = memberDisplayName,
                PointsEarned = order.PointsEarned,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                PointsUsed = order.PointsUsed,
                PointsUsedAmount = order.PointsUsedAmount,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod.ToString(),
                StoreName = _configuration.GetValue<string>("Pos:StoreInfo:Name") ?? "NHÀ SÁCH BOOKKIOSK",
                StoreAddress = _configuration.GetValue<string>("Pos:StoreInfo:Address") ?? "Điểm bán lẻ Kiosk & Quầy thu ngân",
                StoreHotline = _configuration.GetValue<string>("Pos:StoreInfo:Hotline") ?? "1900 6868",
                StoreWebsite = _configuration.GetValue<string>("Pos:StoreInfo:Website") ?? "bookkiosk.vn",
                Items = order.Items.Select(i => new PosReceiptItemDto
                {
                    BookId = i.BookId,
                    Title = i.BookTitle,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPriceAtTime,
                    LineTotal = i.LineTotal
                }).ToList()
            };

            return ApiResult<PosReceiptDto>.Ok(receipt);
        }

        public async Task<ApiResult<int>> GetNeedsSupportCountAsync()
        {
            await Task.Delay(30);
            var count = _orders.Values.Count(o => o.IsPaymentMismatch);
            return ApiResult<int>.Ok(count);
        }
    }
}
