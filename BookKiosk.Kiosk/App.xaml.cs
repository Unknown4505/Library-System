using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using BookKiosk.Kiosk.Services.Api;
using BookKiosk.Kiosk.Services.Hardware;

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
                // Khi lên máy Kiosk thật, chỉ cần đổi thành <IBarcodeScanner, RealScanner>()
                services.AddSingleton<IBarcodeScanner, MockBarcodeScanner>();
                services.AddSingleton<IReceiptPrinter, MockReceiptPrinter>();

                // Đăng ký UI Windows/Pages
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();

        var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();
        
        base.OnExit(e);
    }
}

