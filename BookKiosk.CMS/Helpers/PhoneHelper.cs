namespace BookKiosk.CMS.Helpers
{
    public static class PhoneHelper
    {
        /// <summary>
        /// Che số điện thoại thành viên dạng 091****567 (Idempotent: gọi nhiều lần không bị che lặp)
        /// Tuyệt đối không log hoặc hiển thị số điện thoại đầy đủ ở bất kỳ đâu trên CMS.
        /// </summary>
        public static string MaskPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return "-";
            phone = phone.Trim();

            // Nếu đã bị che rồi thì trả về nguyên dạng (Idempotent)
            if (phone.Contains('*')) return phone;

            if (phone.Length == 10)
            {
                return $"{phone[..3]}****{phone[7..]}";
            }

            if (phone.Length > 6)
            {
                return $"{phone[..3]}****{phone[^3..]}";
            }

            return phone;
        }
    }
}
