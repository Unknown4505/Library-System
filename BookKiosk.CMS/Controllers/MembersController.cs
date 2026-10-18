using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using BookKiosk.CMS.Models.Members;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.ViewModels.Members;

namespace BookKiosk.CMS.Controllers
{
    [Authorize]
    public class MembersController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<MembersController> _logger;
        private static readonly Regex VietnamPhoneRegex = new(@"^(03|05|07|08|09)\d{8}$", RegexOptions.Compiled);

        public MembersController(ICmsApiClient apiClient, ILogger<MembersController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        /// <summary>
        /// Màn hình tra cứu nhanh Khách hàng thành viên bằng Số điện thoại
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? phone = null)
        {
            var vm = new MemberIndexViewModel
            {
                PhoneNumber = phone?.Trim()
            };

            if (string.IsNullOrWhiteSpace(vm.PhoneNumber))
            {
                return View(vm);
            }

            vm.IsSearched = true;

            var result = await _apiClient.GetMemberByPhoneAsync(vm.PhoneNumber);
            if (result.Success && result.Data != null)
            {
                vm.Member = result.Data;
                vm.IsNotFound = false;

                // Tải lịch sử biến động điểm gần nhất
                var historyResult = await _apiClient.GetPointHistoryAsync(result.Data.MemberId);
                if (historyResult.Success && historyResult.Data != null)
                {
                    vm.RecentHistory = historyResult.Data
                        .OrderByDescending(h => h.CreatedAt)
                        .Take(10)
                        .ToList();
                }
            }
            else
            {
                vm.IsNotFound = true;
                vm.Message = result.Message ?? "Không tìm thấy thông tin thành viên với số điện thoại này.";
            }

            return View(vm);
        }

        /// <summary>
        /// Màn hình Đăng ký Thành viên mới (Dùng cho cả Admin và Staff thu ngân)
        /// </summary>
        [HttpGet]
        public IActionResult Create(string? phone = null)
        {
            var vm = new MemberFormViewModel
            {
                PhoneNumber = phone?.Trim() ?? string.Empty
            };

            return View(vm);
        }

        /// <summary>
        /// Xử lý Đăng ký Thành viên mới
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MemberFormViewModel vm)
        {
            if (string.IsNullOrWhiteSpace(vm.PhoneNumber) || !VietnamPhoneRegex.IsMatch(vm.PhoneNumber.Trim()))
            {
                ModelState.AddModelError(nameof(vm.PhoneNumber), "Số điện thoại không đúng định dạng di động Việt Nam (10 số, bắt đầu bằng 03, 05, 07, 08, 09)");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var cleanPhone = vm.PhoneNumber.Trim();
            var dto = new CreateMemberDto
            {
                PhoneNumber = cleanPhone,
                FullName = vm.FullName.Trim()
            };

            var result = await _apiClient.CreateMemberAsync(dto);
            if (result.Success && result.Data != null)
            {
                TempData["ToastMessage"] = $"Đăng ký thành viên '{result.Data.FullName}' ({cleanPhone}) thành công!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Index), new { phone = cleanPhone });
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể đăng ký thành viên. Vui lòng kiểm tra lại số điện thoại.");
            return View(vm);
        }

        /// <summary>
        /// Màn hình Cập nhật thông tin Thành viên (Chỉ sửa Họ tên, SĐT và Điểm giữ nguyên)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Edit(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return RedirectToAction(nameof(Index));
            }

            var result = await _apiClient.GetMemberByPhoneAsync(phone.Trim());
            if (!result.Success || result.Data == null)
            {
                TempData["ToastMessage"] = "Không tìm thấy thông tin thành viên để chỉnh sửa.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var member = result.Data;
            var vm = new MemberFormViewModel
            {
                MemberId = member.MemberId,
                PhoneNumber = member.PhoneNumber,
                FullName = member.FullName,
                Points = member.Points
            };

            return View(vm);
        }

        /// <summary>
        /// Xử lý Cập nhật thông tin Thành viên
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, MemberFormViewModel vm)
        {
            if (id != vm.MemberId)
            {
                TempData["ToastMessage"] = "Mã định danh thành viên không khớp.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(vm.FullName))
            {
                ModelState.AddModelError(nameof(vm.FullName), "Họ và tên thành viên không được để trống.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var dto = new UpdateMemberDto
            {
                MemberId = id,
                PhoneNumber = vm.PhoneNumber,
                FullName = vm.FullName.Trim()
            };

            var result = await _apiClient.UpdateMemberAsync(id, dto);
            if (result.Success)
            {
                TempData["ToastMessage"] = $"Cập nhật họ tên thành viên '{dto.FullName}' thành công!";
                TempData["ToastType"] = "success";
                return RedirectToAction(nameof(Details), new { phone = vm.PhoneNumber });
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể cập nhật thông tin thành viên.");
            return View(vm);
        }

        /// <summary>
        /// Màn hình Chi tiết Thẻ thành viên và Toàn bộ Lịch sử biến động điểm
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(string phone, string? filter = "all")
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return RedirectToAction(nameof(Index));
            }

            var memberResult = await _apiClient.GetMemberByPhoneAsync(phone.Trim());
            if (!memberResult.Success || memberResult.Data == null)
            {
                TempData["ToastMessage"] = "Không tìm thấy thông tin thành viên.";
                TempData["ToastType"] = "error";
                return RedirectToAction(nameof(Index));
            }

            var member = memberResult.Data;
            var vm = new MemberDetailsViewModel
            {
                Member = member,
                FilterType = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim().ToLower()
            };

            // Lấy toàn bộ lịch sử điểm
            var historyResult = await _apiClient.GetPointHistoryAsync(member.MemberId);
            var allHistory = historyResult.Success && historyResult.Data != null 
                ? historyResult.Data.OrderByDescending(h => h.CreatedAt).ToList() 
                : new List<PointTransactionDto>();

            vm.TotalEarnedPoints = allHistory.Where(h => h.Type == 1).Sum(h => h.Points);
            vm.TotalRedeemedPoints = allHistory.Where(h => h.Type == 2).Sum(h => h.Points);

            // Lọc theo loại giao dịch
            vm.PointHistory = vm.FilterType switch
            {
                "earned" => allHistory.Where(h => h.Type == 1).ToList(),
                "redeemed" => allHistory.Where(h => h.Type == 2).ToList(),
                _ => allHistory
            };

            return View(vm);
        }
    }
}
