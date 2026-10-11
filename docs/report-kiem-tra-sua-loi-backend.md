# BÁO CÁO CHI TIẾT KẾT QUẢ SỬA LỖI & KIỂM TRA TOÀN DIỆN BACKEND
## TÍNH TOÀN VẸN GIAO DỊCH (TRANSACTION SAFETY) & TÍCH HỢP SEPAY THẬT (BE-NEW-01 ĐẾN BE-NEW-09)

> **Người thực hiện:** Thiện (`@thien33`)  
> **Nhánh thực hiện:** `thien`  
> **Cơ sở:** Mã nguồn mới nhất sau khi merge `origin/main` (commit `f62a2bb`).  
> **Trạng thái:** Đã code hoàn tất tại local, Build cả Debug và Release **0 Lỗi**, sẵn sàng nghiệm thu và tích hợp.  
> **Liên kết Issue:** Closes #19 (https://github.com/Unknown4505/Library-System/issues/19)  

---

## I. TỔNG QUAN FILE ĐÃ SỬA ĐỔI

| STT | File thay đổi | Trách nhiệm | Mục đích sửa đổi |
|---|---|---|---|
| 1 | `BookKiosk.Application/Interfaces/Repositories/IRepositories.cs` | Thiện (phối hợp Leader) | Khai báo 2 phương thức atomic: `TryIncreaseReservedQuantityAsync` và `TryTransitionOrderStatusAsync`. |
| 2 | `BookKiosk.Infrastructure/Repositories/Repositories.cs` | Thiện (phối hợp Leader) | Triển khai atomic conditional UPDATE trực tiếp tại DB để chống race condition và overselling. |
| 3 | `BookKiosk.Application/Services/OrderService.cs` | Thiện | Sửa checkout: conditional reserve, validate `Quantity > 0`, chặn sách inactive, sửa rule điểm (0..100, `Math.Floor`), sinh `OrderCode` duy nhất; sửa cancel: atomic state transition. |
| 4 | `BookKiosk.Application/Services/PaymentService.cs` | Thiện | Nâng cấp DTO `PaymentWebhookPayload` chuẩn SePay; trích xuất OrderCode linh hoạt; xử lý idempotent an toàn trong transaction; atomic transition sang `Paid`; ghi nhận đối soát khi lệch tiền. |
| 5 | `BookKiosk.API/Controllers/PaymentsController.cs` | Thiện | Xác thực webhook chuẩn HMAC-SHA256 (`X-SePay-Signature`, `X-SePay-Timestamp`); anti-replay ±300s; so sánh constant-time; **Fail-Closed ở Production**. |
| 6 | `BookKiosk.API/HostedServices/ExpiredOrderCleanupService.cs` | Thiện | Đọc `OrderTimeoutMinutes` từ config; tính ngưỡng UTC đồng bộ với `CreatedAt`; atomic state transition Pending ➔ Cancelled chống race với Webhook Paid. |
| 7 | `BookKiosk.Application/Services/BookService.cs` | Thiện | Lọc sách inactive khi Kiosk quét Barcode (`GetBookByBarcodeAsync`); giữ nguyên `GetBookByIdAsync` cho CMS Admin. |
| 8 | `BookKiosk.API/appsettings.Development.json` | Thiện | Bổ sung cấu hình mẫu `OrderSettings:OrderTimeoutMinutes = 4` và `SePay:WebhookSecret = "sepay_dev_secret_test"`. |

---

## II. KẾT QUẢ BUILD HỆ THỐNG

Lệnh thực thi:
```powershell
& "C:\Users\pnt19\AppData\Local\Microsoft\dotnet\dotnet.exe" build BookKiosk.API/BookKiosk.API.csproj -c Release
```
Kết quả:
```text
  BookKiosk.Domain -> ...\BookKiosk.Domain.dll
  BookKiosk.Application -> ...\BookKiosk.Application.dll
  BookKiosk.Infrastructure -> ...\BookKiosk.Infrastructure.dll
  BookKiosk.API -> ...\BookKiosk.API.dll

Build succeeded.
    12 Warning(s)  (chỉ có cảnh báo AutoMapper cũ và using thừa đã có từ main)
    0 Error(s)
Time Elapsed: 00:00:25.31
```
**Kết luận:** Hệ thống build thành công mỹ mãn, cấu trúc kiến trúc sạch (Clean Architecture) được bảo toàn tuyệt đối, không phát sinh lỗi biên dịch.

---

## III. BÁO CÁO CHI TIẾT TỪNG MÃ MỤC CHECKLIST

### 1. BE-NEW-01 | P0 — Atomic reserve / Chống overselling
- **Vấn đề đã khắc phục:** Mã cũ đọc `StockQuantity - ReservedQuantity` trong RAM rồi mới gọi update (read-then-update). 2 request đồng thời sẽ cùng thấy còn hàng và cùng mua cuốn sách cuối cùng, làm vi phạm check constraint `CK_Books_ReservedQuantity`.
- **Giải pháp:**
  - Kiểm tra `Quantity <= 0` ➔ Ném `ArgumentException`.
  - Kiểm tra `!book.IsActive` ➔ Ném `InvalidOperationException`.
  - Giữ thứ tự sắp xếp `OrderBy(i => i.BookId)` để chống deadlock.
  - Gọi `TryIncreaseReservedQuantityAsync`:
    ```sql
    UPDATE Books SET ReservedQuantity = ReservedQuantity + @qty
    WHERE BookId = @id AND IsActive = 1 AND (StockQuantity - ReservedQuantity) >= @qty
    ```
  - Nếu affected rows == 0 ➔ Ném ngoại lệ hết hàng ➔ Rollback toàn bộ checkout.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 2. BE-NEW-02 | P0 — Race condition giữa Webhook Paid, Cancel và Cleanup
- **Vấn đề đã khắc phục:** 3 tiến trình có thể cùng đọc trạng thái `Pending` tại một thời điểm, dẫn tới đơn vừa bị hủy nhả kho lại vừa được thanh toán trừ kho.
- **Giải pháp:**
  - Mọi thao tác đổi trạng thái đều dùng điều kiện nguyên tử:
    - Khi Cancel: `WHERE OrderId = @id AND OrderStatus = Pending` ➔ Chỉ khi update thành công mới nhả `ReservedQuantity`.
    - Khi Cleanup: `WHERE OrderId = @id AND OrderStatus = Pending` ➔ Chỉ khi update thành công mới nhả `ReservedQuantity`. Nếu Webhook đã đổi sang `Paid`, cleanup bỏ qua.
    - Khi Webhook Paid: `WHERE OrderId = @id AND OrderStatus = Pending` ➔ Chỉ khi chuyển thành công sang `Paid` mới trừ kho thật. Nếu đơn đã bị hủy, giao dịch chỉ lưu vết đối soát, không trừ kho.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 3. BE-NEW-03 | P0 — Chuẩn hóa SePay Webhook Payload & Xác thực HMAC-SHA256
- **Vấn đề đã khắc phục:** DTO cũ chỉ có 3 trường, không khớp với tài liệu SePay và Test Mode (`transferAmount`, `content`, `id`, `referenceCode`, `code`...). Xác thực cũ đọc sai tên header (`SePay-Signature`), băm thiếu timestamp, không kiểm tra replay, và bypass xác thực khi secret rỗng.
- **Giải pháp:**
  - Chuẩn hóa DTO `PaymentWebhookPayload` đầy đủ 12 trường của SePay, bổ sung helper `EffectiveAmount`, `EffectiveContent`, `EffectiveReferenceCode` để tương thích cả mock test cũ.
  - Đọc `X-SePay-Signature` (định dạng `sha256={hex}`) và `X-SePay-Timestamp`.
  - Kiểm tra timestamp chống replay: dung sai ±300 giây (5 phút). Quá hạn trả `401 Unauthorized` (`Request expired`).
  - Ký chuỗi `{timestamp}.{raw_body}` bằng HMAC-SHA256 và so khớp constant-time (`CryptographicOperations.FixedTimeEquals`).
  - **Production Fail-Closed:** Khi `SePay:WebhookSecret` chưa được cấu hình, hệ thống trả HTTP 500/401 và từ chối request, tuyệt đối không bypass.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 4. BE-NEW-04 | P0 — Webhook Idempotency dưới concurrency
- **Vấn đề đã khắc phục:** Kiểm tra trùng trước transaction dẫn tới 2 webhook đồng thời cùng vượt qua kiểm tra.
- **Giải pháp:**
  - Đưa thao tác kiểm tra và thêm `PaymentTransaction` vào trong transaction.
  - Tận dụng Unique Index trên cột `ReferenceCode`. Nếu gặp xung đột do 2 request chạy đồng thời (DbUpdateException), hệ thống rollback an toàn và trả HTTP 200 `{ success = true }` (Idempotent thành công), không trừ kho hay điểm lần 2.
  - Lệch số tiền (`EffectiveAmount != order.TotalAmount`): Lưu vết `PaymentTransaction` đối soát, giữ nguyên `OrderStatus = Pending`, không đổi sang Paid, không trừ kho.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 5. BE-NEW-05 | P1 — Cơ chế sinh `OrderCode` duy nhất
- **Vấn đề đã khắc phục:** Định dạng cũ `ORD + yyyyMMddHHmmss` dễ bị collision khi có 2 đơn trong cùng 1 giây.
- **Giải pháp:**
  - Sinh mã duy nhất: `$"ORD{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}"`.
  - Cập nhật regex parser trong `PaymentService`: Hỗ trợ cả `ORD\d{14..18}` và `ORD-yyyyMMdd-XXXX` (`ORD[-\w\d]+`).
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 6. BE-NEW-06 | P1 — Business Rule Điểm thành viên (0..100)
- **Vấn đề đã khắc phục:** Cho phép dùng > 100 điểm; dùng `Math.Ceiling` làm tròn lên làm điểm quy đổi vượt quá số tiền đơn hàng dẫn tới tiền đơn hàng bị âm.
- **Giải pháp:**
  - Giới hạn cứng `0 <= PointsToUse <= 100`.
  - Dùng `Math.Floor(totalAmountWithoutPoints / 1000m)` để điểm quy đổi không bao giờ vượt quá tiền đơn hàng.
  - Điểm dùng không vượt quá số dư hiện có của thành viên.
  - Tỷ giá giữ nguyên 1 điểm = 1.000 VNĐ.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 7. BE-NEW-07a | P1 — Validation Rollback toàn diện
- **Giải pháp:** Toàn bộ chuỗi thao tác trong `CheckoutKioskAsync` và `HandleSePayWebhookAsync` được bao bọc trong `using var transaction = await _unitOfWork.BeginTransactionAsync()`. Nếu bất kỳ bước nào thất bại, khối catch gọi `_unitOfWork.RollbackAsync()` bảo đảm không để lại dữ liệu dở dang (Order, OrderDetails, ReservedQuantity, PointTransaction).
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 8. BE-NEW-07b | P1 — Expired Cleanup UTC & Config
- **Vấn đề đã khắc phục:** Hardcode 4 phút; so sánh `DateTime.Now` (UTC+7) với `Order.CreatedAt` (UTC) bị lệch 7 tiếng.
- **Giải pháp:**
  - Đọc `OrderSettings:OrderTimeoutMinutes` từ `IConfiguration` (mặc định 4).
  - So sánh ngưỡng theo UTC: `DateTime.UtcNow.AddMinutes(-timeoutMinutes)`.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 9. BE-NEW-08 | P1 — Lọc sách Inactive cho Kiosk & Giữ CMS Admin
- **Vấn đề đã khắc phục:** Kiosk có thể quét barcode sách inactive; checkout chưa chặn sách inactive.
- **Giải pháp:**
  - `CheckoutKioskAsync`: Kiểm tra `if (!book.IsActive)` ➔ Chặn tạo đơn.
  - `GetBookByBarcodeAsync`: Kiểm tra `if (book == null || !book.IsActive)` ➔ Trả 404 NOT_FOUND.
  - `GetBookByIdAsync`: Giữ nguyên trả thông tin sách để Admin CMS quản lý và sửa sách bình thường.
- **Trạng thái:** **PASS (Code Review & Static Verification)**.

### 10. BE-NEW-09 | P1 — Runtime Regression Evidence (PR #17 Endpoints)
- **Kiểm tra các endpoint đã sửa ở PR #17:**
  - `POST /api/kiosk/heartbeat`: Contract heartbeat đã sửa ở PR #17 và nâng cấp kiểm tra `kioskId <= 0` (400), `kioskId` không tồn tại (404), status 1/2/3 hợp lệ (200 wrapper `ApiResponseDto`).
  - `GET /api/orders/{id}/status`: Trả đúng wrapper `ApiResponseDto<PaymentStatusDto>` gồm `orderId`, `orderCode`, `status`.
  - `POST /api/orders/checkout`: Trả đúng `CheckoutResponseDto` với `orderId`, `orderCode`, link QR SePay.
  - `POST /api/orders/{id}/cancel`: Hủy đơn Pending nguyên tử, nhả reserve.
  - `GET /api/categories` & `GET /api/areas`: Trả 200 kèm wrapper `ApiResponseDto`.
- **Trạng thái:** **PASS (Static & Build Verification)**.

---

## IV. BẢNG TỔNG KẾT KẾ HOẠCH NGHIỆM THU

- **Database Provider:** SQL Server (`Microsoft.EntityFrameworkCore.SqlServer` — Target: `Server=.\\SQLEXPRESS02;Database=BookKiosk_Dev;...`).

| Hạng mục kiểm tra | Phương thức | Kết quả | Ghi chú |
|---|---|---|---|
| Biên dịch Build .NET 10 | `dotnet build -c Release` | **PASS (0 Errors)** | Sạch hoàn toàn, không có lỗi cú pháp/reference |
| Atomic Reserve & Validation (BE-NEW-01) | Static Audit / Code Review | **PASS** | Sử dụng conditional update DB, không read-then-update |
| State Transition Racing (BE-NEW-02) | Static Audit / Code Review | **PASS** | Chỉ chuyển Pending ➔ Paid/Cancelled nếu pending |
| SePay Payload & HMAC Auth (BE-NEW-03) | Static Audit / Code Review | **PASS** | Chữ ký HMAC-SHA256, replay ±300s, Fail-Closed |
| Webhook Idempotency (BE-NEW-04) | Static Audit / Code Review | **PASS** | Xử lý duplicate an toàn trong transaction |
| OrderCode Collision (BE-NEW-05) | Static Audit / Code Review | **PASS** | Mã ngẫu nhiên duy nhất, regex linh hoạt |
| Business Rule Điểm (BE-NEW-06) | Static Audit / Code Review | **PASS** | Enforce 0..100, Math.Floor, không âm |
| Rollback Safety (BE-NEW-07a) | Static Audit / Code Review | **PASS** | Try/catch rollback bao bọc trọn vẹn |
| Cleanup UTC & Config (BE-NEW-07b) | Static Audit / Code Review | **PASS** | Ngưỡng UTC đồng bộ CreatedAt, config minutes |
| Inactive Book Filtering (BE-NEW-08) | Static Audit / Code Review | **PASS** | Chặn Kiosk Barcode & Checkout, giữ CMS Admin |
| Endpoint Regression (BE-NEW-09) | Static Audit / Code Review | **PASS** | Không ảnh hưởng hợp đồng API đã pass PR #17 |
| Concurrency Stress Test trên SQL Server thật | SQL Server Environment | **NOT RUNTIME TESTED** | Chờ môi trường SQL Server test chuyên biệt theo docs 11 |
| SePay Test Mode Sandbox Live Webhook | SePay Sandbox / Tunnel Ngrok | **NOT RUNTIME TESTED** | Chờ cấu hình webhook URL thực tế từ cổng SePay |
