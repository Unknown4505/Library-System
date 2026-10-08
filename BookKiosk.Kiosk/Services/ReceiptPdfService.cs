using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BookKiosk.Kiosk.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookKiosk.Kiosk.Services
{
    public class ReceiptPdfService : IReceiptService
    {
        public Task<string> GenerateReceiptPdfAsync(IEnumerable<CartItemModel> cartItems, decimal totalAmount, int orderId = 0)
        {
            // Thay đổi thư mục lưu file thành thư mục nội bộ của App
            var receiptsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
            if (!Directory.Exists(receiptsFolder))
            {
                Directory.CreateDirectory(receiptsFolder);
            }

            var fileName = $"Receipt_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            var filePath = Path.Combine(receiptsFolder, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Khổ 80mm ~ 226 points
                    page.ContinuousSize(226, Unit.Point);
                    page.Margin(10, Unit.Point);
                    
                    // Cấu hình Fallback font an toàn chống lỗi ô vuông
                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily(Fonts.Arial)
                        .Fallback(f => f.FontFamily(Fonts.Tahoma).FontFamily(Fonts.TimesNewRoman)));

                    page.Header().Element(c => ComposeHeader(c, orderId));
                    page.Content().Element(c => ComposeContent(c, cartItems));
                    page.Footer().Element(f => ComposeFooter(f, totalAmount));
                });
            })
            .GeneratePdf(filePath);

            return Task.FromResult(filePath);
        }

        private void ComposeHeader(IContainer container, int orderId)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text("KIOSK SÁCH THÔNG MINH").Bold().FontSize(12);
                column.Item().AlignCenter().Text("123 Nguyễn Văn Cừ, Q.5, TP.HCM").FontSize(9);
                
                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                
                column.Item().AlignCenter().Text("HÓA ĐƠN THANH TOÁN").Bold().FontSize(11);
                
                // Đổi định dạng chống trùng lặp mã đơn
                var orderCode = orderId > 0 ? orderId.ToString() : DateTime.Now.ToString("yyMMddHHmmss");
                column.Item().Text($"Mã ĐH: #{orderCode}");
                column.Item().Text($"Ngày: {DateTime.Now:dd/MM/yyyy HH:mm}");
                
                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        }

        private void ComposeContent(IContainer container, IEnumerable<CartItemModel> cartItems)
        {
            container.PaddingVertical(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Tên
                    columns.RelativeColumn(1); // SL
                    columns.RelativeColumn(2); // Tiền
                });

                table.Header(header =>
                {
                    header.Cell().Text("Tên sách").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("SL").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("T.Tiền").Bold().FontSize(9);
                });

                foreach (var item in cartItems)
                {
                    table.Cell().Text(item.Book.Title).FontSize(9);
                    table.Cell().AlignRight().Text(item.Quantity.ToString()).FontSize(9);
                    table.Cell().AlignRight().Text($"{item.TotalPrice:N0} đ").FontSize(9);
                }
            });
        }

        private void ComposeFooter(IContainer container, decimal totalAmount)
        {
            container.Column(column =>
            {
                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                
                column.Item().AlignRight().Text($"Tổng tiền: {totalAmount:N0} đ").Bold().FontSize(11);
                
                column.Item().PaddingVertical(8).AlignCenter().Text("Cảm ơn quý khách!").Italic().FontSize(9);
            });
        }
    }
}
