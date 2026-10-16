using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Services;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [Route("Supplier")]
    public class SuppliersController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<SuppliersController> _logger;

        public SuppliersController(ICmsApiClient apiClient, ILogger<SuppliersController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        /// <summary>
        /// Danh sách Nhà cung cấp sách (Phân hệ F)
        /// </summary>
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index()
        {
            var result = await _apiClient.GetSuppliersAsync();
            if (!result.Success)
            {
                TempData["Error"] = result.Message ?? "Không thể tải danh sách nhà cung cấp.";
                return View(new List<SupplierDto>());
            }

            return View(result.Data ?? new List<SupplierDto>());
        }

        /// <summary>
        /// Tạo mới nhà cung cấp
        /// </summary>
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSupplierDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.CreateSupplierAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Thêm nhà cung cấp \"{dto.Name}\" thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Thêm nhà cung cấp thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Cập nhật thông tin nhà cung cấp
        /// </summary>
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateSupplierDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.UpdateSupplierAsync(id, dto);
            if (result.Success)
            {
                TempData["Success"] = $"Cập nhật nhà cung cấp \"{dto.Name}\" thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Cập nhật nhà cung cấp thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Vô hiệu hóa nhà cung cấp (Soft delete)
        /// </summary>
        [HttpPost("Delete/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _apiClient.DeleteSupplierAsync(id);
            if (result.Success)
            {
                TempData["Success"] = "Đã vô hiệu hóa nhà cung cấp thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Vô hiệu hóa nhà cung cấp thất bại.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
