using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using BookKiosk.Kiosk.Services.Api;
using BookKiosk.Kiosk.Services.Hardware;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.ViewModels;
using BookKiosk.Kiosk.Pages;
using QuestPDF.Infrastructure;

namespace BookKiosk.Kiosk;

public partial class App : System.Windows.Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var env = context.HostingEnvironment.EnvironmentName;
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                      .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;

                // 2. ApiClient: Chọn Mock hay Real dựa trên cấu hình (Mặc định Production an toàn: false)
                bool useMockApi = configuration.GetValue<bool>("KioskSettings:UseMockApi", false)
                               || configuration.GetValue<bool>("UseMockApi", false);
                if (useMockApi)
                {
                    // DEV/MOCK MODE có chủ đích: Sử dụng Mock API client
                    services.AddSingleton<IBookKioskApiClient, MockBookKioskApiClient>();
                }
                else
                {
                    // PRODUCTION MODE (Fail-Closed): Cấu hình HttpClientFactory thuần túy kết nối Backend thật
                    services.AddHttpClient<IBookKioskApiClient, BookKioskApiClient>(client =>
                    {
                        client.BaseAddress = new Uri(configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7111/");
                        client.Timeout = TimeSpan.FromSeconds(30);
                        client.DefaultRequestHeaders.Add("X-Api-Key", configuration["ApiSettings:ApiKey"] ?? "kiosk-secret-key");
                    });
                }

                // 3. Hardware Scanner Service: Chọn Mock hay Real dựa trên ScannerMode (Mặc định Production an toàn: "Auto")
                string scannerMode = configuration["KioskSettings:ScannerMode"]
                                  ?? configuration["ScannerMode"]
                                  ?? "Auto";
                if (string.Equals(scannerMode, "Mock", StringComparison.OrdinalIgnoreCase))
                {
                    services.AddSingleton<IBarcodeScanner, MockBarcodeScanner>();
                }
                else
                {
                    // Production / Auto / Usb / Camera: Sử dụng KioskBarcodeScanner thật (không mock, không tự fallback)
                    services.AddSingleton<KioskBarcodeScanner>();
                    services.AddSingleton<IBarcodeScanner>(sp => sp.GetRequiredService<KioskBarcodeScanner>());
                }

                // Hardware Printer: Mô phỏng hardware có chủ đích khi chưa có driver máy in nhiệt thật
                // (Module 8, Task 8.5 thuộc trách nhiệm Leader; cần Leader xác nhận và bàn giao driver ESC/POS).
                services.AddSingleton<IReceiptPrinter, MockReceiptPrinter>();

                // Đăng ký Core Services cho Kiosk
                services.AddSingleton<NavigationService>();
                services.AddSingleton<IdleTimerService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<CartService>();
                services.AddSingleton<CartViewModel>();
                services.AddSingleton<HeartbeatWorker>();
                services.AddSingleton<IReceiptService, ReceiptPdfService>();
                services.AddSingleton<AdvertisingService>();

                // Đăng ký Windows/Pages
                services.AddSingleton<MainWindow>();
                services.AddTransient<IdlePage>();
                services.AddTransient<SearchPage>();
                services.AddTransient<BookDetailPage>();
                services.AddTransient<MemberPage>();
                services.AddTransient<CheckoutPage>();
                services.AddTransient<ReceiptPage>();

                // Đăng ký ViewModels
                services.AddTransient<IdleViewModel>();
                services.AddTransient<SearchViewModel>();
                services.AddTransient<BookDetailViewModel>();
                services.AddTransient<MemberViewModel>();
                services.AddTransient<CheckoutViewModel>();
                services.AddTransient<ReceiptViewModel>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        await AppHost!.StartAsync();

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Điều hướng trang ban đầu sau khi load MainWindow
        var navigationService = AppHost.Services.GetRequiredService<NavigationService>();
        navigationService.Navigate<IdlePage>();

        // Khởi động Heartbeat Worker
        var heartbeatWorker = AppHost.Services.GetRequiredService<HeartbeatWorker>();
        heartbeatWorker.Start();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();

        base.OnExit(e);
    }
}
