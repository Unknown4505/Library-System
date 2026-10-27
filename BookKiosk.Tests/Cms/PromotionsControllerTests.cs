using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using BookKiosk.CMS.Controllers;
using BookKiosk.CMS.Models.Catalog;
using BookKiosk.CMS.Models.Common;
using BookKiosk.CMS.Models.Promotions;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Promotions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    public class PromotionsControllerTests
    {
        private readonly Mock<ICmsApiClient> _apiClientMock;
        private readonly Mock<ILogger<PromotionsController>> _loggerMock;
        private readonly TempDataDictionary _tempData;

        public PromotionsControllerTests()
        {
            _apiClientMock = new Mock<ICmsApiClient>();
            _loggerMock = new Mock<ILogger<PromotionsController>>();
            var httpContext = new DefaultHttpContext();
            _tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        private PromotionsController CreateController(string role = "Admin")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = principal };
            var controller = new PromotionsController(_apiClientMock.Object, _loggerMock.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                },
                TempData = _tempData
            };
            return controller;
        }

        #region 1. RBAC Tests

        [Fact]
        public void Create_StaffRole_RedirectsToIndexWithToastError()
        {
            var controller = CreateController("Staff");

            var result = controller.Create() as RedirectToActionResult;

            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Index");
            controller.TempData["ToastType"].Should().Be("error");
        }

        [Fact]
        public void Create_AdminRole_ReturnsViewWithFormViewModel()
        {
            var controller = CreateController("Admin");

            var result = controller.Create() as ViewResult;

            result.Should().NotBeNull();
            var model = result!.Model as PromotionFormViewModel;
            model.Should().NotBeNull();
            model!.PromotionType.Should().Be(1);
            model.OrderMinOrderValue.Should().Be(0);
            model.OrderDiscountAmount.Should().Be(10000);
        }

        [Fact]
        public async Task Edit_StaffRole_RedirectsToIndexWithToastError()
        {
            var controller = CreateController("Staff");

            var result = await controller.Edit(1) as RedirectToActionResult;

            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Index");
            controller.TempData["ToastType"].Should().Be("error");
        }

        [Fact]
        public async Task ToggleActive_StaffRole_ReturnsForbiddenJson()
        {
            var controller = CreateController("Staff");

            var result = await controller.ToggleActive(1, true) as JsonResult;

            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result!.Value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            json.Should().Contain("\"success\":false");
            json.Should().Contain("Bạn không có quyền");
        }

        #endregion

        #region 2. Validation Tests

        [Fact]
        public async Task Create_EndDateBeforeStartDate_ReturnsViewWithModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi test",
                PromotionType = 1,
                StartDate = DateTime.Today.AddDays(5),
                EndDate = DateTime.Today.AddDays(2), // Lỗi: kết thúc trước bắt đầu
                OrderMinOrderValue = 0,
                OrderDiscountAmount = 10000
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ContainsKey(nameof(vm.EndDate)).Should().BeTrue();
            _apiClientMock.Verify(a => a.CreatePromotionAsync(It.IsAny<CreatePromotionDto>()), Times.Never);
        }

        [Fact]
        public async Task Create_OrderDiscount_NegativeMinOrderValue_ReturnsModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi đơn hàng âm",
                PromotionType = 1,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(10),
                OrderMinOrderValue = -5000,
                OrderDiscountAmount = 10000
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ContainsKey(nameof(vm.OrderMinOrderValue)).Should().BeTrue();
        }

        [Fact]
        public async Task Create_OrderDiscount_ZeroOrNegativeDiscountAmount_ReturnsModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi 0đ",
                PromotionType = 1,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(10),
                OrderMinOrderValue = 100000,
                OrderDiscountAmount = 0 // Lỗi: <= 0
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ContainsKey(nameof(vm.OrderDiscountAmount)).Should().BeTrue();
        }

        [Fact]
        public async Task Create_OrderDiscount_NonIntegerVnd_ReturnsModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi số lẻ",
                PromotionType = 1,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(10),
                OrderMinOrderValue = 100000,
                OrderDiscountAmount = 10000.5m // Lỗi: số thập phân
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState.ContainsKey(nameof(vm.OrderDiscountAmount)).Should().BeTrue();
        }

        [Fact]
        public async Task Create_ProductDiscount_EmptyBooks_ReturnsModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi rỗng sách",
                PromotionType = 2,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(10),
                ProductDiscounts = new List<ProductDiscountItemViewModel>()
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
        }

        [Fact]
        public async Task Create_ProductDiscount_DuplicateBookId_ReturnsModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi trùng sách",
                PromotionType = 2,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(10),
                ProductDiscounts = new List<ProductDiscountItemViewModel>
                {
                    new ProductDiscountItemViewModel { BookId = 10, DiscountAmount = 15000 },
                    new ProductDiscountItemViewModel { BookId = 10, DiscountAmount = 20000 } // Trùng BookId 10
                }
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
            _apiClientMock.Verify(a => a.CreatePromotionAsync(It.IsAny<CreatePromotionDto>()), Times.Never);
        }

        [Fact]
        public async Task Create_ProductDiscount_ZeroDiscountAmount_ReturnsModelError()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi sách 0đ",
                PromotionType = 2,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(10),
                ProductDiscounts = new List<ProductDiscountItemViewModel>
                {
                    new ProductDiscountItemViewModel { BookId = 12, DiscountAmount = 0 }
                }
            };

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            controller.ModelState.IsValid.Should().BeFalse();
        }

        #endregion

        #region 3. Success & Serialization Contract Tests

        [Fact]
        public async Task Create_OrderDiscount_Valid_SendsNumericType_AndOrderDiscountPayload()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi đơn hàng 2026",
                Description = "Giảm 20k cho đơn từ 200k",
                PromotionType = 1,
                StartDate = new DateTime(2026, 11, 1),
                EndDate = new DateTime(2026, 11, 7),
                OrderMinOrderValue = 200000,
                OrderDiscountAmount = 20000,
                IsActive = true
            };

            CreatePromotionDto? capturedDto = null;
            _apiClientMock
                .Setup(a => a.CreatePromotionAsync(It.IsAny<CreatePromotionDto>()))
                .Callback<CreatePromotionDto>(dto => capturedDto = dto)
                .ReturnsAsync(ApiResult<PromotionDto>.Ok(new PromotionDto { PromotionId = 101, Name = vm.Name }));

            var result = await controller.Create(vm) as RedirectToActionResult;

            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Index");
            capturedDto.Should().NotBeNull();
            capturedDto!.PromotionType.Should().Be(1);
            capturedDto.OrderDiscount.Should().NotBeNull();
            capturedDto.OrderDiscount!.MinOrderValue.Should().Be(200000);
            capturedDto.OrderDiscount.DiscountAmount.Should().Be(20000);
            capturedDto.ProductDiscounts.Should().BeEmpty();
            capturedDto.StartDate.Kind.Should().Be(DateTimeKind.Utc);
            capturedDto.EndDate.Kind.Should().Be(DateTimeKind.Utc);
        }

        [Fact]
        public async Task Create_ProductDiscount_Valid_SendsProductDiscountsArray_AndNullOrderDiscount()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Tuần lễ sách Việt",
                Description = "Giảm theo đầu sách",
                PromotionType = 2,
                StartDate = new DateTime(2026, 11, 1),
                EndDate = new DateTime(2026, 11, 7),
                IsActive = true,
                ProductDiscounts = new List<ProductDiscountItemViewModel>
                {
                    new ProductDiscountItemViewModel { BookId = 12, DiscountAmount = 15000 },
                    new ProductDiscountItemViewModel { BookId = 18, DiscountAmount = 20000 }
                }
            };

            CreatePromotionDto? capturedDto = null;
            _apiClientMock
                .Setup(a => a.CreatePromotionAsync(It.IsAny<CreatePromotionDto>()))
                .Callback<CreatePromotionDto>(dto => capturedDto = dto)
                .ReturnsAsync(ApiResult<PromotionDto>.Ok(new PromotionDto { PromotionId = 102, Name = vm.Name }));

            var result = await controller.Create(vm) as RedirectToActionResult;

            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Index");
            capturedDto.Should().NotBeNull();
            capturedDto!.PromotionType.Should().Be(2);
            capturedDto.OrderDiscount.Should().BeNull();
            capturedDto.ProductDiscounts.Should().HaveCount(2);
            capturedDto.ProductDiscounts[0].BookId.Should().Be(12);
            capturedDto.ProductDiscounts[0].DiscountAmount.Should().Be(15000);
            capturedDto.ProductDiscounts[1].BookId.Should().Be(18);
            capturedDto.ProductDiscounts[1].DiscountAmount.Should().Be(20000);
            capturedDto.StartDate.Kind.Should().Be(DateTimeKind.Utc);
            capturedDto.EndDate.Kind.Should().Be(DateTimeKind.Utc);
        }

        #endregion

        #region 4. Replace-All Semantics & Stale State Cleaning

        [Fact]
        public async Task Edit_ProductDiscount_ReplaceAll_SendsNewTargetStateExclusively()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                PromotionId = 5,
                Name = "Cập nhật giảm giá sách",
                PromotionType = 2,
                StartDate = new DateTime(2026, 11, 1),
                EndDate = new DateTime(2026, 11, 7),
                IsActive = true,
                // Ban đầu có sách #12 và #18, người dùng xóa sách #12 và thêm sách #25
                ProductDiscounts = new List<ProductDiscountItemViewModel>
                {
                    new ProductDiscountItemViewModel { BookId = 18, DiscountAmount = 25000 },
                    new ProductDiscountItemViewModel { BookId = 25, DiscountAmount = 30000 }
                }
            };

            UpdatePromotionDto? capturedUpdateDto = null;
            _apiClientMock
                .Setup(a => a.UpdatePromotionAsync(5, It.IsAny<UpdatePromotionDto>()))
                .Callback<int, UpdatePromotionDto>((id, dto) => capturedUpdateDto = dto)
                .ReturnsAsync(ApiResult<PromotionDto>.Ok(new PromotionDto { PromotionId = 5, Name = vm.Name }));

            var result = await controller.Edit(5, vm) as RedirectToActionResult;

            result.Should().NotBeNull();
            result!.ActionName.Should().Be("Index");
            capturedUpdateDto.Should().NotBeNull();
            capturedUpdateDto!.ProductDiscounts.Should().HaveCount(2);
            // Sách #12 bị xóa hoàn toàn khỏi payload replace-all
            capturedUpdateDto.ProductDiscounts.Any(x => x.BookId == 12).Should().BeFalse();
            capturedUpdateDto.ProductDiscounts[0].BookId.Should().Be(18);
            capturedUpdateDto.ProductDiscounts[1].BookId.Should().Be(25);
        }

        #endregion

        #region 5. Error Recovery & Keep Form Data (400, 409)

        [Fact]
        public async Task Create_BackendReturns400BadRequest_PreservesFormDataAndDoesNotRedirect()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi lỗi Backend",
                PromotionType = 2,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                ProductDiscounts = new List<ProductDiscountItemViewModel>
                {
                    new ProductDiscountItemViewModel { BookId = 9999, DiscountAmount = 10000 }
                }
            };

            // Backend trả lỗi 400 sách không tồn tại
            _apiClientMock
                .Setup(a => a.CreatePromotionAsync(It.IsAny<CreatePromotionDto>()))
                .ReturnsAsync(ApiResult<PromotionDto>.Fail("Sách với ID 9999 không tồn tại trong hệ thống.", 400));

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            var returnedModel = result!.Model as PromotionFormViewModel;
            returnedModel.Should().NotBeNull();
            returnedModel!.Name.Should().Be("Khuyến mãi lỗi Backend");
            returnedModel.ProductDiscounts.Should().HaveCount(1);
            returnedModel.ProductDiscounts[0].BookId.Should().Be(9999);
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState[string.Empty]!.Errors[0].ErrorMessage.Should().Contain("9999 không tồn tại");
        }

        [Fact]
        public async Task Create_BackendReturns409Conflict_PreservesFormDataAndDoesNotRedirect()
        {
            var controller = CreateController("Admin");
            var vm = new PromotionFormViewModel
            {
                Name = "Khuyến mãi xung đột 409",
                PromotionType = 1,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                OrderMinOrderValue = 100000,
                OrderDiscountAmount = 20000
            };

            _apiClientMock
                .Setup(a => a.CreatePromotionAsync(It.IsAny<CreatePromotionDto>()))
                .ReturnsAsync(ApiResult<PromotionDto>.Fail("Chương trình khuyến mãi bị trùng lặp thời gian hoặc điều kiện.", 409));

            var result = await controller.Create(vm) as ViewResult;

            result.Should().NotBeNull();
            var returnedModel = result!.Model as PromotionFormViewModel;
            returnedModel.Should().NotBeNull();
            returnedModel!.Name.Should().Be("Khuyến mãi xung đột 409");
            controller.ModelState.IsValid.Should().BeFalse();
            controller.ModelState[string.Empty]!.Errors[0].ErrorMessage.Should().Contain("trùng lặp thời gian");
        }

        #endregion

        #region 6. TimeZone Round-Trip Tests (Vietnam Local <-> UTC)

        [Fact]
        public void TimeZone_RoundTrip_VietnamLocalToUtc_AndUtcToVietnamLocal_PreservesDate()
        {
            // Người dùng tại Việt Nam nhập ngày 2026-11-01
            var inputDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Unspecified);

            // Chuyển sang UTC tường minh
            var utcStart = PromotionsController.ConvertVietnamLocalToUtc(inputDate, isEndOfDay: false);
            utcStart.Kind.Should().Be(DateTimeKind.Utc);
            // 2026-11-01 00:00:00 (UTC+7) tương ứng với 2026-10-31 17:00:00 UTC
            utcStart.Hour.Should().Be(17);
            utcStart.Day.Should().Be(31);

            // Khi load lại form từ Backend, chuyển từ UTC về lại giờ Việt Nam
            var localReloaded = PromotionsController.ConvertUtcToVietnamLocal(utcStart);
            localReloaded.Year.Should().Be(2026);
            localReloaded.Month.Should().Be(11);
            localReloaded.Day.Should().Be(1);
            localReloaded.Hour.Should().Be(0);
        }

        [Fact]
        public async Task TimeZone_EditRoundTrip_WithBackendExactDates_PreservesExactUtcTimestamps()
        {
            var controller = CreateController("Admin");
            var originalStartDateUtcString = "2026-11-01T00:00:00Z";
            var originalEndDateUtcString = "2026-11-07T23:59:59Z";

            var backendPromo = new PromotionDto
            {
                PromotionId = 55,
                Name = "Khuyến mãi kiểm thử múi giờ",
                PromotionType = 1,
                StartDate = DateTime.Parse(originalStartDateUtcString, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                EndDate = DateTime.Parse(originalEndDateUtcString, null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                IsActive = true,
                OrderDiscount = new PromotionOrderDiscountDto { MinOrderValue = 100000, DiscountAmount = 20000 }
            };

            _apiClientMock
                .Setup(a => a.GetPromotionByIdAsync(55))
                .ReturnsAsync(ApiResult<PromotionDto>.Ok(backendPromo));

            // 1. GET Edit: Controller chuyển đổi sang ViewModel
            var getResult = await controller.Edit(55) as ViewResult;
            getResult.Should().NotBeNull();
            var editVm = getResult!.Model as PromotionFormViewModel;
            editVm.Should().NotBeNull();
            editVm!.OriginalStartDateUtc.Should().Be(originalStartDateUtcString);
            editVm.OriginalEndDateUtc.Should().Be(originalEndDateUtcString);

            // 2. POST Edit: Người dùng KHÔNG sửa trường ngày, chỉ sửa tên hoặc giữ nguyên
            UpdatePromotionDto? capturedDto = null;
            _apiClientMock
                .Setup(a => a.UpdatePromotionAsync(55, It.IsAny<UpdatePromotionDto>()))
                .Callback<int, UpdatePromotionDto>((_, dto) => capturedDto = dto)
                .ReturnsAsync(ApiResult<PromotionDto>.Ok(backendPromo));

            var postResult = await controller.Edit(55, editVm) as RedirectToActionResult;
            postResult.Should().NotBeNull();
            postResult!.ActionName.Should().Be("Index");

            capturedDto.Should().NotBeNull();
            // Khẳng định StartDate và EndDate gửi tới Backend phải khớp chính xác 100% với 2 mốc UTC ban đầu không lệch 1 giây
            capturedDto!.StartDate.Kind.Should().Be(DateTimeKind.Utc);
            capturedDto.EndDate.Kind.Should().Be(DateTimeKind.Utc);
            capturedDto.StartDate.ToString("yyyy-MM-ddTHH:mm:ssZ").Should().Be("2026-11-01T00:00:00Z");
            capturedDto.EndDate.ToString("yyyy-MM-ddTHH:mm:ssZ").Should().Be("2026-11-07T23:59:59Z");
        }

        #endregion

        #region 7. Book Search AJAX

        [Fact]
        public async Task SearchBooks_ReturnsPaginatedJsonResult()
        {
            var controller = CreateController("Admin");
            var paginated = new PaginatedResult<BookDto>
            {
                Page = 1,
                PageSize = 5,
                TotalCount = 1,
                TotalPages = 1,
                Items = new List<BookDto>
                {
                    new BookDto
                    {
                        BookId = 12,
                        Title = "Lập trình C# nâng cao",
                        Barcode = "8935074123456",
                        SellingPrice = 120000,
                        StockQuantity = 15,
                        Author = "Nguyễn Văn A"
                    }
                }
            };

            _apiClientMock
                .Setup(a => a.GetBooksAsync(1, 5, "C#", null, null))
                .ReturnsAsync(ApiResult<PaginatedResult<BookDto>>.Ok(paginated));

            var result = await controller.SearchBooks("C#", 1, 5) as JsonResult;

            result.Should().NotBeNull();
            var json = JsonSerializer.Serialize(result!.Value, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            json.Should().Contain("\"success\":true");
            json.Should().Contain("Lập trình C# nâng cao");
            json.Should().Contain("\"bookId\":12");
        }

        #endregion
    }
}
