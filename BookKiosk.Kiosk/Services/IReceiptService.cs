using System.Threading.Tasks;
using BookKiosk.Application.DTOs.Order;

namespace BookKiosk.Kiosk.Services
{
    public interface IReceiptService
    {
        /// <summary>
        /// Tạo file PDF hóa đơn chính thức từ dữ liệu ReceiptDto trả về bởi máy chủ.
        /// </summary>
        Task<string> GenerateReceiptPdfAsync(ReceiptDto receipt);
    }
}
