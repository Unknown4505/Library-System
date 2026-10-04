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

namespace BookKiosk.Kiosk;

public partial class App : System.Windows.Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                var configuration = context.Configuration;
                
                // Cấu hình HttpClientFactory thuần túy với appsettings.json
                services.AddHttpClient<IBookKioskApiClient, BookKioskApiClient>(client =>
                {
                    client.BaseAddress = new Uri(configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7111/");
                    client.Timeout = TimeSpan.FromSeconds(30);
                    // Header xác thực Kiosk
                    client.DefaultRequestHeaders.Add("X-Api-Key", configuration["ApiSettings:ApiKey"] ?? "kiosk-secret-key");
                });

                // Đăng ký Hardware Mock Services (Dùng cho Laptop Dev)
                services.AddSingleton<IBarcodeScanner, MockBarcodeScanner>();
                services.AddSingleton<IReceiptPrinter, MockReceiptPrinter>();
                services.AddSingleton<IBarcodeScannerService, MockBarcodeScannerService>();

                // Đăng ký Core Services cho Kiosk
                services.AddSingleton<NavigationService>();
                services.AddSingleton<IdleTimerService>();
                services.AddSingleton<IBookService, MockBookService>();
                services.AddSingleton<CartService>();
                services.AddSingleton<CartViewModel>();

                // Đăng ký Windows/Pages
                services.AddSingleton<MainWindow>();
                services.AddTransient<IdlePage>();
                services.AddTransient<HomePage>();
                services.AddTransient<SearchPage>();
                services.AddTransient<BookDetailPage>();
                services.AddTransient<MemberPage>();
                services.AddTransient<CheckoutPage>();
                services.AddTransient<ReceiptPage>();

                // Đăng ký ViewModels
                services.AddTransient<IdleViewModel>();
                services.AddTransient<HomePageViewModel>();
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
        await AppHost!.StartAsync();

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        
        // Điều hướng trang ban đầu sau khi load MainWindow
        var navigationService = AppHost.Services.GetRequiredService<NavigationService>();
        navigationService.Navigate<IdlePage>();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();
        
        base.OnExit(e);
    }
}
