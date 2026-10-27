using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Promotions;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Promotions;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    public class PromotionsController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<PromotionsController> _logger;

        public PromotionsController(ICmsApiClient apiClient, ILogger<PromotionsController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        private bool IsAdmin => User.IsInRole("Admin");

        #region TimeZone Helpers (Vietnam UTC+7)

        private static readonly TimeZoneInfo VietnamTimeZone = GetVietnamTimeZone();

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
                }
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("Asia/Ho_Chi_Minh", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
            }
        }

        public static DateTime ConvertVietnamLocalToUtc(DateTime localInput, bool isEndOfDay = false)
        {
            var date = localInput.Date;
            var time = isEndOfDay ? new TimeSpan(23, 59, 59) : localInput.TimeOfDay;
            if (time == TimeSpan.Zero && isEndOfDay)
            {
                time = new TimeSpan(23, 59, 59);
            }
            var unspecified = new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, time.Seconds, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, VietnamTimeZone);
        }

        public static DateTime ConvertUtcToVietnamLocal(DateTime utcInput)
        {
            var utc = utcInput.Kind == DateTimeKind.Utc
                ? utcInput
                : DateTime.SpecifyKind(utcInput, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTimeZone);
        }

        public static DateTime ResolveDateToUtc(DateTime inputDate, string? originalUtcString, bool isEndOfDay)
        {
            if (!string.IsNullOrWhiteSpace(originalUtcString) &&
                DateTime.TryParse(originalUtcString, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out var originalUtc))
            {
                var originalUtcNormalized = DateTime.SpecifyKind(originalUtc, DateTimeKind.Utc);
                var originalVnDate = ConvertUtcToVietnamLocal(originalUtcNormalized).Date;
                if (inputDate.Date == originalVnDate)
                {
                    return originalUtcNormalized;
                }
            }

            return ConvertVietnamLocalToUtc(inputDate, isEndOfDay);
        }

        #endregion

        /// <summary>
        /// Danh sách chương trình khuyến mãi (Admin quản lý toàn diện, Staff xem các khuyến mãi đang hiệu lực)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? status = "all", string? keyword = null)
        {
            var vm = new PromotionListViewModel
            {
                Keyword = keyword?.Trim(),
                CurrentStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.Trim().ToLower(),
                IsAdmin = IsAdmin
            };

            // 1. Tải dữ liệu từ API
            List<PromotionDto> allPromotions = new();
            if (IsAdmin)
            {
                var result = await _apiClient.GetPromotionsAsync();
                if (result.Success && result.Data != null)
                {
                    allPromotions = result.Data.ToList();
                }
                else if (!result.Success)
                {
                    TempData["AlertMessage"] = result.Message ?? "Không thể tải danh sách chương trình khuyến mãi.";
                    TempData["AlertType"] = "danger";
                }
            }
            else
            {
                // Nhân viên thu ngân chỉ xem các khuyến mãi đang hiệu lực
                var result = await _apiClient.GetActivePromotionsAsync();
                if (result.Success && result.Data != null)
                {
                    allPromotions = result.Data.ToList();
                }
            }

            // 2. Thống kê số lượng theo trạng thái
            vm.TotalCount = allPromotions.Count;
            vm.ActiveCount = allPromotions.Count(p => p.IsEffectiveNow);
            vm.UpcomingCount = allPromotions.Count(p => p.IsActive && DateTime.UtcNow < p.StartDate);
            vm.ExpiredCount = allPromotions.Count(p => p.IsActive && DateTime.UtcNow > p.EndDate);
            vm.InactiveCount = allPromotions.Count(p => !p.IsActive);

            // 3. Lọc theo từ khóa
            var query = allPromotions.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(vm.Keyword))
            {
                var kw = vm.Keyword.ToLower();
                query = query.Where(p =>
                    (p.Name != null && p.Name.ToLower().Contains(kw)) ||
                    (p.Description != null && p.Description.ToLower().Contains(kw)) ||
                    (p.BookTitle != null && p.BookTitle.ToLower().Contains(kw)));
            }

            // 4. Lọc theo tab trạng thái
            query = vm.CurrentStatus switch
            {
                "active" => query.Where(p => p.IsEffectiveNow),
                "upcoming" => query.Where(p => p.IsActive && DateTime.UtcNow < p.StartDate),
                "expired" => query.Where(p => p.IsActive && DateTime.UtcNow > p.EndDate),
                "inactive" => query.Where(p => !p.IsActive),
                _ => query
            };

            vm.Promotions = query.OrderByDescending(p => p.PromotionId).ToList();
            return View(vm);
        }

        /// <summary>
        /// Màn hình Tạo mới chương trình khuyến mãi (Chỉ Admin)
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            if (!IsAdmin)
            {
                TempData["ToastMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var todayVn = ConvertUtcToVietnamLocal(DateTime.UtcNow).Date;
            var vm = new PromotionFormViewModel
            {
                StartDate = todayVn,
                EndDate = todayVn.AddMonths(1),
                OrderMinOrderValue = 0,
                OrderDiscountAmount = 10000,
                IsActive = true,
                PromotionType = 1,
                ProductDiscounts = new List<ProductDiscountItemViewModel>()
            };

            return View(vm);
        }

        /// <summary>
        /// Xử lý Tạo mới chương trình khuyến mãi (Chỉ Admin)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PromotionFormViewModel vm)
        {
            if (!IsAdmin)
            {
                TempData["ToastMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            ValidatePromotionRules(vm);

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var dto = new CreatePromotionDto
            {
                Name = vm.Name.Trim(),
                Description = vm.Description?.Trim(),
                PromotionType = vm.PromotionType,
                StartDate = ConvertVietnamLocalToUtc(vm.StartDate, isEndOfDay: false),
                EndDate = ConvertVietnamLocalToUtc(vm.EndDate, isEndOfDay: true),
                IsActive = vm.IsActive,
                OrderDiscount = vm.PromotionType == 1
                    ? new PromotionOrderDiscountDto
                    {
                        MinOrderValue = vm.OrderMinOrderValue ?? 0,
                        DiscountAmount = vm.OrderDiscountAmount ?? 0
                    }
                    : null,
                ProductDiscounts = vm.PromotionType == 2 && vm.ProductDiscounts != null
                    ? vm.ProductDiscounts.Select(p => new PromotionProductDiscountDto
                    {
                        BookId = p.BookId,
                        DiscountAmount = p.DiscountAmount
                    }).ToList()
                    : new List<PromotionProductDiscountDto>()
            };

            var result = await _apiClient.CreatePromotionAsync(dto);
            if (result.Success)
            {
                TempData["ToastMessage"] = $"Đã tạo mới chương trình khuyến mãi '{dto.Name}' thành công!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Đã xảy ra lỗi khi tạo chương trình khuyến mãi.");
            return View(vm);
        }

        /// <summary>
        /// Màn hình Chỉnh sửa chương trình khuyến mãi (Chỉ Admin)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!IsAdmin)
            {
                TempData["ToastMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.GetPromotionByIdAsync(id);
            if (!result.Success || result.Data == null)
            {
                TempData["ToastMessage"] = result.Message ?? "Không tìm thấy chương trình khuyến mãi.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var promo = result.Data;
            var vm = new PromotionFormViewModel
            {
                PromotionId = promo.PromotionId,
                Name = promo.Name,
                Description = promo.Description,
                PromotionType = promo.PromotionType,
                StartDate = ConvertUtcToVietnamLocal(promo.StartDate).Date,
                EndDate = ConvertUtcToVietnamLocal(promo.EndDate).Date,
                OriginalStartDateUtc = (promo.StartDate.Kind == DateTimeKind.Utc ? promo.StartDate : DateTime.SpecifyKind(promo.StartDate, DateTimeKind.Utc)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                OriginalEndDateUtc = (promo.EndDate.Kind == DateTimeKind.Utc ? promo.EndDate : DateTime.SpecifyKind(promo.EndDate, DateTimeKind.Utc)).ToString("yyyy-MM-ddTHH:mm:ssZ"),
                IsActive = promo.IsActive,
                OrderMinOrderValue = promo.OrderDiscount?.MinOrderValue,
                OrderDiscountAmount = promo.OrderDiscount?.DiscountAmount,
                ProductDiscounts = new List<ProductDiscountItemViewModel>()
            };

            if (promo.PromotionType == 2 && promo.ProductDiscounts != null)
            {
                foreach (var p in promo.ProductDiscounts)
                {
                    var itemVm = new ProductDiscountItemViewModel
                    {
                        BookId = p.BookId,
                        BookTitle = p.BookTitle ?? $"Sách #{p.BookId}",
                        DiscountAmount = p.DiscountAmount
                    };

                    try
                    {
                        var bookResult = await _apiClient.GetBookByIdAsync(p.BookId);
                        if (bookResult.Success && bookResult.Data != null)
                        {
                            itemVm.BookTitle = bookResult.Data.Title;
                            itemVm.Barcode = bookResult.Data.Barcode;
                            itemVm.SellingPrice = bookResult.Data.SellingPrice;
                            itemVm.StockQuantity = bookResult.Data.StockQuantity;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Không thể lấy thông tin chi tiết cho sách #{BookId}", p.BookId);
                    }

                    vm.ProductDiscounts.Add(itemVm);
                }
            }

            return View(vm);
        }

        /// <summary>
        /// Xử lý Cập nhật chương trình khuyến mãi (Chỉ Admin)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PromotionFormViewModel vm)
        {
            if (!IsAdmin)
            {
                TempData["ToastMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            if (id != vm.PromotionId)
            {
                TempData["ToastMessage"] = "Mã chương trình khuyến mãi không khớp.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            ValidatePromotionRules(vm);

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var dto = new UpdatePromotionDto
            {
                Name = vm.Name.Trim(),
                Description = vm.Description?.Trim(),
                PromotionType = vm.PromotionType,
                StartDate = ResolveDateToUtc(vm.StartDate, vm.OriginalStartDateUtc, isEndOfDay: false),
                EndDate = ResolveDateToUtc(vm.EndDate, vm.OriginalEndDateUtc, isEndOfDay: true),
                IsActive = vm.IsActive,
                OrderDiscount = vm.PromotionType == 1
                    ? new PromotionOrderDiscountDto
                    {
                        MinOrderValue = vm.OrderMinOrderValue ?? 0,
                        DiscountAmount = vm.OrderDiscountAmount ?? 0
                    }
                    : null,
                ProductDiscounts = vm.PromotionType == 2 && vm.ProductDiscounts != null
                    ? vm.ProductDiscounts.Select(p => new PromotionProductDiscountDto
                    {
                        BookId = p.BookId,
                        DiscountAmount = p.DiscountAmount
                    }).ToList()
                    : new List<PromotionProductDiscountDto>()
            };

            var result = await _apiClient.UpdatePromotionAsync(id, dto);
            if (result.Success)
            {
                TempData["ToastMessage"] = $"Cập nhật chương trình khuyến mãi '{dto.Name}' thành công!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Đã xảy ra lỗi khi cập nhật chương trình khuyến mãi.");
            return View(vm);
        }

        /// <summary>
        /// Tìm kiếm sách theo từ khóa có phân trang phục vụ cấu hình khuyến mãi (AJAX - Admin)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SearchBooks(string? keyword, int page = 1, int pageSize = 8)
        {
            try
            {
                var result = await _apiClient.GetBooksAsync(page: Math.Max(1, page), pageSize: Math.Clamp(pageSize, 1, 50), keyword: keyword?.Trim());
                if (result.Success && result.Data != null)
                {
                    return Json(new
                    {
                        success = true,
                        items = result.Data.Items.Select(b => new
                        {
                            bookId = b.BookId,
                            title = b.Title,
                            barcode = b.Barcode,
                            sellingPrice = b.SellingPrice,
                            stockQuantity = b.StockQuantity,
                            author = b.Author
                        }),
                        totalCount = result.Data.TotalCount,
                        page = result.Data.Page,
                        pageSize = result.Data.PageSize,
                        totalPages = result.Data.TotalPages
                    });
                }
                return Json(new { success = false, message = result.Message ?? "Không tìm thấy ấn phẩm sách." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tìm kiếm sách trong Promotions");
                return Json(new { success = false, message = "Lỗi khi tra cứu danh mục sách." });
            }
        }

        /// <summary>
        /// Bật/Tắt trạng thái kích hoạt khuyến mãi nhanh chóng (AJAX / POST - Chỉ Admin)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, bool isActive)
        {
            if (!IsAdmin)
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            var result = await _apiClient.TogglePromotionActiveAsync(id, isActive);
            if (result.Success)
            {
                var actionText = isActive ? "Kích hoạt" : "Tạm ngưng";
                return Json(new { success = true, message = $"{actionText} chương trình khuyến mãi thành công!" });
            }

            return Json(new { success = false, message = result.Message ?? "Không thể thay đổi trạng thái khuyến mãi." });
        }

        /// <summary>
        /// Ngưng áp dụng / Vô hiệu hóa chương trình khuyến mãi (Chỉ Admin)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsAdmin)
            {
                TempData["ToastMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.DeletePromotionAsync(id);
            if (result.Success)
            {
                TempData["ToastMessage"] = "Đã ngưng áp dụng chương trình khuyến mãi thành công.";
                TempData["ToastType"] = "success";
            }
            else
            {
                TempData["ToastMessage"] = result.Message ?? "Không thể ngưng chương trình khuyến mãi.";
                TempData["ToastType"] = "error";
            }

            return RedirectToAction(nameof(Index));
        }

        #region Private Helpers

        private void ValidatePromotionRules(PromotionFormViewModel vm)
        {
            if (vm.EndDate < vm.StartDate)
            {
                ModelState.AddModelError(nameof(vm.EndDate), "Ngày kết thúc phải diễn ra sau hoặc cùng ngày với ngày bắt đầu.");
            }

            if (vm.PromotionType == 1) // OrderDiscount
            {
                if (!vm.OrderMinOrderValue.HasValue || vm.OrderMinOrderValue.Value < 0)
                {
                    ModelState.AddModelError(nameof(vm.OrderMinOrderValue), "Giá trị đơn hàng tối thiểu không được âm.");
                }
                else if (decimal.Truncate(vm.OrderMinOrderValue.Value) != vm.OrderMinOrderValue.Value)
                {
                    ModelState.AddModelError(nameof(vm.OrderMinOrderValue), "Giá trị đơn hàng tối thiểu phải là số nguyên (VNĐ).");
                }

                if (!vm.OrderDiscountAmount.HasValue || vm.OrderDiscountAmount.Value <= 0)
                {
                    ModelState.AddModelError(nameof(vm.OrderDiscountAmount), "Số tiền giảm giá phải lớn hơn 0 đồng.");
                }
                else if (decimal.Truncate(vm.OrderDiscountAmount.Value) != vm.OrderDiscountAmount.Value)
                {
                    ModelState.AddModelError(nameof(vm.OrderDiscountAmount), "Số tiền giảm giá phải là số nguyên (VNĐ).");
                }
            }
            else if (vm.PromotionType == 2) // ProductDiscount
            {
                if (vm.ProductDiscounts == null || vm.ProductDiscounts.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Vui lòng chọn ít nhất một ấn phẩm sách áp dụng mức giảm giá.");
                }
                else
                {
                    var duplicates = vm.ProductDiscounts.GroupBy(x => x.BookId).Where(g => g.Count() > 1).ToList();
                    if (duplicates.Count > 0)
                    {
                        ModelState.AddModelError(string.Empty, $"Trùng lặp ấn phẩm sách trong cấu hình giảm giá (Mã sách: {string.Join(", ", duplicates.Select(d => d.Key))}).");
                    }

                    for (int i = 0; i < vm.ProductDiscounts.Count; i++)
                    {
                        var item = vm.ProductDiscounts[i];
                        if (item.BookId <= 0)
                        {
                            ModelState.AddModelError($"ProductDiscounts[{i}].BookId", "Mã ấn phẩm sách không hợp lệ.");
                        }

                        if (item.DiscountAmount <= 0)
                        {
                            ModelState.AddModelError($"ProductDiscounts[{i}].DiscountAmount", $"Số tiền giảm cho sách #{item.BookId} phải lớn hơn 0 đồng.");
                        }
                        else if (decimal.Truncate(item.DiscountAmount) != item.DiscountAmount)
                        {
                            ModelState.AddModelError($"ProductDiscounts[{i}].DiscountAmount", $"Số tiền giảm cho sách #{item.BookId} phải là số nguyên (VNĐ).");
                        }
                    }
                }
            }
            else
            {
                ModelState.AddModelError(nameof(vm.PromotionType), "Loại khuyến mãi không hợp lệ.");
            }
        }

        #endregion
    }
}
