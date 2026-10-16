namespace BookKiosk.CMS.Helpers
{
    public static class DateTimeHelper
    {
        private static readonly TimeZoneInfo VnTimeZone = 
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

        public static DateTime ToVietnamTime(DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Unspecified)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime.ToUniversalTime(), VnTimeZone);
        }

        public static string FormatVietnamDateTime(DateTime? dateTime, string format = "dd/MM/yyyy HH:mm")
        {
            if (!dateTime.HasValue) return "-";
            var vnTime = ToVietnamTime(dateTime.Value);
            return vnTime.ToString(format);
        }

        public static string FormatVietnamDate(DateTime? dateTime)
        {
            return FormatVietnamDateTime(dateTime, "dd/MM/yyyy");
        }
    }
}
