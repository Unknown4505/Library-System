using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace BookKiosk.Tests.Cms
{
    /// <summary>
    /// Kiểm thử hợp đồng tĩnh (Static Contract Tests) cho phân hệ In Hóa Đơn K80 (CMS-POS-PRINT-01).
    /// Giới hạn: Kiểm tra chuỗi/cú pháp tĩnh (String & Regex validation) trên các file Razor, CSS và JS
    /// để đảm bảo marker bảo vệ, CSS @media print và cơ chế chặn in không bị gỡ bỏ hoặc thụt lùi.
    /// Không mô phỏng được render động của CSSOM/trình duyệt thật nếu không chạy browser subagent.
    /// </summary>
    public class PosPrintReceiptTests
    {
        private static string GetCmsRoot()
        {
            var baseDir = AppContext.BaseDirectory;
            var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "BookKiosk.CMS"));
            if (Directory.Exists(candidate)) return candidate;

            candidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "BookKiosk.CMS"));
            if (Directory.Exists(candidate)) return candidate;

            throw new DirectoryNotFoundException("Không tìm thấy thư mục BookKiosk.CMS");
        }

        [Fact]
        public void ReceiptModal_HasDefaultUnofficialMarkerAndNotice()
        {
            var modalPath = Path.Combine(GetCmsRoot(), "Views", "Pos", "_ReceiptModal.cshtml");
            File.Exists(modalPath).Should().BeTrue();

            var content = File.ReadAllText(modalPath);

            // Marker mặc định phải là false
            content.Should().Contain("data-receipt-official=\"false\"");

            // Chứa phần tử thông báo cố định khi unofficial
            content.Should().Contain("receiptUnofficialNotice");
            content.Should().Contain("Chưa có hóa đơn chính thức - không dùng làm chứng từ");
        }

        [Fact]
        public void PrintReceiptCss_EnforcesPrintIsolationBetweenOfficialAndUnofficial()
        {
            var cssPath = Path.Combine(GetCmsRoot(), "wwwroot", "css", "print-receipt.css");
            File.Exists(cssPath).Should().BeTrue();

            var css = File.ReadAllText(cssPath);

            // Phải có @media print
            css.Should().Contain("@media print");

            // Phải có selector phân biệt [data-receipt-official="true"] và :not([data-receipt-official="true"])
            css.Should().Contain("data-receipt-official=\"true\"");
            css.Should().Contain(":not([data-receipt-official=\"true\"])");

            // Khi unofficial: ẩn header, table, totals, footer
            css.Should().Contain(".receipt-unofficial-notice");
        }

        [Fact]
        public void PosJs_EnforcesOfficialMarkerAndBeforePrintProtection()
        {
            var jsPath = Path.Combine(GetCmsRoot(), "wwwroot", "js", "pos.js");
            File.Exists(jsPath).Should().BeTrue();

            var js = File.ReadAllText(jsPath);

            // Marker data-receipt-official
            js.Should().Contain("data-receipt-official");

            // Event beforeprint
            js.Should().Contain("beforeprint");

            // Reset khi đóng modal hoặc tạo đơn mới
            js.Should().Contain("hidden.bs.modal");

            // Chặn in khi unofficial
            js.Should().Contain("Chưa có hóa đơn chính thức");
        }

        [Fact]
        public void OrdersJs_SetsOfficialMarkerOnReprint()
        {
            var jsPath = Path.Combine(GetCmsRoot(), "wwwroot", "js", "orders.js");
            File.Exists(jsPath).Should().BeTrue();

            var js = File.ReadAllText(jsPath);

            js.Should().Contain("data-receipt-official");
        }
    }
}
