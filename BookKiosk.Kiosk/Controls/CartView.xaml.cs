using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Controls
{
    public partial class CartView : UserControl
    {
        public CartView()
        {
            InitializeComponent();
            
            // Phân giải CartViewModel từ Service Provider để bind vào View
            if (App.AppHost != null)
            {
                DataContext = App.AppHost.Services.GetRequiredService<CartViewModel>();
            }
        }
    }
}
