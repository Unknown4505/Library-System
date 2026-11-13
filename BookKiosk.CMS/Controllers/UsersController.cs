using BookKiosk.Application.DTOs.Users;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Users;
using BookKiosk.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.CMS.Controllers;

[Authorize(Roles = "Admin")]
[Route("Users")]
public class UsersController : Controller
{
    private readonly ICmsApiClient _apiClient;
    private readonly IConfiguration _configuration;

    public UsersController(ICmsApiClient apiClient, IConfiguration configuration)
    {
        _apiClient = apiClient;
        _configuration = configuration;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? search = null, string? role = null, bool? isActive = null, int page = 1)
    {
        page = Math.Max(page, 1);
        var listTask = _apiClient.GetUsersAsync(page, 10, search, role, isActive);
        var totalTask = _apiClient.GetUsersAsync(1, 1);
        var adminTask = _apiClient.GetUsersAsync(1, 1, role: "Admin");
        var managerTask = _apiClient.GetUsersAsync(1, 1, role: "Manager");
        var staffTask = _apiClient.GetUsersAsync(1, 1, role: "Staff");
        await Task.WhenAll(listTask, totalTask, adminTask, managerTask, staffTask);

        var list = await listTask;
        var viewModel = new UserManagementViewModel
        {
            UseMockApi = _configuration.GetValue<bool>("Cms:UseMockApi"),
            IsBackendAvailable = list.Success && list.Data != null,
            SearchKeyword = search?.Trim(),
            SelectedRole = role?.Trim(),
            SelectedStatus = isActive,
            Users = list.Data ?? new(),
            TotalUsers = (await totalTask).Data?.TotalCount ?? 0,
            AdminCount = (await adminTask).Data?.TotalCount ?? 0,
            ManagerCount = (await managerTask).Data?.TotalCount ?? 0,
            StaffCount = (await staffTask).Data?.TotalCount ?? 0,
            StatusMessage = list.Success ? string.Empty : list.Message ?? "Không thể tải danh sách tài khoản."
        };

        return View(viewModel);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserDto dto)
    {
        if (!ModelState.IsValid)
            return RedirectWithValidationErrors();

        SetResultMessage(await _apiClient.CreateUserAsync(dto), "Đã tạo tài khoản.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string fullName, UserRole role)
    {
        var dto = new UpdateUserDto { FullName = fullName?.Trim() ?? string.Empty, Role = role };
        if (!TryValidateModel(dto))
            return RedirectWithValidationErrors();

        SetResultMessage(await _apiClient.UpdateUserAsync(id, dto), "Đã cập nhật tài khoản.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int id, bool isActive)
    {
        SetResultMessage(await _apiClient.UpdateUserStatusAsync(id, isActive),
            isActive ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ResetPassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(int id, string newPassword, string confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            ModelState.AddModelError(nameof(newPassword), "Mật khẩu mới phải có tối thiểu 8 ký tự.");
        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            ModelState.AddModelError(nameof(confirmPassword), "Mật khẩu xác nhận không khớp.");
        if (!ModelState.IsValid)
            return RedirectWithValidationErrors();

        SetResultMessage(await _apiClient.ResetUserPasswordAsync(id, newPassword), "Đã đặt lại mật khẩu.");
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectWithValidationErrors()
    {
        TempData["Error"] = string.Join(" ", ModelState.Values
            .SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage));
        return RedirectToAction(nameof(Index));
    }

    private void SetResultMessage<T>(BookKiosk.CMS.Models.Common.ApiResult<T> result, string successMessage)
    {
        TempData[result.Success ? "Success" : "Error"] =
            result.Success ? successMessage : result.Message ?? "Thao tác không thành công.";
    }
}
