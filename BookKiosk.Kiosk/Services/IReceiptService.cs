using System.Collections.Generic;
using System.Threading.Tasks;
using BookKiosk.Kiosk.Models;

namespace BookKiosk.Kiosk.Services
{
    public interface IReceiptService
    {
        Task<string> GenerateReceiptPdfAsync(IEnumerable<CartItemModel> cartItems, decimal totalAmount, int orderId = 0);
    }
}
