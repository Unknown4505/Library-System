using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Inventory;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Inventory;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class InventoryController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<InventoryController> _logger;

        public InventoryController(ICmsApiClient apiClient, ILogger<InventoryController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        #region Receipts Management (Quản lý Phiếu nhập kho)

        /// <summary>
        /// Danh sách phiếu nhập kho có lọc theo ngày, nhà cung cấp và phân trang
        /// </summary>
        [HttpGet("")]
        [HttpGet("Index")]
        [HttpGet("Receipts")]
        public async Task<IActionResult> Index(
            int? supplierId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 10)
        {
            var vm = new ReceiptListViewModel
            {
                SupplierId = supplierId,
                FromDate = fromDate,
                ToDate = toDate,
                Page = Math.Max(1, page),
                PageSize = pageSize > 0 ? pageSize : 10
            };

            // 1. Tải danh sách phiếu nhập kho từ API
            var receiptsResult = await _apiClient.GetReceiptsAsync(vm.SupplierId, vm.FromDate, vm.ToDate, vm.Page, vm.PageSize);
            if (receiptsResult.Success && receiptsResult.Data != null)
            {
                vm.Receipts = receiptsResult.Data;
            }
            else
            {
                vm.Receipts = new PaginatedResult<ImportReceiptDto>();
            }

            // 2. Tải danh sách nhà cung cấp để nạp vào bộ lọc
            var suppliersResult = await _apiClient.GetSuppliersAsync();
            if (suppliersResult.Success && suppliersResult.Data != null)
            {
                vm.Suppliers = suppliersResult.Data;
            }

            return View(vm);
        }

        /// <summary>
        /// Xem chi tiết phiếu nhập kho (Bất biến - Không cho sửa/xóa)
        /// </summary>
        [HttpGet("Detail/{id:int}")]
        public async Task<IActionResult> Detail(int id)
        {
            var result = await _apiClient.GetReceiptByIdAsync(id);
            if (!result.Success || result.Data == null)
            {
                TempData["Error"] = result.Message ?? "Không tìm thấy thông tin phiếu nhập kho yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data);
        }

        /// <summary>
        /// Màn hình lập phiếu nhập kho với UX quét mã vạch liên tục
        /// </summary>
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var vm = new CreateReceiptViewModel();
            await PopulateSuppliersDropdownAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Xử lý lưu phiếu nhập kho
        /// </summary>
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateImportReceiptDto dto)
        {
            if (dto.SupplierId <= 0)
            {
                ModelState.AddModelError(nameof(dto.SupplierId), "Vui lòng chọn nhà cung cấp hợp lệ.");
            }

            if (dto.Items == null || !dto.Items.Any())
            {
                ModelState.AddModelError(string.Empty, "Phiếu nhập phải có ít nhất một ấn phẩm sách.");
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu không hợp lệ: {errors}";

                var vm = new CreateReceiptViewModel
                {
                    SupplierId = dto.SupplierId,
                    Notes = dto.Notes
                };
                await PopulateSuppliersDropdownAsync(vm);
                return View(vm);
            }

            // Gửi dữ liệu tạo phiếu lên Backend API (Backend tính toán lại tổng tiền và tăng Stock)
            var result = await _apiClient.CreateReceiptAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Lập phiếu nhập kho thành công! Mã phiếu #{result.Data}.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = result.Message ?? "Không thể lưu phiếu nhập kho.";
            var retryVm = new CreateReceiptViewModel
            {
                SupplierId = dto.SupplierId,
                Notes = dto.Notes
            };
            await PopulateSuppliersDropdownAsync(retryVm);
            return View(retryVm);
        }

        #endregion

        #region Stock & Adjust Management (Tồn kho & Kiểm kê)

        /// <summary>
        /// Màn hình giám sát Tồn kho & Cảnh báo sắp hết hàng
        /// </summary>
        [HttpGet("Stock")]
        public async Task<IActionResult> Stock(bool lowOnly = false, string? keyword = null)
        {
            var vm = new StockListViewModel
            {
                LowOnly = lowOnly,
                Keyword = keyword?.Trim()
            };

            // 1. Gọi API lấy dữ liệu tồn kho (nguồn thẩm quyền duy nhất)
            try
            {
                var stockResult = await _apiClient.GetStockAsync(lowOnly, vm.Keyword);
                if (stockResult.Success && stockResult.Data != null)
                {
                    vm.StockItems = stockResult.Data;
                    vm.IsUnavailable = false;
                }
                else if (stockResult.Success && stockResult.Data == null)
                {
                    vm.StockItems = Enumerable.Empty<StockItemDto>();
                    vm.IsUnavailable = true;
                    vm.ErrorMessage = "Dữ liệu tồn kho từ máy chủ không hợp lệ.";
                    TempData["Error"] = vm.ErrorMessage;
                    _logger.LogWarning("API tồn kho trả về thành công nhưng dữ liệu null.");
                }
                else if (stockResult.IsEndpointUnavailable)
                {
                    vm.StockItems = Enumerable.Empty<StockItemDto>();
                    vm.IsUnavailable = true;
                    vm.ErrorMessage = "Dịch vụ quản lý tồn kho hiện chưa sẵn sàng. Vui lòng liên hệ quản trị viên.";
                    TempData["Error"] = vm.ErrorMessage;
                    _logger.LogWarning("Endpoint tồn kho chưa sẵn sàng: {Message}", stockResult.Message);
                }
                else
                {
                    vm.StockItems = Enumerable.Empty<StockItemDto>();
                    vm.IsUnavailable = true;
                    vm.ErrorMessage = stockResult.Message ?? "Không thể tải dữ liệu tồn kho từ máy chủ. Vui lòng thử lại sau.";
                    TempData["Error"] = vm.ErrorMessage;
                    _logger.LogWarning("Không tải được dữ liệu tồn kho: {Message}", stockResult.Message);
                }
            }
            catch (Exception ex)
            {
                vm.StockItems = Enumerable.Empty<StockItemDto>();
                vm.IsUnavailable = true;
                vm.ErrorMessage = "Đã xảy ra lỗi khi kết nối đến dịch vụ tồn kho.";
                TempData["Error"] = vm.ErrorMessage;
                _logger.LogError(ex, "Lỗi ngoại lệ khi gọi GetStockAsync");
            }

            return View(vm);
        }

        /// <summary>
        /// Xử lý điều chỉnh kiểm kê kho (Yêu cầu bắt buộc phải có Lý do)
        /// </summary>
        [HttpPost("Adjust")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(AdjustStockDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["Error"] = $"Dữ liệu điều chỉnh không hợp lệ: {errors}";
                return RedirectToAction(nameof(Stock));
            }

            if (string.IsNullOrWhiteSpace(dto.Reason))
            {
                TempData["Error"] = "Bắt buộc phải nhập lý do kiểm kê / điều chỉnh số tồn kho.";
                return RedirectToAction(nameof(Stock));
            }

            var result = await _apiClient.AdjustStockAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Đã cập nhật kiểm kê sách #{dto.BookId} thành công (Tồn mới: {dto.ActualQuantity} cuốn).";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Ghi nhận điều chỉnh kiểm kê thất bại.";
            }

            return RedirectToAction(nameof(Stock));
        }

        #endregion

        #region Helpers

        private async Task PopulateSuppliersDropdownAsync(CreateReceiptViewModel vm)
        {
            var suppliersResult = await _apiClient.GetSuppliersAsync();
            if (suppliersResult.Success && suppliersResult.Data != null)
            {
                // Chỉ hiển thị nhà cung cấp đang hoạt động khi tạo phiếu mới
                vm.SupplierOptions = suppliersResult.Data
                    .Where(s => s.IsActive)
                    .Select(s => new SelectListItem
                    {
                        Value = s.SupplierId.ToString(),
                        Text = string.IsNullOrWhiteSpace(s.ContactName) ? s.Name : $"{s.Name} ({s.ContactName})",
                        Selected = s.SupplierId == vm.SupplierId
                    }).ToList();
            }
        }

        #endregion
    }
}
