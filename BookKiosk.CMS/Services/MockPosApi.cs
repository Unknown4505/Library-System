using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BookKiosk.CMS.Models.Api.Pos;
using BookKiosk.CMS.Models.Common;

namespace BookKiosk.CMS.Services
{
    /// <summary>
    /// MockPosApi: Triển khai giả lập IPosApi theo chiến lược Mock-First (Gate G5).
    /// Đáp ứng 100% hợp đồng API và 6 kịch bản kiểm thử quy định trong docs/11-end-to-end-test-plan.
    /// Thin Client: Toàn bộ tính toán tiền giảm, tiền điểm, thành tiền và trừ kho được thực thi tập trung tại đây.
    /// </summary>
    public class MockPosApi : IPosApi
    {
        // 1. Kho dữ liệu bộ nhớ (In-memory Thread-safe)
        private readonly ConcurrentDictionary<int, PosBookDto> _books = new();
        private readonly ConcurrentDictionary<string, int> _barcodeMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, PosMemberDto> _members = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PosCategoryDto> _categories = new();
        private readonly List<PosPromotionDto> _promotions = new();
        private readonly ConcurrentDictionary<int, MockOrderEntity> _orders = new();
        private int _orderIdCounter = 1000;
        private int _memberIdCounter = 9500;

        public MockPosApi()
        {
            SeedData();
        }

        private void SeedData()
        {
            // --- Danh mục sách ---
            _categories.AddRange(new[]
            {
                new PosCategoryDto { CategoryId = 9001, Name = "Văn Học & Tiểu Thuyết", Description = "Tác phẩm văn học kinh điển và hiện đại" },
                new PosCategoryDto { CategoryId = 9002, Name = "Kinh Tế & Khởi Nghiệp", Description = "Sách quản trị kinh doanh và tư duy tài chính" },
                new PosCategoryDto { CategoryId = 9003, Name = "Sách Thiếu Nhi", Description = "Truyện tranh, cổ tích và khám phá khoa học" },
                new PosCategoryDto { CategoryId = 9004, Name = "Kỹ Năng Sống", Description = "Sách phát triển bản thân và kỹ năng giao tiếp" }
            });

            // --- Sách & Mã vạch (Bao gồm dữ liệu test E2E-RUN01) ---
            var bookList = new[]
            {
                new PosBookDto
                {
                    BookId = 9301,
                    Barcode = "8930000001",
                    Title = "Dế Mèn Phiêu Lưu Ký",
                    Author = "Tô Hoài",
                    Publisher = "NXB Kim Đồng",
                    SellingPrice = 50000,
                    StockQuantity = 25,
                    AvailableStock = 25,
                    IsActive = true,
                    CategoryId = 9003,
                    CategoryName = "Sách Thiếu Nhi"
                },
                new PosBookDto
                {
                    BookId = 9302,
                    Barcode = "8930000002",
                    Title = "Đất Rừng Phương Nam",
                    Author = "Đoàn Giỏi",
                    Publisher = "NXB Kim Đồng",
                    SellingPrice = 75000,
                    StockQuantity = 15,
                    AvailableStock = 15,
                    IsActive = true,
                    CategoryId = 9003,
                    CategoryName = "Sách Thiếu Nhi"
                },
                new PosBookDto
                {
                    BookId = 9303,
                    Barcode = "8930000003",
                    Title = "Số Đỏ",
                    Author = "Vũ Trọng Phụng",
                    Publisher = "NXB Văn Học",
                    SellingPrice = 60000,
                    StockQuantity = 10,
                    AvailableStock = 10,
                    IsActive = true,
                    CategoryId = 9001,
                    CategoryName = "Văn Học & Tiểu Thuyết"
                },
                new PosBookDto
                {
                    BookId = 9304,
                    Barcode = "8930000004",
                    Title = "Đắc Nhân Tâm",
                    Author = "Dale Carnegie",
                    Publisher = "NXB Trẻ",
                    SellingPrice = 85000,
                    StockQuantity = 30,
                    AvailableStock = 30,
                    IsActive = true,
                    CategoryId = 9004,
                    CategoryName = "Kỹ Năng Sống"
                },
                new PosBookDto
                {
                    BookId = 9305,
                    Barcode = "8930000005",
                    Title = "Nhà Giả Kim",
                    Author = "Paulo Coelho",
                    Publisher = "NXB Hội Nhà Văn",
                    SellingPrice = 90000,
                    StockQuantity = 12,
                    AvailableStock = 12,
                    IsActive = true,
                    CategoryId = 9001,
                    CategoryName = "Văn Học & Tiểu Thuyết"
                },
                // Cuốn test Kịch bản 4: Hết hàng trong kho
                new PosBookDto
                {
                    BookId = 9399,
                    Barcode = "8930000099",
                    Title = "Giáo Trình Triết Học (Sách Hết Hàng)",
                    Author = "Nhiều Tác Giả",
                    Publisher = "NXB Chính Trị",
                    SellingPrice = 120000,
                    StockQuantity = 0,
                    AvailableStock = 0,
                    IsActive = true,
                    CategoryId = 9002,
                    CategoryName = "Kinh Tế & Khởi Nghiệp"
                }
            };

            foreach (var b in bookList)
            {
                _books[b.BookId] = b;
                _barcodeMap[b.Barcode] = b.BookId;
            }

            // --- Khuyến mãi hoạt động ---
            _promotions.AddRange(new[]
            {
                new PosPromotionDto
                {
                    PromotionId = 9201,
                    Name = "KM Giảm 30K đơn từ 200K",
                    Description = "Giảm trực tiếp 30.000 ₫ cho hóa đơn từ 200.000 ₫",
                    PromotionType = BookKiosk.Domain.Enums.PromotionType.OrderDiscount,
                    OrderDiscount = new PosPromotionOrderDiscountDto { MinOrderValue = 200000, DiscountAmount = 30000 },
                    MinOrderValue = 200000,
                    DiscountAmount = 30000,
                    StartDate = DateTime.UtcNow.AddDays(-10),
                    EndDate = DateTime.UtcNow.AddDays(30),
                    IsActive = true
                },
                new PosPromotionDto
                {
                    PromotionId = 9202,
                    Name = "KM Khủng Giảm 50K đơn từ 350K",
                    Description = "Giảm trực tiếp 50.000 ₫ cho hóa đơn từ 350.000 ₫",
                    PromotionType = BookKiosk.Domain.Enums.PromotionType.OrderDiscount,
                    OrderDiscount = new PosPromotionOrderDiscountDto { MinOrderValue = 350000, DiscountAmount = 50000 },
                    MinOrderValue = 350000,
                    DiscountAmount = 50000,
                    StartDate = DateTime.UtcNow.AddDays(-5),
                    EndDate = DateTime.UtcNow.AddDays(25),
                    IsActive = true
                },
                new PosPromotionDto
                {
                    PromotionId = 9203,
                    Name = "Ưu đãi Khách hàng Thân thiết",
                    Description = "Giảm trực tiếp 15.000 ₫ cho mọi đơn hàng từ 100.000 ₫",
                    PromotionType = BookKiosk.Domain.Enums.PromotionType.OrderDiscount,
                    OrderDiscount = new PosPromotionOrderDiscountDto { MinOrderValue = 100000, DiscountAmount = 15000 },
                    MinOrderValue = 100000,
                    DiscountAmount = 15000,
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    EndDate = DateTime.UtcNow.AddDays(15),
                    IsActive = true
                },
                new PosPromotionDto
                {
                    PromotionId = 9204,
                    Name = "KM V7.1 Chiết khấu theo đầu sách",
                    Description = "Giảm giá đặc biệt theo từng cuốn sách (Dế Mèn 10K, Đất Rừng 15K, Đắc Nhân Tâm 20K)",
                    PromotionType = BookKiosk.Domain.Enums.PromotionType.ProductDiscount,
                    ProductDiscounts = new List<PosPromotionProductDiscountDto>
                    {
                        new() { BookId = 9301, DiscountAmount = 10000 },
                        new() { BookId = 9302, DiscountAmount = 15000 },
                        new() { BookId = 9304, DiscountAmount = 20000 }
                    },
                    MinOrderValue = null,
                    DiscountAmount = 0,
                    StartDate = DateTime.UtcNow.AddDays(-2),
                    EndDate = DateTime.UtcNow.AddDays(20),
                    IsActive = true
                }
            });

            // --- Thành viên ---
            var memberList = new[]
            {
                new PosMemberDto { MemberId = 9101, PhoneNumber = "0981110001", FullName = "Nguyễn Văn An", Points = 50 },
                new PosMemberDto { MemberId = 9102, PhoneNumber = "0981110002", FullName = "Trần Thị Bích", Points = 0 },
                new PosMemberDto { MemberId = 9103, PhoneNumber = "0981110003", FullName = "Lê Hoàng Cường (Khách Vàng)", Points = 100 },
                new PosMemberDto { MemberId = 9104, PhoneNumber = "0981110004", FullName = "Phạm Minh Đức", Points = 25 }
            };

            foreach (var m in memberList)
            {
                _members[m.PhoneNumber] = m;
            }
        }

        // ==============================================================
        // 1. Quét mã vạch (Barcode Scan)
        // ==============================================================
        public async Task<ApiResult<PosBookDto>> GetBookByBarcodeAsync(string barcode)
        {
            await Task.Delay(80); // Giả lập độ trễ mạng nhẹ

            if (barcode == "8939999999")
            {
                // Kịch bản 6: Mô phỏng mất mạng
                return ApiResult<PosBookDto>.Fail("Không thể kết nối đến máy chủ POS (Lỗi mạng mô phỏng 503).", 503);
            }

            if (_barcodeMap.TryGetValue(barcode.Trim(), out var bookId) && _books.TryGetValue(bookId, out var book))
            {
                return ApiResult<PosBookDto>.Ok(book);
            }

            return ApiResult<PosBookDto>.Fail($"Không tìm thấy sách với mã vạch: {barcode}", 404);
        }

        // ==============================================================
        // 2. Tìm kiếm sách thủ công (Search Books)
        // ==============================================================
        public async Task<ApiResult<PaginatedResult<PosBookDto>>> SearchBooksAsync(string? keyword = null, int? categoryId = null, int page = 1, int pageSize = 12)
        {
            await Task.Delay(100);

            var query = _books.Values.Where(b => b.IsActive).AsEnumerable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(b => (b.Title != null && b.Title.ToLower().Contains(kw))
                                      || (b.Author != null && b.Author.ToLower().Contains(kw))
                                      || (b.Barcode != null && b.Barcode.Contains(kw)));
            }

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(b => b.CategoryId == categoryId.Value);
            }

            var allItems = query.ToList();
            var totalCount = allItems.Count;
            var items = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var paginated = new PaginatedResult<PosBookDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return ApiResult<PaginatedResult<PosBookDto>>.Ok(paginated);
        }

        // ==============================================================
        // 3. Danh mục sách
        // ==============================================================
        public async Task<ApiResult<IEnumerable<PosCategoryDto>>> GetCategoriesAsync()
        {
            await Task.Delay(50);
            return ApiResult<IEnumerable<PosCategoryDto>>.Ok(_categories);
        }

        // ==============================================================
        // 4. Khuyến mãi đang hoạt động
        // ==============================================================
        public async Task<ApiResult<IEnumerable<PosPromotionDto>>> GetActivePromotionsAsync()
        {
            await Task.Delay(50);
            var now = DateTime.UtcNow;
            var active = _promotions.Where(p => p.IsActive && now >= p.StartDate && now <= p.EndDate).ToList();
            return ApiResult<IEnumerable<PosPromotionDto>>.Ok(active);
        }

        // ==============================================================
        // 5. Tra cứu thành viên bằng SĐT
        // ==============================================================
        public async Task<ApiResult<PosMemberDto>> GetMemberByPhoneAsync(string phoneNumber)
        {
            await Task.Delay(80);
            if (_members.TryGetValue(phoneNumber.Trim(), out var member))
            {
                return ApiResult<PosMemberDto>.Ok(member);
            }

            return ApiResult<PosMemberDto>.Fail("Không tìm thấy thành viên với số điện thoại này.", 404);
        }

        // ==============================================================
        // 6. Đăng ký nhanh thành viên tại quầy
        // ==============================================================
        public async Task<ApiResult<PosMemberDto>> CreateMemberAsync(PosCreateMemberDto dto)
        {
            await Task.Delay(120);

            var phone = dto.PhoneNumber.Trim();
            if (_members.ContainsKey(phone))
            {
                return ApiResult<PosMemberDto>.Fail("Số điện thoại này đã được đăng ký thành viên trước đó.", 400);
            }

            var newMember = new PosMemberDto
            {
                MemberId = System.Threading.Interlocked.Increment(ref _memberIdCounter),
                PhoneNumber = phone,
                FullName = dto.FullName.Trim(),
                Points = 0
            };

            _members[phone] = newMember;
            return ApiResult<PosMemberDto>.Ok(newMember);
        }

        // ==============================================================
        // 7. Tạo đơn hàng quầy nháp (CheckoutCounter)
        // Thin Client Rule: Mọi số tiền giảm, trừ điểm, thực trả tính tại đây
        // ==============================================================
        public async Task<ApiResult<CounterCheckoutResponseDto>> CheckoutCounterAsync(CounterCheckoutRequestDto dto)
        {
            await Task.Delay(150);

            if (dto.Items == null || dto.Items.Count == 0)
            {
                return ApiResult<CounterCheckoutResponseDto>.Fail("Giỏ hàng rỗng, không thể tạo đơn.", 400);
            }

            // Kịch bản 4: Kiểm tra tồn kho & Báo tên sách nếu hết hàng
            decimal subTotal = 0;
            var orderItems = new List<CounterOrderItemDto>();

            foreach (var item in dto.Items)
            {
                if (!_books.TryGetValue(item.BookId, out var book))
                {
                    return ApiResult<CounterCheckoutResponseDto>.Fail($"Không tìm thấy sách #{item.BookId} trong hệ thống.", 404);
                }

                if (book.AvailableStock < item.Quantity)
                {
                    // 409 Xung đột tồn kho kèm tên sách cụ thể
                    return ApiResult<CounterCheckoutResponseDto>.Fail(
                        $"Sách \"{book.Title}\" hiện không đủ tồn kho khả dụng (yêu cầu {item.Quantity}, chỉ còn {book.AvailableStock} cuốn).",
                        409);
                }

                var lineTotal = book.SellingPrice * item.Quantity;
                subTotal += lineTotal;

                orderItems.Add(new CounterOrderItemDto
                {
                    BookId = book.BookId,
                    Title = book.Title,
                    Quantity = item.Quantity,
                    UnitPriceAtTime = book.SellingPrice,
                    DiscountPerUnit = 0m,
                    LineTotal = lineTotal,
                    LineDiscountAmount = 0m,
                    NetLineTotal = lineTotal
                });
            }

            // Tính toán Khuyến mãi (Backend tính)
            decimal discountAmount = 0;
            PosPromotionDto? appliedPromo = null;
            BookKiosk.Domain.Enums.PromotionType? appliedPromoType = null;
            if (dto.PromotionId.HasValue && dto.PromotionId.Value > 0)
            {
                var promo = _promotions.FirstOrDefault(p => p.PromotionId == dto.PromotionId.Value);
                if (promo != null && promo.IsActive)
                {
                    appliedPromo = promo;
                    if (promo.PromotionId == 9204)
                    {
                        // Kịch bản V7.1 ProductDiscount nhiều sách để thử tay POS
                        appliedPromoType = BookKiosk.Domain.Enums.PromotionType.ProductDiscount;
                        foreach (var item in orderItems)
                        {
                            decimal discPerUnit = item.BookId switch
                            {
                                9301 => 10000m, // Dế Mèn: giảm 10k/cuốn
                                9302 => 15000m, // Đất Rừng: giảm 15k/cuốn
                                9304 => 20000m, // Đắc Nhân Tâm: giảm 20k/cuốn
                                _ => 0m
                            };
                            item.DiscountPerUnit = discPerUnit;
                            item.LineDiscountAmount = discPerUnit * item.Quantity;
                            item.NetLineTotal = item.LineTotal - item.LineDiscountAmount;
                        }
                        discountAmount = orderItems.Sum(i => i.LineDiscountAmount);
                    }
                    else
                    {
                        // Kịch bản mặc định OrderDiscount
                        appliedPromoType = BookKiosk.Domain.Enums.PromotionType.OrderDiscount;
                        if (!promo.EffectiveMinOrderValue.HasValue || subTotal >= promo.EffectiveMinOrderValue.Value)
                        {
                            discountAmount = promo.EffectiveDiscountAmount;
                        }
                    }
                }
            }

            // Tính toán Trừ điểm (Backend tính)
            int pointsUsed = 0;
            decimal pointsUsedAmount = 0;
            if (dto.MemberId.HasValue && dto.PointsToUse > 0)
            {
                var member = _members.Values.FirstOrDefault(m => m.MemberId == dto.MemberId.Value);
                if (member != null)
                {
                    pointsUsed = Math.Clamp(dto.PointsToUse, 0, Math.Min(100, member.Points));
                    pointsUsedAmount = pointsUsed * 1000m;
                }
            }

            // Thực trả Backend tính
            var totalAmount = Math.Max(0, subTotal - discountAmount - pointsUsedAmount);

            // Giữ kho (Trừ AvailableStock)
            foreach (var item in dto.Items)
            {
                if (_books.TryGetValue(item.BookId, out var book))
                {
                    book.AvailableStock -= item.Quantity;
                }
            }

            var orderId = System.Threading.Interlocked.Increment(ref _orderIdCounter);
            var now = DateTime.UtcNow;
            var expiresAt = now.AddMinutes(4);
            var orderCode = $"ORD-{now:yyMMdd}-{orderId}";

            // Backend thật luôn sort items tăng dần theo BookId
            var sortedOrderItems = orderItems.OrderBy(i => i.BookId).ToList();

            var orderEntity = new MockOrderEntity
            {
                OrderId = orderId,
                OrderCode = orderCode,
                Status = "Pending",
                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                PointsUsed = pointsUsed,
                PointsUsedAmount = pointsUsedAmount,
                TotalAmount = totalAmount,
                PaymentMethod = dto.PaymentMethod,
                MemberId = dto.MemberId,
                PromotionId = appliedPromo?.PromotionId,
                PromotionName = appliedPromo?.Name,
                PromotionType = appliedPromoType,
                CreatedAt = now,
                ExpiresAt = expiresAt,
                Items = sortedOrderItems,
                PollCount = 0
            };

            _orders[orderId] = orderEntity;

            var response = new CounterCheckoutResponseDto
            {
                OrderId = orderId,
                OrderCode = orderCode,
                OrderStatus = BookKiosk.CMS.Models.Api.Orders.OrderStatus.Pending,
                SubTotal = subTotal,
                PromotionId = appliedPromo?.PromotionId,
                PromotionName = appliedPromo?.Name,
                PromotionType = appliedPromoType,
                DiscountAmount = discountAmount,
                PointsUsed = pointsUsed,
                PointsUsedAmount = pointsUsedAmount,
                TotalAmount = totalAmount,
                ExpiresAt = expiresAt,
                CreatedAt = now,
                RemainingSeconds = 240,
                SepayQrCodeUrl = "/images/mock-qr.svg",
                Items = sortedOrderItems
            };

            return ApiResult<CounterCheckoutResponseDto>.Ok(response);
        }

        // ==============================================================
        // 8. Xác nhận thanh toán (ConfirmPayment)
        // ==============================================================
        public async Task<ApiResult<PosReceiptDto>> ConfirmPaymentAsync(int orderId)
        {
            await Task.Delay(150);

            if (!_orders.TryGetValue(orderId, out var order))
            {
                return ApiResult<PosReceiptDto>.Fail($"Không tìm thấy đơn hàng #{orderId}.", 404);
            }

            // Kịch bản 5: 409 Đơn không ở trạng thái Pending
            if (!order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResult<PosReceiptDto>.Fail(
                    $"Đơn hàng #{orderId} không ở trạng thái Pending (trạng thái hiện tại: {order.Status}).",
                    409);
            }

            // Chốt thanh toán thành công
            order.Status = "Paid";
            order.CompletedAt = DateTime.UtcNow;

            // Trừ điểm đã dùng và cộng điểm tích lũy mới cho Thành viên
            int pointsEarned = (int)(order.TotalAmount / 10000m);
            if (order.MemberId.HasValue)
            {
                var member = _members.Values.FirstOrDefault(m => m.MemberId == order.MemberId.Value);
                if (member != null)
                {
                    member.Points = Math.Max(0, member.Points - order.PointsUsed) + pointsEarned;
                }
            }

            // Xuất Hóa đơn bán lẻ K80
            var receipt = new PosReceiptDto
            {
                OrderCode = order.OrderCode,
                CreatedAt = order.CreatedAt,
                CompletedAt = order.CompletedAt.Value,
                CashierName = "Thu ngân",
                MemberDisplayName = "Thành viên",
                PointsEarned = pointsEarned,
                SubTotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                PointsUsed = order.PointsUsed,
                PointsUsedAmount = order.PointsUsedAmount,
                TotalAmount = order.TotalAmount,
                PaymentMethod = order.PaymentMethod,
                Items = order.Items.Select(i => new PosReceiptItemDto
                {
                    BookId = i.BookId,
                    Title = i.Title,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPriceAtTime,
                    LineTotal = i.LineTotal
                }).ToList()
            };

            return ApiResult<PosReceiptDto>.Ok(receipt);
        }

        // ==============================================================
        // 9. Hủy đơn quầy & Nhả kho (CancelOrder)
        // ==============================================================
        public async Task<ApiResult<bool>> CancelOrderAsync(int orderId)
        {
            await Task.Delay(100);

            if (!_orders.TryGetValue(orderId, out var order))
            {
                return ApiResult<bool>.Fail($"Không tìm thấy đơn hàng #{orderId}.", 404);
            }

            // Kịch bản 5: 409 Đơn không ở trạng thái Pending
            if (!order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResult<bool>.Fail(
                    $"Không thể hủy đơn hàng #{orderId} vì không ở trạng thái Pending (trạng thái hiện tại: {order.Status}).",
                    409);
            }

            order.Status = "Cancelled";

            // Nhả kho giữ chỗ
            foreach (var item in order.Items)
            {
                if (_books.TryGetValue(item.BookId, out var book))
                {
                    book.AvailableStock += item.Quantity;
                }
            }

            return ApiResult<bool>.Ok(true);
        }

        // ==============================================================
        // 10. Tra cứu trạng thái đơn (PollStatus)
        // Kịch bản 2: Tự động chuyển Paid sau 2 lần poll (~6s) khi khách thanh toán QR
        // ==============================================================
        public async Task<ApiResult<OrderPaymentStatusDto>> GetOrderStatusAsync(int orderId)
        {
            await Task.Delay(60);

            if (!_orders.TryGetValue(orderId, out var order))
            {
                return ApiResult<OrderPaymentStatusDto>.Fail($"Không tìm thấy đơn hàng #{orderId}.", 404);
            }

            // Giả lập webhook ngân hàng: Nếu đơn đang Pending qua QR, sau 2 lần poll sẽ chuyển sang Paid
            if (order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
                order.PaymentMethod.Equals("QR", StringComparison.OrdinalIgnoreCase))
            {
                order.PollCount++;
                if (order.PollCount >= 2)
                {
                    order.Status = "Paid";
                    order.CompletedAt = DateTime.UtcNow;

                    // Cộng điểm thành viên
                    int pointsEarned = (int)(order.TotalAmount / 10000m);
                    if (order.MemberId.HasValue)
                    {
                        var member = _members.Values.FirstOrDefault(m => m.MemberId == order.MemberId.Value);
                        if (member != null)
                        {
                            member.Points = Math.Max(0, member.Points - order.PointsUsed) + pointsEarned;
                        }
                    }
                }
            }

            var orderStatusEnum = Enum.TryParse<BookKiosk.CMS.Models.Api.Orders.OrderStatus>(order.Status, true, out var parsedStatus)
                ? parsedStatus
                : BookKiosk.CMS.Models.Api.Orders.OrderStatus.Pending;

            var statusDto = new OrderPaymentStatusDto
            {
                OrderId = order.OrderId,
                OrderCode = order.OrderCode,
                Status = orderStatusEnum,
                CompletedAt = order.CompletedAt,
                ServerTime = DateTime.UtcNow
            };

            return ApiResult<OrderPaymentStatusDto>.Ok(statusDto);
        }

        // Class đại diện entity đơn hàng giả lập trong bộ nhớ
        private class MockOrderEntity
        {
            public int OrderId { get; set; }
            public string OrderCode { get; set; } = string.Empty;
            public string Status { get; set; } = "Pending";
            public decimal SubTotal { get; set; }
            public decimal DiscountAmount { get; set; }
            public int PointsUsed { get; set; }
            public decimal PointsUsedAmount { get; set; }
            public decimal TotalAmount { get; set; }
            public string PaymentMethod { get; set; } = "Cash";
            public int? MemberId { get; set; }
            public int? PromotionId { get; set; }
            public string? PromotionName { get; set; }
            public BookKiosk.Domain.Enums.PromotionType? PromotionType { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public List<CounterOrderItemDto> Items { get; set; } = new();
            public int PollCount { get; set; }
        }
    }
}
