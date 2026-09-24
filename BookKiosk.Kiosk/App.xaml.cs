using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BookKiosk.Kiosk.Services.Api;

namespace BookKiosk.Kiosk;

public partial class App : Application
{
    public static IHost? AppHost { get; private set; }

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Cấu hình HttpClientFactory thuần túy
                services.AddHttpClient<IBookKioskApiClient, BookKioskApiClient>(client =>
                {
                    client.BaseAddress = new Uri("https://localhost:5001/");
                    client.Timeout = TimeSpan.FromSeconds(30);
                    // Header xác thực Kiosk
                    client.DefaultRequestHeaders.Add("X-Api-Key", "kiosk-secret-key");
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

