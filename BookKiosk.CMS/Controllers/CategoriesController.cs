using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Services;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [Route("Category")]
    public class CategoriesController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(ICmsApiClient apiClient, ILogger<CategoriesController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        /// <summary>
        /// Danh sách danh mục thể loại sách (Admin và Staff đều có quyền xem)
        /// </summary>
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var result = await _apiClient.GetCategoriesAsync();
            if (!result.Success)
            {
                TempData["Error"] = result.Message ?? "Không thể tải danh sách danh mục thể loại.";
                return View(new List<CategoryDto>());
            }

            return View(result.Data ?? new List<CategoryDto>());
        }

        /// <summary>
        /// Tạo danh mục mới (Chỉ dành cho Admin)
        /// </summary>
        [HttpPost("Create")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCategoryDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.CreateCategoryAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Thêm danh mục \"{dto.Name}\" thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Thêm danh mục thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Cập nhật thông tin danh mục (Chỉ dành cho Admin)
        /// </summary>
        [HttpPost("Edit/{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateCategoryDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.UpdateCategoryAsync(id, dto);
            if (result.Success)
            {
                TempData["Success"] = $"Cập nhật danh mục \"{dto.Name}\" thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Cập nhật danh mục thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Xóa danh mục (Chỉ dành cho Admin)
        /// </summary>
        [HttpPost("Delete/{id:int}")]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _apiClient.DeleteCategoryAsync(id);
            if (result.Success)
            {
                TempData["Success"] = "Xóa danh mục thành công!";
            }
            else
            {
                // Message đã được CmsApiClient xử lý thân thiện khi gặp lỗi 409/400 (đang chứa sách)
                TempData["Error"] = result.Message ?? "Xóa danh mục thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
