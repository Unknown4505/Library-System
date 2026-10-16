using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Catalog;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    public class BooksController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<BooksController> _logger;

        public BooksController(ICmsApiClient apiClient, ILogger<BooksController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        /// <summary>
        /// Danh sách sách Catalog có phân trang và bộ lọc tìm kiếm
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 10,
            string? keyword = null,
            int? categoryId = null,
            int? areaId = null)
        {
            var vm = new BookListViewModel
            {
                Page = Math.Max(1, page),
                PageSize = pageSize > 0 ? pageSize : 10,
                Keyword = keyword?.Trim(),
                CategoryId = categoryId,
                AreaId = areaId
            };

            // 1. Tải danh sách sách phân trang từ Backend API
            var booksResult = await _apiClient.GetBooksAsync(vm.Page, vm.PageSize, vm.Keyword, vm.CategoryId, vm.AreaId);
            if (booksResult.Success && booksResult.Data != null)
            {
                vm.Books = booksResult.Data;
            }
            else
            {
                TempData["Error"] = booksResult.Message ?? "Không thể tải danh sách sách từ máy chủ.";
            }

            // 2. Tải danh mục và khu vực để nạp vào bộ lọc Toolbar
            var categoriesResult = await _apiClient.GetCategoriesAsync();
            if (categoriesResult.Success && categoriesResult.Data != null)
            {
                vm.Categories = categoriesResult.Data;
            }

            var areasResult = await _apiClient.GetAreasAsync();
            if (areasResult.Success && areasResult.Data != null)
            {
                vm.Areas = areasResult.Data;
            }

            return View(vm);
        }

        /// <summary>
        /// Màn hình thêm mới sách (Admin & Staff đều có quyền)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var vm = new BookFormViewModel
            {
                IsEditMode = false,
                IsActive = true,
                StockQuantity = 0
            };

            await PopulateDropdownsAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Xử lý thêm mới sách
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(vm);
                return View(vm);
            }

            var dto = new CreateBookDto
            {
                Barcode = vm.Barcode.Trim(),
                Title = vm.Title.Trim(),
                Author = vm.Author.Trim(),
                Publisher = vm.Publisher?.Trim(),
                ImageUrl = vm.ImageUrl?.Trim(),
                CostPrice = vm.CostPrice,
                SellingPrice = vm.SellingPrice,
                StockQuantity = vm.StockQuantity,
                CategoryId = vm.CategoryId,
                AreaId = vm.AreaId,
                IsActive = vm.IsActive
            };

            var result = await _apiClient.CreateBookAsync(dto);
            if (result.Success)
            {
                TempData["Success"] = $"Thêm mới sách \"{dto.Title}\" thành công!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = result.Message ?? "Thêm sách thất bại.";
            await PopulateDropdownsAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Màn hình chỉnh sửa sách (Admin & Staff đều có quyền)
        /// Lưu ý nghiệp vụ: StockQuantity & ReservedQuantity là CHỈ ĐỌC
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _apiClient.GetBookByIdAsync(id);
            if (!result.Success || result.Data == null)
            {
                TempData["Error"] = result.Message ?? "Không tìm thấy thông tin sách yêu cầu.";
                return RedirectToAction(nameof(Index));
            }

            var book = result.Data;
            var vm = new BookFormViewModel
            {
                IsEditMode = true,
                BookId = book.BookId,
                Barcode = book.Barcode,
                Title = book.Title,
                Author = book.Author,
                Publisher = book.Publisher,
                ImageUrl = book.ImageUrl,
                CostPrice = book.CostPrice,
                SellingPrice = book.SellingPrice,
                StockQuantity = book.StockQuantity,
                ReservedQuantity = book.ReservedQuantity,
                CategoryId = book.CategoryId,
                AreaId = book.AreaId,
                IsActive = book.IsActive
            };

            await PopulateDropdownsAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Xử lý cập nhật thông tin sách
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BookFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.IsEditMode = true;
                await PopulateDropdownsAsync(vm);
                return View(vm);
            }

            var dto = new UpdateBookDto
            {
                Title = vm.Title.Trim(),
                Author = vm.Author.Trim(),
                Publisher = vm.Publisher?.Trim(),
                ImageUrl = vm.ImageUrl?.Trim(),
                CostPrice = vm.CostPrice,
                SellingPrice = vm.SellingPrice,
                StockQuantity = vm.StockQuantity, // Giữ nguyên tồn kho hiện tại
                CategoryId = vm.CategoryId,
                AreaId = vm.AreaId,
                IsActive = vm.IsActive
            };

            var result = await _apiClient.UpdateBookAsync(id, dto);
            if (result.Success)
            {
                TempData["Success"] = $"Cập nhật sách \"{dto.Title}\" thành công!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = result.Message ?? "Cập nhật sách thất bại.";
            vm.IsEditMode = true;
            await PopulateDropdownsAsync(vm);
            return View(vm);
        }

        /// <summary>
        /// Ẩn sách (Soft delete IsActive = false)
        /// Phân quyền: Chỉ ADMIN mới có quyền ẩn sách
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _apiClient.DeleteBookAsync(id);
            if (result.Success)
            {
                TempData["Success"] = "Đã ẩn sách khỏi hệ thống thành công!";
            }
            else
            {
                TempData["Error"] = result.Message ?? "Không thể ẩn sách.";
            }

            return RedirectToAction(nameof(Index));
        }

        #region Helpers

        private async Task PopulateDropdownsAsync(BookFormViewModel vm)
        {
            var categoriesResult = await _apiClient.GetCategoriesAsync();
            if (categoriesResult.Success && categoriesResult.Data != null)
            {
                vm.CategoryOptions = categoriesResult.Data.Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.Name,
                    Selected = c.CategoryId == vm.CategoryId
                }).ToList();
            }

            var areasResult = await _apiClient.GetAreasAsync();
            if (areasResult.Success && areasResult.Data != null)
            {
                vm.AreaOptions = areasResult.Data.Select(a => new SelectListItem
                {
                    Value = a.AreaId.ToString(),
                    Text = a.Name,
                    Selected = a.AreaId == vm.AreaId
                }).ToList();
            }
        }

        #endregion
    }
}
