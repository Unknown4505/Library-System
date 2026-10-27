using System;
using System.IO;
using System.Threading.Tasks;
using BookKiosk.Application.DTOs.Order;
using BookKiosk.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BookKiosk.Kiosk.Services
{
    public class ReceiptPdfService : IReceiptService
    {
        public Task<string> GenerateReceiptPdfAsync(ReceiptDto receipt)
        {
            if (receipt == null)
            {
                throw new ArgumentNullException(nameof(receipt));
            }

            // Thư mục lưu file hóa đơn nội bộ của ứng dụng
            var receiptsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Receipts");
            if (!Directory.Exists(receiptsFolder))
            {
                Directory.CreateDirectory(receiptsFolder);
            }

            var safeCode = !string.IsNullOrWhiteSpace(receipt.OrderCode)
                ? receipt.OrderCode
                : (receipt.OrderId > 0 ? receipt.OrderId.ToString() : DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            var fileName = $"Receipt_{safeCode}.pdf";
            var filePath = Path.Combine(receiptsFolder, fileName);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Khổ giấy in nhiệt 80mm ~ 226 points
                    page.ContinuousSize(226, Unit.Point);
                    page.Margin(10, Unit.Point);

                    // Cấu hình danh sách font ưu tiên Segoe UI/Arial/Tahoma và fallback về Lato (font nhúng sẵn trong QuestPDF)
                    // Tắt ThrowOnMissingFontFamilies để tương thích môi trường QA/container không có system fonts
                    QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily("Segoe UI", "Arial", "Tahoma", "Lato"));

                    page.Header().Element(c => ComposeHeader(c, receipt));
                    page.Content().Element(c => ComposeContent(c, receipt));
                    page.Footer().Element(f => ComposeFooter(f, receipt));
                });
            });

            try
            {
                document.GeneratePdf(filePath);
            }
            catch (IOException)
            {
                // Fallback nếu file PDF đang bị ứng dụng ngoài mở khóa ghi (lock file)
                var fallbackFileName = $"Receipt_{safeCode}_{DateTime.Now.Ticks}.pdf";
                filePath = Path.Combine(receiptsFolder, fallbackFileName);
                document.GeneratePdf(filePath);
            }

            if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
            {
                throw new IOException($"Không thể tạo file PDF hoặc file tạo ra rỗng (0 bytes): {filePath}");
            }

            return Task.FromResult(filePath);
        }

        private void ComposeHeader(IContainer container, ReceiptDto receipt)
        {
            var utcTime = receipt.CompletedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(receipt.CompletedAt, DateTimeKind.Utc)
                : receipt.CompletedAt.ToUniversalTime();
            var localTime = utcTime.ToLocalTime();

            container.Column(column =>
            {
                column.Item().AlignCenter().Text("KIOSK SÁCH THÔNG MINH").Bold().FontSize(12);
                column.Item().AlignCenter().Text("123 Nguyễn Văn Cừ, Q.5, TP.HCM").FontSize(9);

                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().AlignCenter().Text("HÓA ĐƠN THANH TOÁN").Bold().FontSize(11);

                column.Item().Text($"Mã ĐH: #{receipt.OrderCode}");
                column.Item().Text($"Thời gian: {localTime:dd/MM/yyyy HH:mm:ss}");

                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        }

        private void ComposeContent(IContainer container, ReceiptDto receipt)
        {
            container.PaddingVertical(2).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Tên sách
                    columns.RelativeColumn(1); // SL
                    columns.RelativeColumn(2); // Thành tiền
                });

                table.Header(header =>
                {
                    header.Cell().Text("Tên sách").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("SL").Bold().FontSize(9);
                    header.Cell().AlignRight().Text("T.Tiền").Bold().FontSize(9);
                });

                if (receipt.Items != null)
                {
                    foreach (var item in receipt.Items)
                    {
                        var hasLineDiscount = item.LineDiscountAmount > 0;

                        // Cột 1: Tên sách + dòng phụ đơn giá gốc & chiết khấu nếu có
                        table.Cell().Column(col =>
                        {
                            col.Item().Text(item.BookTitle).FontSize(9);
                            if (hasLineDiscount && item.DiscountPerUnit > 0)
                            {
                                col.Item().Text($"Đơn giá: {item.UnitPriceAtTime:N0} đ (Giảm {item.DiscountPerUnit:N0} đ/c)")
                                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                            }
                        });

                        // Cột 2: SL
                        table.Cell().AlignRight().Text(item.Quantity.ToString()).FontSize(9);

                        // Cột 3: Thành tiền theo QUY TẮC CHỌN TRƯỜNG Q1
                        if (hasLineDiscount)
                        {
                            table.Cell().AlignRight().Column(col =>
                            {
                                col.Item().Text($"{item.LineTotal:N0} đ")
                                    .FontSize(8).Strikethrough().FontColor(Colors.Grey.Medium);
                                col.Item().Text($"{item.NetLineTotal:N0} đ")
                                    .Bold().FontSize(9);
                            });
                        }
                        else
                        {
                            table.Cell().AlignRight().Text($"{item.LineTotal:N0} đ").FontSize(9);
                        }
                    }
                }
            });
        }

        private void ComposeFooter(IContainer container, ReceiptDto receipt)
        {
            container.Column(column =>
            {
                column.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().AlignRight().Text($"Tạm tính: {receipt.SubTotal:N0} đ").FontSize(9);

                if (receipt.DiscountAmount > 0)
                {
                    var promoLabel = !string.IsNullOrWhiteSpace(receipt.PromotionName)
                        ? receipt.PromotionName
                        : "Khuyến mãi";

                    string typeSuffix = receipt.PromotionType switch
                    {
                        PromotionType.OrderDiscount => " (Hóa đơn)",
                        PromotionType.ProductDiscount => " (Từng cuốn)",
                        _ => string.Empty
                    };

                    column.Item().AlignRight().Text($"{promoLabel}{typeSuffix}: -{receipt.DiscountAmount:N0} đ").FontSize(9);
                }

                if (receipt.PointsUsedAmount > 0)
                {
                    column.Item().AlignRight().Text($"Điểm ({receipt.PointsUsed} điểm): -{receipt.PointsUsedAmount:N0} đ").FontSize(9);
                }

                column.Item().AlignRight().Text($"Tổng thanh toán: {receipt.TotalAmount:N0} đ").Bold().FontSize(11);
                column.Item().AlignRight().Text($"Phương thức: {receipt.PaymentMethod}").FontSize(8).Italic();

                column.Item().PaddingVertical(8).AlignCenter().Text("Cảm ơn quý khách!").Italic().FontSize(9);
            });
        }
    }
}
