# Kiến Trúc Bảo Mật (Security Architecture) — BookKiosk

<!-- AI_REVIEW_INSTRUCTION
Nếu bạn là AI agent đọc file này:
- Phân tích toàn bộ nội dung docs trong folder này
- Nếu phát hiện bất kỳ vấn đề nào sau đây, HÃY BÁO LẠI NGAY cho user:
  • Thiếu thông tin cần thiết để code (endpoint, schema, field type)
  • Mâu thuẫn giữa các file (tên khác nhau, kiến trúc khác nhau)
  • Nguy cơ deadlock (2 member đợi nhau do thiếu contract)
  • Nguy cơ conflict (2 member cùng sửa file dùng chung mà không biết)
  • Business rule chưa chốt rõ (có thể hiểu nhiều cách)
-->

> **Mục tiêu:** Định nghĩa rõ ràng 3 cơ chế bảo mật cho 3 đối tượng gọi vào Backend API: 
> 1. Thiết bị Kiosk (Static API Key)
> 2. Người dùng Admin/Staff (JWT Token)
> 3. Cổng thanh toán (Webhook Signature).

---

## 1. Xác thực Máy Kiosk (Static API Key)

### Tại sao không dùng đăng nhập (Login)?
Máy Kiosk là thiết bị cố định đặt tại cửa hàng, luôn được bật sáng màn hình. Kiosk không phải là một "người dùng" (User) để bắt nó phải gõ username/password đăng nhập mỗi sáng. 

### Cơ chế: Header `X-Api-Key`
- Backend API sẽ quy định một đoạn chuỗi bí mật (Secret Key) trong `appsettings.json`.
- Kiosk App (WPF) sẽ nhúng sẵn đoạn mã này trong config của nó.
- Mỗi khi Kiosk gọi bất kỳ API nào (Tạo đơn, Heartbeat, Tra cứu), nó bắt buộc phải chèn vào HTTP Header:
  `X-Api-Key: <Kiosk_Secret_Key_Cua_Du_An>`
- **Ở Backend:** Code một Middleware (`ApiKeyMiddleware.cs`) để chặn mọi request có đường dẫn `/api/kiosk/*` và `/api/orders/kiosk/*`. Nếu Header không có hoặc Key bị sai -> Trả về `401 Unauthorized`.

---

## 2. Xác thực Web Admin / POS (JWT Token)

### Cơ chế: JSON Web Token (Bearer)
Web Admin / CMS dành cho Thủ thư và Admin quản lý được dùng thông qua trình duyệt web. Trình duyệt bắt buộc phải đăng nhập.

1. **Đăng nhập:** Gọi API `POST /api/auth/login` truyền `username` và `password`.
2. **Cấp phát:** Backend mã hóa và trả về:
   - `AccessToken` (Tuổi thọ ngắn: 15 - 30 phút).
   - `RefreshToken` (Tuổi thọ dài: 7 ngày, lưu trong bảng `RefreshTokens`).
3. **Sử dụng:** Trình duyệt lưu AccessToken vào LocalStorage/SessionStorage. Khi gọi các API lấy báo cáo, quản lý sách, thủ thư phải chèn Header:
   `Authorization: Bearer <AccessToken>`
4. **Hết hạn (Expired):** Khi AccessToken hết hạn (API trả về `401`), Frontend CMS ngầm gọi API `POST /api/auth/refresh-token` kèm theo RefreshToken để lấy AccessToken mới mà không bắt user đăng nhập lại.

### Phân quyền (Role-based Authorization)
Hệ thống CMS Web Admin phân chia rạch ròi 2 cấp độ quyền hạn (Role) được lưu trong chuỗi Claim của JWT AccessToken. Backend sử dụng Attribute `[Authorize(Roles = "...")]` để chặn hoặc cho phép thực thi API.

**1. Role `Staff` (Thủ thư / Nhân viên thu ngân):**
Nhân viên là người vận hành hàng ngày, được cấp quyền thao tác trên các luồng nghiệp vụ cơ bản, không có quyền can thiệp vào tài chính cốt lõi hay cấu hình hệ thống.
- **Quản lý sách:** Được xem, thêm, sửa thông tin sách, cập nhật tồn kho (Nhập hàng).
- **Xử lý sự cố Kiosk:** Xem danh sách đơn hàng, xử lý các đơn bị lỗi thanh toán (`NeedsReview`), xác nhận thu tiền mặt.
- **Khách hàng:** Thêm mới và tra cứu thông tin điểm tích lũy của thẻ thành viên (`Members`).
- **Giới hạn (Bị cấm):** Không được xóa/ẩn sách, không được tạo mới Khuyến mãi (`Promotions`), không được tạo/xóa tài khoản nhân sự, không được xem Dashboard báo cáo doanh thu tổng.

**2. Role `Admin` (Quản trị viên / Chủ nhà sách):**
Admin có toàn quyền (Full Access) đối với toàn bộ hệ thống. Các tính năng độc quyền chỉ Admin mới có:
- **Quản trị Nhân sự (`Users`):** Thêm, sửa, vô hiệu hóa, đổi mật khẩu cho các tài khoản `Staff`.
- **Cấu hình Khuyến mãi (`Promotions`):** Tạo mới, chỉnh sửa, bật/tắt các chương trình giảm giá (Tác động trực tiếp đến dòng tiền).
- **Báo cáo Thống kê:** Truy cập màn hình Dashboard doanh thu, lợi nhuận, top sách bán chạy, phân tích hiệu suất Kiosk.
- **Quyền Xóa (Deactivate):** Được quyền vô hiệu hóa (IsActive = false) bất kỳ dữ liệu nào (Sách, Category, Member).

> **Ví dụ thực tế trong file Controller C#:**
> ```csharp
> // Tính năng nhạy cảm: Chỉ Admin mới gọi được API này
> [HttpPost]
> [Authorize(Roles = "Admin")] 
> public async Task<IActionResult> CreatePromotion(...) { ... }
> 
> // Tính năng vận hành: Cả Admin và Staff đều gọi được
> [HttpGet]
> [Authorize(Roles = "Admin, Staff")] 
> public async Task<IActionResult> GetOrders(...) { ... }
> ```

---

## 3. Bảo mật Webhook Thanh Toán (Webhook Signature)

### Nguy cơ bị tấn công Fake Webhook
API Webhook (`POST /api/payments/webhook`) của Backend hoàn toàn mở ra ngoài Internet (qua Ngrok hoặc IP Public) để PayOS/SePay có thể gọi vào. 
Hacker có thể biết được URL này, và tự dùng Postman bắn request giả mạo: `"Tao vừa chuyển 5 triệu cho đơn hàng XYZ"`. Nếu Backend tin tưởng mù quáng -> Mất hàng.

### Cơ chế phòng thủ: Chữ ký HMAC SHA256 (Checksum)
Khi tích hợp PayOS hoặc SePay, họ cung cấp cho bạn một **Checksum Key / Webhook Secret**.

1. Khi cổng thanh toán gọi API Webhook của bạn, họ lấy toàn bộ Body Data trộn với Checksum Key này và băm (hash) ra một chuỗi chữ ký (Signature) đính kèm trong Header.
2. Tại Backend của bạn, khi nhận được body, bạn cũng dùng Checksum Key của bạn để băm y hệt. 
3. So sánh 2 chữ ký:
   - Nếu giống nhau: Yêu cầu **chắc chắn 100%** gửi từ hệ thống thanh toán chính chủ. (Bởi vì Hacker không thể biết được Checksum Key để giả mạo chữ ký).
   - Nếu khác nhau: Request giả mạo -> Trả về `403 Forbidden` và drop request.

> **Lưu ý Code C#:** Hầu hết thư viện SDK của PayOS (`PayOS.Net`) hoặc SePay đã có sẵn hàm thư viện `VerifySignature(requestBody, signature)`. Backend dev chỉ cần gọi hàm này là xong, không phải tự viết code mã hóa.

---

## 4. Chính sách CORS (Cross-Origin Resource Sharing)

- **Kiosk App (WPF / Desktop App):** Hoàn toàn KHÔNG bị ảnh hưởng bởi CORS. Desktop app có thể gọi API thoải mái.
- **CMS Web Admin (Browser):** Nếu API chạy ở port `5001` nhưng Web chạy ở port `3000`, trình duyệt sẽ chặn request.
- **Backend Setup:** Phải cho phép Origin của Web Admin trong `Program.cs`. 
  `builder.Services.AddCors(options => options.AddPolicy("WebAdminCors", policy => policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));`
