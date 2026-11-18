using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.ViewModels.Inventory;

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
            return View(await LoadListAsync(new SupplierListViewModel()));
        }

        /// <summary>
        /// Tạo mới nhà cung cấp
        /// </summary>
        [HttpPost("Create")]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateSupplierDto dto)
        {
            if (!ModelState.IsValid)
            {
                return await RedisplayAsync(dto);
            }

            var result = await _apiClient.CreateSupplierAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Thêm nhà cung cấp \"{dto.Name}\" thành công!";
            }
            else
            {
                AddApiErrors(result);
                return await RedisplayAsync(dto);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Cập nhật thông tin nhà cung cấp
        /// </summary>
        [HttpPost("Edit/{id:int}")]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UpdateSupplierDto dto)
        {
            if (id <= 0) return NotFound();
            if (!ModelState.IsValid)
            {
                return await RedisplayAsync(dto, id);
            }

            var result = await _apiClient.UpdateSupplierAsync(id, dto);
            if (result.Success)
            {
                TempData["Success"] = $"Cập nhật nhà cung cấp \"{dto.Name}\" thành công!";
            }
            else
            {
                AddApiErrors(result);
                return await RedisplayAsync(dto, id);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Vô hiệu hóa nhà cung cấp (Soft delete)
        /// </summary>
        [HttpPost("Delete/{id:int}")]
        [Authorize(Roles = "Admin,Manager")]
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

        private async Task<SupplierListViewModel> LoadListAsync(SupplierListViewModel model)
        {
            var result = await _apiClient.GetSuppliersAsync();
            if (result.Success) model.Suppliers = result.Data ?? new List<SupplierDto>();
            else model.LoadError = "Không thể tải danh sách nhà cung cấp. Dữ liệu bạn đang nhập vẫn được giữ lại.";
            return model;
        }

        private async Task<IActionResult> RedisplayAsync(CreateSupplierDto dto) =>
            View("Index", await LoadListAsync(new SupplierListViewModel { ActiveForm = "create", CreateForm = dto }));

        private async Task<IActionResult> RedisplayAsync(UpdateSupplierDto dto, int id) =>
            View("Index", await LoadListAsync(new SupplierListViewModel { ActiveForm = "edit", EditForm = dto, EditId = id }));

        private void AddApiErrors(ApiResult<SupplierDto> result)
        {
            foreach (var field in result.FieldErrors)
                foreach (var error in field.Value) ModelState.AddModelError(field.Key, error);
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể lưu nhà cung cấp. Vui lòng thử lại.");
        }
    }
}
