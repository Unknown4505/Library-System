using Microsoft.AspNetCore.Authentication.Cookies;
using BookKiosk.CMS.Services;
using BookKiosk.CMS.Services.Handlers;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers with Views
builder.Services.AddControllersWithViews();

// 2. Add HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// 3. Register DelegatingHandler
builder.Services.AddTransient<JwtDelegatingHandler>();

// 4. Kiểm tra an toàn cờ Mock & Đăng ký Typed HttpClient cho API Backend & POS
var useMockApi = builder.Configuration.GetValue<bool>("Cms:UseMockApi");
if (!builder.Environment.IsDevelopment() && useMockApi)
{
    throw new InvalidOperationException("NGUY HIỂM: Không được phép bật cờ Cms:UseMockApi trên môi trường ngoài Development (Production/Staging/Release)!");
}

var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7111";

builder.Services.AddHttpClient<ICmsApiClient, CmsApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<JwtDelegatingHandler>();

if (useMockApi)
{
    builder.Services.AddSingleton<IPosApi, MockPosApi>();
    builder.Services.AddSingleton<IOrderApi, MockOrderApi>();
    builder.Services.AddSingleton<IReportApi, MockReportApi>();
}
else
{
    builder.Services.AddHttpClient<IPosApi, CmsPosApi>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<JwtDelegatingHandler>();

    builder.Services.AddHttpClient<IOrderApi, CmsOrderApi>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<JwtDelegatingHandler>();

    builder.Services.AddHttpClient<IReportApi, CmsReportApi>(client =>
    {
        client.BaseAddress = new Uri(apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<JwtDelegatingHandler>();
}

// 5. Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "BookKiosk.CMS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
