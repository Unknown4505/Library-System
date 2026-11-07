using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Helpers;
using BookKiosk.CMS.ViewModels.Users;

namespace BookKiosk.CMS.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("Users")]
    public class UsersController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            IConfiguration configuration,
            ILogger<UsersController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index(string? search = null, string? role = null)
        {
            bool useMock = _configuration.GetValue<bool>("Cms:UseMockApi");

            var viewModel = new UserManagementViewModel
            {
                UseMockApi = useMock,
                SearchKeyword = search?.Trim(),
                SelectedRole = role?.Trim()
            };

            if (useMock)
            {
                // Dữ liệu mô phỏng để test layout FE khi bật cờ UseMockApi: true trong môi trường Dev
                viewModel.IsBackendAvailable = true;

                var mockUsers = new List<UserAccountItem>
                {
                    new()
                    {
                        UserId = "USR-001",
                        Username = "admin",
                        FullName = "Quản trị viên hệ thống",
                        Email = "admin@bookkiosk.vn",
                        Role = "Admin",
                        IsActive = true,
                        CreatedAt = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).AddMonths(-6)
                    },
                    new()
                    {
                        UserId = "USR-002",
                        Username = "manager01",
                        FullName = "Nguyễn Văn Quản Lý",
                        Email = "manager01@bookkiosk.vn",
                        Role = "Manager",
                        IsActive = true,
                        CreatedAt = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).AddMonths(-3)
                    },
                    new()
                    {
                        UserId = "USR-003",
                        Username = "staff01",
                        FullName = "Trần Thị Thu Ngân",
                        Email = "staff01@bookkiosk.vn",
                        Role = "Staff",
                        IsActive = true,
                        CreatedAt = DateTimeHelper.ToVietnamTime(DateTime.UtcNow).AddDays(-45)
                    }
                };

                // Lọc theo từ khóa tìm kiếm (Username, FullName, Email)
                var filtered = mockUsers.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(viewModel.SearchKeyword))
                {
                    var kw = viewModel.SearchKeyword;
                    filtered = filtered.Where(u =>
                        u.Username.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        u.FullName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        u.Email.Contains(kw, StringComparison.OrdinalIgnoreCase));
                }

                // Lọc theo Role
                if (!string.IsNullOrWhiteSpace(viewModel.SelectedRole))
                {
                    filtered = filtered.Where(u => u.Role.Equals(viewModel.SelectedRole, StringComparison.OrdinalIgnoreCase));
                }

                var list = filtered.ToList();
                viewModel.Users = list;
                viewModel.TotalUsers = mockUsers.Count;
                viewModel.AdminCount = mockUsers.Count(u => u.Role == "Admin");
                viewModel.ManagerCount = mockUsers.Count(u => u.Role == "Manager");
                viewModel.StaffCount = mockUsers.Count(u => u.Role == "Staff");
            }
            else
            {
                // Khi chạy với Backend thật: Backend hiện chưa có endpoint /api/users (Docs 04 §3.8 ghi rõ PLANNED).
                // Theo đúng nguyên tắc: phần nào thiếu sẽ để trống chuẩn UI/UX, không hiển thị dữ liệu giả như dữ liệu thật.
                viewModel.IsBackendAvailable = false;
                viewModel.TotalUsers = 0;
                viewModel.AdminCount = 0;
                viewModel.ManagerCount = 0;
                viewModel.StaffCount = 0;
                viewModel.Users = new List<UserAccountItem>();
                viewModel.StatusMessage = "Backend API hiện chưa mở endpoint CRUD /api/users (theo Docs 04 §3.8 đang ở trạng thái PLANNED). Toàn bộ danh sách tài khoản người dùng đang để trống chờ Backend kết nối.";
            }

            return View(viewModel);
        }
    }
}
