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
- Backend API đọc secret từ key canonical `ApiSettings:ApiKey`; production phải cấp bằng environment variable `ApiSettings__ApiKey` hoặc secret store, không commit vào `appsettings.json`.
- Kiosk App (WPF) sẽ nhúng sẵn đoạn mã này trong config của nó.
- Mỗi khi Kiosk gọi bất kỳ API nào (Tạo đơn, Heartbeat, Tra cứu), nó bắt buộc phải chèn vào HTTP Header:
  `X-Api-Key: <Kiosk_Secret_Key_Cua_Du_An>`
- **Ở Backend:** `ApiKeyMiddleware.cs` bảo vệ `/api/kiosk/*` và `/api/orders/kiosk/*`. Thiếu header trả `401`; có header nhưng key sai trả `403`. Cả hai đều trả JSON `ApiResponseDto` và không log secret.

---

## 2. Xác thực Web Admin / POS (JWT Token)

### Cơ chế: JSON Web Token (Bearer)
Web Admin / CMS dành cho Thủ thư và Admin quản lý được dùng thông qua trình duyệt web. Trình duyệt bắt buộc phải đăng nhập.

1. **Đăng nhập (Planned):** Contract dự kiến là `POST /api/auth/login`; endpoint này chưa có trên `main`.
2. **Cấp phát:** Backend xác thực BCrypt, trả về `AccessToken` (JWT, tuổi thọ **15–30 phút**). Không lưu gì vào DB.
3. **Sử dụng:** Trình duyệt lưu AccessToken vào `sessionStorage`. Khi gọi API, chèn Header:
   `Authorization: Bearer <AccessToken>`
4. **Hết hạn:** Khi AccessToken hết hạn (API trả `401`), Frontend redirect về trang Login. Nhân viên đăng nhập lại.

> **Lý do không dùng Refresh Token:** Hệ thống chạy môi trường nội bộ cửa hàng, phiên làm việc ngắn, không có nhu cầu "nhớ đăng nhập". Stateless đơn giản hơn và không cần thêm bảng DB.

### Phân quyền (Role-based Authorization)
Hệ thống CMS Web Admin phân chia rạch ròi 2 cấp độ quyền hạn (Role) được lưu trong chuỗi Claim của JWT AccessToken. Backend sử dụng Attribute `[Authorize(Roles = "...")]` để chặn hoặc cho phép thực thi API.

**1. Role `Staff` (Thủ thư / Nhân viên thu ngân):**
Nhân viên là người vận hành hàng ngày, được cấp quyền thao tác trên các luồng nghiệp vụ cơ bản, không có quyền can thiệp vào tài chính cốt lõi hay cấu hình hệ thống.
- **Quản lý sách:** Được xem, thêm, sửa thông tin sách, cập nhật tồn kho (Nhập hàng).
- **Xử lý sự cố Kiosk:** Xem danh sách đơn hàng, xử lý các đơn bị lỗi thanh toán (khách chuyển sai tiền), xác nhận thu tiền mặt.
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
API Webhook (`POST /api/payments/sepay-webhook`) của Backend mở ra ngoài Internet (qua Ngrok hoặc IP Public) để SePay có thể gọi vào.
Hacker có thể biết được URL này, và tự dùng Postman bắn request giả mạo: `"Tao vừa chuyển 5 triệu cho đơn hàng XYZ"`. Nếu Backend tin tưởng mù quáng -> Mất hàng.

### Cơ chế phòng thủ: Chữ ký HMAC SHA256 (Checksum)
Khi tích hợp SePay, hệ thống thanh toán cung cấp **Webhook Secret** theo cơ chế đã cấu hình.

1. Khi cổng thanh toán gọi API Webhook của bạn, họ lấy toàn bộ Body Data trộn với Checksum Key này và băm (hash) ra một chuỗi chữ ký (Signature) đính kèm trong Header.
2. Tại Backend của bạn, khi nhận được body, bạn cũng dùng Checksum Key của bạn để băm y hệt. 
3. So sánh 2 chữ ký:
   - Nếu giống nhau: Yêu cầu **chắc chắn 100%** gửi từ hệ thống thanh toán chính chủ. (Bởi vì Hacker không thể biết được Checksum Key để giả mạo chữ ký).
   - Nếu khác nhau: Request giả mạo -> Trả về `403 Forbidden` và drop request.

> **Lưu ý:** Phải đối chiếu chính xác thuật toán/header trong tài liệu SePay đang dùng; không giả định contract của gateway khác tương thích.

---

## 4. Chính sách CORS (Cross-Origin Resource Sharing)

- **Kiosk App (WPF / Desktop App):** Hoàn toàn KHÔNG bị ảnh hưởng bởi CORS. Desktop app có thể gọi API thoải mái.
- **CMS Web Admin (Browser):** API (`7111`/`5014`) và CMS (`7125`/`5131`) là hai origin khác nhau nên trình duyệt áp dụng CORS.
- **Backend Setup:** Chỉ cho phép origin nằm trong `Cors:AllowedOrigins`; không dùng `AllowAnyOrigin()` khi triển khai.

## 5. Cấu hình JWT và Swagger

- JWT signing key dùng key `Jwt:Key` (environment: `Jwt__Key`). Development có fallback demo và ghi warning; Production từ chối startup nếu thiếu, không dùng fallback.
- Swagger Development khai báo hai scheme: `Bearer` cho CMS và `ApiKey` qua header `X-API-KEY` cho Kiosk.
- Xem `06-local-setup.md` để cấu hình bằng user-secrets/environment variables.
