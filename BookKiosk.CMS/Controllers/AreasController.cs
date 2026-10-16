using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Services;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [Route("Area")]
    public class AreasController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<AreasController> _logger;

        public AreasController(ICmsApiClient apiClient, ILogger<AreasController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        /// <summary>
        /// Danh sách khu vực & kệ sách (Admin và Staff đều có quyền xem)
        /// </summary>
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var result = await _apiClient.GetAreasAsync();
            if (!result.Success)
            {
                TempData["Error"] = result.Message ?? "Không thể tải danh sách khu vực / kệ sách.";
                return View(new List<AreaDto>());
            }

            return View(result.Data ?? new List<AreaDto>());
        }

        /// <summary>
        /// Tạo khu vực / kệ sách mới (Chỉ dành cho Admin)
        /// </summary>
        [HttpPost("Create")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAreaDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.CreateAreaAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Thêm khu vực \"{dto.Name}\" thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Thêm khu vực thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Cập nhật khu vực / kệ sách (Chỉ dành cho Admin)
        /// </summary>
        [HttpPost("Edit/{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateAreaDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.UpdateAreaAsync(id, dto);
            if (result.Success)
            {
                TempData["Success"] = $"Cập nhật khu vực \"{dto.Name}\" thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Cập nhật khu vực thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Xóa khu vực / kệ sách (Chỉ dành cho Admin)
        /// </summary>
        [HttpPost("Delete/{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _apiClient.DeleteAreaAsync(id);
            if (result.Success)
            {
                TempData["Success"] = "Xóa khu vực thành công! Sách liên quan đã được bỏ vị trí kệ.";
            }
            else
            {
                // Message đã được CmsApiClient xử lý thân thiện khi gặp lỗi 409/400 (đang chứa sách)
                TempData["Error"] = result.Message ?? "Xóa khu vực thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
