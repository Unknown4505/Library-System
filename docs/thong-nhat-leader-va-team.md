# TÀI LIỆU CÁC ĐIỂM THỐNG NHẤT VỚI LEADER VÀ NHÓM PHÁT TRIỂN (CẬP NHẬT)

> **Người lập:** Thiện (`@thien33`)  
> **Dự án:** BookKiosk Backend — Sau PR #17 & Phản hồi PR #26  
> **Người nhận:** Leader (`@Unknown4505`), Khang (`@lehuukhang`)  
> **Trạng thái:** Đã hoàn tất triển khai & kiểm thử biên dịch Release 0 lỗi — Chờ phê duyệt merge  
> **Liên kết Issue:** Issue #19 (https://github.com/Unknown4505/Library-System/issues/19)  

---

### 1. Mở rộng Repository cho Concurrency Safety (BE-NEW-01, BE-NEW-02)
- **Đã triển khai:**
  - `TryIncreaseReservedQuantityAsync`: Conditional atomic UPDATE `WHERE BookId = @id AND IsActive = 1 AND (StockQuantity - ReservedQuantity) >= @quantity`.
  - `TryTransitionOrderStatusAsync`: Atomic state transition `WHERE OrderId = @id AND OrderStatus = @fromStatus`.
- **Trạng thái:** Đã tích hợp và kiểm thử biên dịch thành công.

---

### 2. Định dạng mã đơn hàng `OrderCode` duy nhất tuyệt đối (BE-NEW-05, BE-NEW-14)
- **Giải pháp:**
  - Đồng bộ 100% theo `docs/03-database.md` (§3.2, dòng 140) và `docs/init.sql` (dòng 341):
    Format chuẩn: `ORD-yyyyMMdd-XXXX` (trong đó `XXXX` là `OrderId` toàn hệ thống padding 4 chữ số, VD: `ORD-20261011-0042`).
  - Sử dụng SQL Server Identity của `OrderId` để sinh mã, đảm bảo **100% toán học không bao giờ trùng lặp** và tương thích hoàn toàn với QR SePay và FE của Khang.

---

### 3. Cơ chế lọc sách Inactive giữa Kiosk và CMS Admin (BE-NEW-08)
- **Giải pháp:**
  - `OrderService.CheckoutKioskAsync`: Chặn tuyệt đối sách có `!book.IsActive` (ném ngoại lệ `Sách '{book.Title}' đã ngừng kinh doanh`).
  - `BookService.GetBookByBarcodeAsync`: Chặn sách inactive và trả về 404 `NOT_FOUND` (phục vụ Kiosk quét mã vạch).
  - `BookService.GetBookByIdAsync`: Giữ nguyên trả về thông tin sách (kèm cờ `IsActive = false`) để Admin CMS quản lý bình thường.

---

### 4. Chiến lược Quản lý Secret & Xác thực Webhook SePay (BE-NEW-03, BE-NEW-10)
- **Giải pháp:**
  - Xác thực chữ ký HMAC-SHA256 (`X-SePay-Signature`, `X-SePay-Timestamp`), dung sai anti-replay ±300s, so sánh constant-time.
  - **Production Fail-Closed:** Khi thiếu secret trong Production, từ chối ngay lập tức (HTTP 500/401).
  - **BE-NEW-10:** Chỉ xác nhận thanh toán cho giao dịch tiền vào (`transferType = "in"`). Đối chiếu tài khoản nhận tiền `accountNumber` và `subAccount` với cấu hình.

---

### 5. Xử lý An toàn `DbUpdateException` & Idempotency (BE-NEW-04, BE-NEW-11)
- **Giải pháp:**
  - Sau khi rollback, query lại DB `CheckTransactionExistsAsync(refCode)`.
  - Chỉ coi là duplicate thành công khi transaction thực sự đã tồn tại trong DB.
  - Nếu transaction chưa tồn tại: Đây là lỗi DB thật sự (FK, Check Constraint, dữ liệu lỗi) -> `throw;` lại lỗi, tuyệt đối không nuốt lỗi.

---

### 6. Webhook Thanh toán Đến sau khi Order Đã Cancelled (BE-NEW-12)
- **Giải pháp:**
  - Bổ sung `GetOrderByCodeAsync` để lấy đơn hàng ở mọi trạng thái.
  - Nếu đơn đã `Cancelled`: Vẫn lưu `PaymentTransaction` vào DB để lưu vết đối soát, không chuyển lại sang `Paid`, không trừ kho, không đụng điểm, log warning để nhân viên đối soát hoàn tiền/tạo đơn bù. Trả `200 OK` cho SePay.

---

### 7. Kiểm soát Điểm Thành viên Đồng thời (BE-NEW-13)
- **Giải pháp:**
  - Sửa lỗi vi phạm `CONSTRAINT [CK_PointTransactions_Points] CHECK ([Points] > 0)`: Luôn lưu `Points` dương > 0 khi Redeem.
  - Áp dụng mô hình Hold & Release chuẩn eCommerce:
    + Tại `CheckoutKioskAsync`: Trừ giữ điểm nguyên tử tại DB `TryDeductPointsAsync` (`UPDATE Members SET Points = Points - @used WHERE MemberId = @id AND Points >= @used`). Nếu không đủ điểm khả dụng -> báo lỗi ngay tại checkout, chặn 2 đơn dùng chung điểm.
    + Tại `CancelOrderAsync` & `ExpiredOrderCleanupService`: Hoàn lại điểm nguyên tử `AddPointsAsync` và ghi transaction hoàn điểm.
    + Tại `PaymentService`: Điểm đã giữ an toàn lúc checkout, chỉ việc cộng điểm thưởng tích lũy mới (`TotalAmount / 10000`).

---

### 8. Endpoint F3 Development Mô phỏng Thanh toán Kiosk (Phối hợp PR #11)
- **Giải pháp:**
  - Triển khai `POST /api/payments/dev-simulate-success/{orderCode}` theo đúng `docs/04-api-reference.md` (§3.4).
  - Chỉ map trong `Development`, Production trả `404`.
  - Xác thực qua `X-Api-Key` của Kiosk.
  - Dùng chung quy trình hoàn tất an toàn với webhook thật (chuyển `Paid` nguyên tử, trừ kho thật, tích điểm), trả về `ApiResponseDto<PaymentStatusDto>`.
