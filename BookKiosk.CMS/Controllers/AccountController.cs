using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BookKiosk.CMS.Models.Auth;
using BookKiosk.CMS.Services;

namespace BookKiosk.CMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly ICmsApiClient _apiClient;
        private readonly ILogger<AccountController> _logger;

        public AccountController(ICmsApiClient apiClient, ILogger<AccountController> logger)
        {
            _apiClient = apiClient;
            _logger = logger;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            // Đã đăng nhập rồi → chuyển thẳng vào hệ thống
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginRequestModel());
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequestModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _apiClient.LoginAsync(model);

            if (!result.Success || result.Data == null)
            {
                // Không lộ tài khoản tồn tại hay không — luôn dùng cùng thông báo
                ModelState.AddModelError(string.Empty, result.Message ?? "Tên đăng nhập hoặc mật khẩu không đúng.");
                return View(model);
            }

            var loginData = result.Data;

            // Tính thời hạn sống của Cookie theo expiresAt từ API
            var cookieExpiry = loginData.ExpiresAt > DateTime.UtcNow
                ? loginData.ExpiresAt
                : DateTime.UtcNow.AddHours(8);

            // Xây dựng Claims Identity
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, loginData.FullName),
                new(ClaimTypes.NameIdentifier, model.Username),
                new(ClaimTypes.Role, loginData.Role),
                // Đính kèm access_token vào Claim để JwtDelegatingHandler trích xuất
                new("access_token", loginData.AccessToken),
                new("token_expiry", loginData.ExpiresAt.ToString("O"))
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = cookieExpiry,
                AllowRefresh = true
            };

            // Lưu access_token vào Authentication Properties để GetTokenAsync lấy được
            authProps.StoreTokens(new[]
            {
                new AuthenticationToken { Name = "access_token", Value = loginData.AccessToken },
                new AuthenticationToken { Name = "expires_at",   Value = loginData.ExpiresAt.ToString("O") }
            });

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

            _logger.LogInformation("Người dùng '{Username}' (vai trò: {Role}) đã đăng nhập thành công.", model.Username, loginData.Role);

            return RedirectToLocal(returnUrl);
        }

        // POST: /Account/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var username = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Unknown";
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Người dùng '{Username}' đã đăng xuất.", username);
            return RedirectToAction("Login", "Account");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // Helper: Chống Open Redirect
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            // Phân quyền điều hướng theo Role
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Home");
            }

            // Staff → vào thẳng POS
            return RedirectToAction("Index", "Pos");
        }
    }
}
