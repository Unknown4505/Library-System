using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
            vm.UpcomingCount = allPromotions.Count(p => p.IsActive && DateTime.Now < p.StartDate);
            vm.ExpiredCount = allPromotions.Count(p => p.IsActive && DateTime.Now > p.EndDate);
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
                "upcoming" => query.Where(p => p.IsActive && DateTime.Now < p.StartDate),
                "expired" => query.Where(p => p.IsActive && DateTime.Now > p.EndDate),
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
        public async Task<IActionResult> Create()
        {
            if (!IsAdmin)
            {
                TempData["ToastMessage"] = "Bạn không có quyền thực hiện chức năng này.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var vm = new PromotionFormViewModel
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddMonths(1),
                DiscountAmount = 10000,
                MinOrderValue = 0,
                IsActive = true,
                PromotionType = 1,
                AvailableBooks = await GetBookSelectListAsync()
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
                vm.AvailableBooks = await GetBookSelectListAsync(vm.BookId);
                return View(vm);
            }

            var dto = new CreatePromotionDto
            {
                Name = vm.Name.Trim(),
                Description = vm.Description?.Trim(),
                PromotionType = vm.PromotionType,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                IsActive = vm.IsActive,
                MinOrderValue = vm.PromotionType == 1 ? (vm.MinOrderValue ?? 0) : null,
                DiscountAmount = vm.DiscountAmount,
                BookId = vm.PromotionType == 2 ? vm.BookId : null
            };

            var result = await _apiClient.CreatePromotionAsync(dto);
            if (result.Success)
            {
                TempData["ToastMessage"] = $"Đã tạo mới chương trình khuyến mãi '{dto.Name}' thành công!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Đã xảy ra lỗi khi tạo chương trình khuyến mãi.");
            vm.AvailableBooks = await GetBookSelectListAsync(vm.BookId);
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
                StartDate = promo.StartDate,
                EndDate = promo.EndDate,
                IsActive = promo.IsActive,
                MinOrderValue = promo.MinOrderValue,
                DiscountAmount = promo.DiscountAmount,
                BookId = promo.BookId,
                AvailableBooks = await GetBookSelectListAsync(promo.BookId)
            };

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
                vm.AvailableBooks = await GetBookSelectListAsync(vm.BookId);
                return View(vm);
            }

            var dto = new UpdatePromotionDto
            {
                Name = vm.Name.Trim(),
                Description = vm.Description?.Trim(),
                PromotionType = vm.PromotionType,
                StartDate = vm.StartDate,
                EndDate = vm.EndDate,
                IsActive = vm.IsActive,
                MinOrderValue = vm.PromotionType == 1 ? (vm.MinOrderValue ?? 0) : null,
                DiscountAmount = vm.DiscountAmount,
                BookId = vm.PromotionType == 2 ? vm.BookId : null
            };

            var result = await _apiClient.UpdatePromotionAsync(id, dto);
            if (result.Success)
            {
                TempData["ToastMessage"] = $"Cập nhật chương trình khuyến mãi '{dto.Name}' thành công!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Đã xảy ra lỗi khi cập nhật chương trình khuyến mãi.");
            vm.AvailableBooks = await GetBookSelectListAsync(vm.BookId);
            return View(vm);
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

            if (vm.PromotionType == 2)
            {
                if (!vm.BookId.HasValue || vm.BookId.Value <= 0)
                {
                    ModelState.AddModelError(nameof(vm.BookId), "Vui lòng chọn ấn phẩm sách áp dụng mức giảm giá.");
                }
            }
            else if (vm.PromotionType == 1)
            {
                if (vm.MinOrderValue.HasValue && vm.MinOrderValue.Value < 0)
                {
                    ModelState.AddModelError(nameof(vm.MinOrderValue), "Giá trị đơn hàng tối thiểu không được âm.");
                }
            }
        }

        private async Task<IEnumerable<SelectListItem>> GetBookSelectListAsync(int? selectedBookId = null)
        {
            var items = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Chọn sách áp dụng giảm giá --" }
            };

            try
            {
                var booksResult = await _apiClient.GetBooksAsync(page: 1, pageSize: 300);
                if (booksResult.Success && booksResult.Data?.Items != null)
                {
                    foreach (var book in booksResult.Data.Items)
                    {
                        items.Add(new SelectListItem
                        {
                            Value = book.BookId.ToString(),
                            Text = $"{book.Title} (Tồn: {book.StockQuantity} | Giá bán: {book.SellingPrice:N0} ₫)",
                            Selected = selectedBookId.HasValue && book.BookId == selectedBookId.Value
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi nạp danh sách sách cho khuyến mãi");
            }

            return items;
        }

        #endregion
    }
}
