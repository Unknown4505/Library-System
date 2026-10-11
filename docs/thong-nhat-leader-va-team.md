# TÀI LIỆU CÁC ĐIỂM CẦN THỐNG NHẤT VỚI LEADER VÀ NHÓM PHÁT TRIỂN

> **Người lập:** Thiện (`@thien33`)  
> **Dự án:** BookKiosk Backend — Sau PR #17  
> **Người nhận:** Leader (`@Unknown4505`), Khang (`@Khang`)  
> **Trạng thái:** Chờ thống nhất trước khi tạo PR mới  
> **Liên kết Issue:** Issue #19 (https://github.com/Unknown4505/Library-System/issues/19)  

---

### 1. Thống nhất mở rộng Repository cho Concurrency Safety (BE-NEW-01, BE-NEW-02)
- **Vấn đề:** Để triệt tiêu race condition (read-then-update) khi 2 khách cùng mua cuốn sách cuối cùng (overselling) và xung đột giữa Webhook Paid với Cancel/Cleanup, cần thực hiện UPDATE có điều kiện (conditional update) nguyên tử ở cấp cơ sở dữ liệu.
- **Đề xuất đã triển khai:**
  - Trong `IOrderRepository` và `OrderRepository`: bổ sung 2 phương thức:
    ```csharp
    Task<bool> TryIncreaseReservedQuantityAsync(int bookId, int quantity);
    Task<bool> TryTransitionOrderStatusAsync(int orderId, OrderStatus fromStatus, OrderStatus toStatus, DateTime? completedAt = null);
    ```
  - `TryIncreaseReservedQuantityAsync`: Sử dụng EF `ExecuteUpdateAsync` với điều kiện `WHERE BookId = @id AND IsActive = 1 AND (StockQuantity - ReservedQuantity) >= @quantity`. Trả về `true` nếu cập nhật thành công (affected rows > 0), `false` nếu không đủ tồn kho.
  - `TryTransitionOrderStatusAsync`: Sử dụng `WHERE OrderId = @id AND OrderStatus = @fromStatus`. Trả về `true` nếu chuyển trạng thái thành công, `false` nếu đơn đã chuyển sang trạng thái khác (tránh nhả kho 2 lần hoặc trừ kho 2 lần).
- **Hành động cần Leader:** Leader review và chốt việc giữ 2 method này trong tầng `BookKiosk.Infrastructure/Repositories/Repositories.cs` và interface `IRepositories.cs`.

---

### 2. Thống nhất định dạng mã đơn hàng `OrderCode` (BE-NEW-05)
- **Vấn đề:** 
  - Mã nguồn cũ sinh mã: `"ORD" + DateTime.Now.ToString("yyyyMMddHHmmss")` (độ dài 17 ký tự, độ chính xác giây, rủi ro collision cao khi 2 khách đặt cùng 1 giây).
  - Tài liệu `docs/03-database.md` ghi định dạng: `ORD-yyyyMMdd-XXXX` (XXXX là số thứ tự tăng dần toàn hệ thống).
  - Regex trích xuất SePay webhook trên `main` trước đó lại dùng: `ORD\d{14}` (không chấp nhận dấu gạch ngang `-`).
- **Đề xuất của Thiện:**
  - Trong code tạm thời: Sinh mã duy nhất bằng timestamp UTC + 4 số ngẫu nhiên: `$"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}"` (vẫn bắt đầu bằng `ORD` và đủ số, không bao giờ trùng lặp).
  - Đã nâng cấp regex parser trong `PaymentService.cs`: Hỗ trợ cả định dạng `ORD\d{14..18}` và `ORD-yyyyMMdd-XXXX` (regex: `ORD[-\w\d]+`).
- **Hành động cần Leader:** Chốt format chính thức toàn hệ thống giữa Kiosk, QR code, Webhook và DB sequence.

---

### 3. Thống nhất cơ chế lọc sách Inactive giữa Kiosk và CMS Admin (BE-NEW-08)
- **Vấn đề:** 
  - Kiosk không được phép quét barcode, xem chi tiết hoặc mua sách đã ngừng kinh doanh (`IsActive = false`).
  - Tuy nhiên, CMS Admin cần xem được sách inactive để quản lý kho và kích hoạt lại sách (`PUT /api/books/{id}`).
- **Đề xuất của Thiện:**
  - `OrderService.CheckoutKioskAsync`: Chặn tuyệt đối sách có `!book.IsActive` (ném ngoại lệ `Sách '{book.Title}' đã ngừng kinh doanh`).
  - `BookService.GetBookByBarcodeAsync`: Chặn sách inactive và trả về 404 `NOT_FOUND` (vì tính năng quét barcode chỉ phục vụ Kiosk mua hàng).
  - `BookService.GetBookByIdAsync`: Giữ nguyên trả về thông tin sách (kèm cờ `IsActive = false`) để Admin CMS không bị lỗi màn hình chỉnh sửa sách.
- **Hành động cần Leader:** Xác nhận phương án trên có đáp ứng nghiệp vụ hay Leader muốn tách riêng API `GET /api/books/kiosk/{id}` và `GET /api/books/admin/{id}`.

---

### 4. Chiến lược Quản lý Secret Webhook SePay (BE-NEW-03)
- **Vấn đề:** SePay yêu cầu bảo mật Webhook qua chữ ký HMAC-SHA256 (`X-SePay-Signature`, `X-SePay-Timestamp`). Cần bảo vệ webhook endpoint trong Production không có cửa sau (fail closed).
- **Đề xuất của Thiện:**
  - Tuyệt đối không hardcode secret trong source code hoặc commit vào git.
  - Trong Development: Cấu hình secret test mẫu trong `appsettings.Development.json` (`"SePay:WebhookSecret": "sepay_dev_secret_test"`).
  - Trong Production: Nếu `SePay:WebhookSecret` bị bỏ trống, hệ thống **từ chối ngay lập tức** với mã HTTP 500/401 (Fail-Closed), không bypass kiểm tra chữ ký.
- **Hành động cần Leader:** Xác nhận cơ chế nạp secret qua Environment Variable (`SePay__WebhookSecret`) hoặc User Secrets khi triển khai deploy thực tế.

---

### 5. Thống nhất Endpoint Mô phỏng Thanh toán F3 (Dev Simulation)
- **Vấn đề:** Trong môi trường thử nghiệm nội bộ hoặc khi không có internet/ngrok kết nối SePay thật, cần endpoint mô phỏng F3 để Kiosk test thanh toán.
- **Quy tắc:**
  - Không mở bypass trên route Webhook thật.
  - Endpoint `POST /api/payments/dev-simulate-success/{orderCode}` (đã ghi trong docs 04) chỉ được map khi `IHostEnvironment.IsDevelopment()` là `true`.
  - Phải xác thực bằng `X-Api-Key` của Kiosk.
- **Hành động cần Leader & Khang:** Xác nhận kế hoạch triển khai endpoint này ở nhánh dev của Thiện sau khi các issue P0/P1 hiện tại được phê duyệt.
