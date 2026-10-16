using System.Globalization;

namespace BookKiosk.CMS.Helpers
{
    public static class MoneyHelper
    {
        private static readonly CultureInfo VnCulture = new CultureInfo("vi-VN");

        public static string FormatVnd(decimal? amount)
        {
            if (!amount.HasValue) return "0 đ";
            return string.Format(VnCulture, "{0:N0} đ", amount.Value);
        }

        public static string FormatVnd(decimal amount)
        {
            return string.Format(VnCulture, "{0:N0} đ", amount);
        }
    }
}
