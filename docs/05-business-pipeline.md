# Luồng Nghiệp Vụ (Business Pipeline) — BookKiosk

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

> **Mục tiêu:** Mô tả chi tiết logic xử lý phía Backend cho các chức năng cốt lõi. Đây là cẩm nang để Backend Dev code luồng xử lý giỏ hàng và thanh toán mà không bị hổng (loophole).

## 0. Nguyên tắc lõi về Tồn Kho (Inventory Rule)

Để Kiosk luôn hiển thị đúng số lượng sách thực tế đang có trên kệ tủ (khớp với vật lý), hệ thống tuân thủ nguyên tắc **Tồn kho khả dụng (AvailableStock)**:

`AvailableStock = StockQuantity (Tồn vật lý) - ReservedQuantity (Tồn đang bị tạm giữ bởi các đơn Pending)`

- **Khi khách Search sách (`GET /api/books`):** Backend **bắt buộc** phải trả về `AvailableStock`. Nếu Khách A đang cầm 1 cuốn ra màn hình quét QR (`Reserved` + 1), Khách B search sẽ thấy hụt đi 1 cuốn — điều này khớp 100% với thực tế vì cuốn đó không còn nằm trên kệ nữa!
- **Khi khách Hủy đơn (Cancelled):** Backend nhả `Reserved` - 1. `AvailableStock` tự động tăng lại, khớp với việc khách A sẽ bỏ lại sách ra quầy.

---

## 1. Luồng Mua Hàng & Giữ Chỗ Kho (Kiosk Checkout Pipeline)

Đây là quy trình diễn ra khi khách hàng bấm nút "Thanh toán" trên Kiosk.

### Bước 1: Validate Giỏ hàng
- Backend nhận request gồm mảng `items` (bookId, quantity) và `pointsToUse` (nếu khách là thành viên).
- Lặp qua từng sách để kiểm tra tồn kho khả dụng: `Available = StockQuantity - ReservedQuantity`.
- Nếu có cuốn sách nào `Available < quantity` yêu cầu -> Ngưng toàn bộ, ném lỗi 409 (Message: `"Sách [Tên sách] đã hết hàng..."`).

### Bước 2: Khóa dòng để tính toán & chống Deadlock
- Backend **LUÔN SORT** mảng `items` theo `BookId` từ nhỏ đến lớn trước khi mở Transaction.
- Dùng `ExecuteUpdate` (Atomic) để cộng số lượng sách vào cột `ReservedQuantity`.

### Bước 3: Áp dụng Khuyến mãi (Promotion)
- Hệ thống lấy tất cả CTKM đang ở trạng thái `IsActive = true` và `Ngày hiện tại` nằm trong khoảng StartDate - EndDate.
- Tính tổng tiền hàng (`SubTotal`).
- Lọc ra các CTKM thỏa mãn điều kiện `MinOrderValue <= SubTotal`.
- Nếu có nhiều CTKM thỏa mãn, **tự động chọn CTKM có `DiscountAmount` lớn nhất** để có lợi nhất cho khách.

### Bước 4: Trừ Điểm & Tính Tổng Tiền
- Đảm bảo `pointsToUse` <= Số điểm hiện có của Member.
- Quy đổi: 1 điểm = 1.000 VNĐ.
- Tính toán: `TotalAmount = SubTotal - DiscountAmount - (pointsToUse * 1000)`.
- Đảm bảo `TotalAmount >= 0` (Nếu < 0 thì set bằng 0).

### Bước 5: Lưu Đơn Hàng & Sinh QR Code
- Lưu bảng `Orders` (Status: `Pending`).
- Lưu bảng `OrderDetails`.
- Lấy `TotalAmount` và `OrderCode` ghép vào link API của SePay để sinh link ảnh QR Động.
- Commit Transaction. Trả về `sepayQrCodeUrl` cho Kiosk.

---

## 2. Luồng Xử Lý Webhook Thanh Toán (SePay Pipeline)

Khi khách dùng app ngân hàng quét mã QR và chuyển khoản thành công, SePay sẽ bắn một HTTP POST request (Webhook) về Backend.

### Bước 1: Xác thực & Chống trùng lặp (Idempotent)
- Xác thực `Signature` ở HTTP Header để đảm bảo request thực sự đến từ SePay.
- Trích xuất `ReferenceCode` (mã tham chiếu giao dịch của ngân hàng) từ payload.
- Kiểm tra trong bảng `PaymentTransactions`, nếu `ReferenceCode` đã tồn tại -> Đây là webhook gọi lặp -> Trả về HTTP 200 OK ngay lập tức và bỏ qua.

### Bước 2: Đối soát dữ liệu
- Dùng Regex để tìm `OrderCode` trong chuỗi `transactionContent`.
- Query lấy đơn hàng từ DB lên. Nếu không thấy -> Bỏ qua.
- Nếu `OrderStatus != Pending` -> Bỏ qua.
- **Kiểm tra số tiền (`amountIn` vs `Order.TotalAmount`):**
  - **Trường hợp LỆCH (Khách tự sửa số tiền chuyển sai):** 
    - Giữ `OrderStatus` = `Pending` (không đổi trạng thái).
    - Insert vào `PaymentTransactions` để lưu vết giao dịch lệch.
    - Cảnh báo ra màn hình Kiosk ("Chờ nhân viên hỗ trợ") và bắn Notification cho Staff Admin. KHÔNG nhả sách.
  - **Trường hợp ĐÚNG:** Sang Bước 3.

> **💡 LƯU Ý QUAN TRỌNG VỀ VIỆC KHÓA CỨNG SỐ TIỀN QR (CHỐNG GIAN LẬN)**
> - Nếu hệ thống dùng **Mã VietQR Chuyển khoản cá nhân** (như gói miễn phí của SePay/Casso), mã QR mang cờ lệnh "Chuyển tiền". Nhiều App ngân hàng sẽ cho phép khách chạm vào và sửa số tiền. Do đó bắt buộc phải có luồng bắt lỗi **"Trường hợp LỆCH"** như trên.
> - **Khuyến nghị nâng cấp:** Để khóa cứng 100% ô nhập tiền khiến khách tuyệt đối không thể sửa, dự án nên sử dụng API của các **Cổng thanh toán Merchant QR** hoặc **Tài khoản ảo (Virtual Account)**:
>   1. **PayOS (Khuyên dùng cho Đồ án/Dự án mới):** Miễn phí, dễ tích hợp với C#/.NET. Mã QR sinh ra qua hệ thống Tài khoản ảo định danh sẽ khóa cứng số tiền trên mọi app ngân hàng.
>   2. **VNPAY / MoMo / ZaloPay:** Mã QR chuẩn Merchant (Thanh toán hóa đơn). Mọi app khi quét trúng sẽ bị vô hiệu hóa ô nhập tiền, chỉ có thể bấm Xác nhận/Hủy. (Yêu cầu ĐKKD).
>   3. **SePay / Casso (Gói Doanh nghiệp - Virtual Account):** Cấp 1 số Tài khoản ảo riêng cho từng hóa đơn. Khách chuyển sai số tiền sẽ bị ngân hàng từ chối và hoàn tiền tự động ngay lập tức.

### Bước 3: Hoàn tất Đơn hàng (Thành công)
Mở Transaction:
1. Insert vào bảng `PaymentTransactions`.
2. Đổi `OrderStatus` = `Paid` (2), Cập nhật `CompletedAt`.
3. **Cập nhật Kho thực sự:** 
   - `StockQuantity = StockQuantity - quantity`
   - `ReservedQuantity = ReservedQuantity - quantity`
4. **Tích điểm (Nếu có Member):**
   - Trừ điểm khách đã dùng: Insert `PointTransactions` (Type: `Redeemed`).
   - Cộng thêm điểm thưởng mới: mọi 10.000đ TotalAmount = 1 điểm (làm tròn xuống).
   - Cập nhật `Members.Points` mới nhất.
5. Gửi tín hiệu (SignalR/WebSocket hoặc Kiosk Polling) báo Kiosk hiển thị màn hình thành công và ra lệnh máy in in hóa đơn.
6. Commit Transaction.

---

## 3. Luồng Dọn Dẹp Đơn Hàng Treo (Background Worker)

Khách ra Kiosk tạo đơn hàng (đã giữ chỗ kho) nhưng đứng ngó rồi bỏ đi, không thanh toán. Chúng ta cần Background Job (Dùng `IHostedService` hoặc `Hangfire` trong C#) để nhả kho.

### Logic chạy định kỳ:
- Chạy mỗi 1 phút / lần.
- Quét bảng `Orders` tìm các đơn:
  - `OrderStatus == Pending`
  - `CreatedAt` < Thời điểm hiện tại trừ đi **`OrderTimeoutMinutes`** phút (config trong `appsettings.json`, mặc định **4 phút**).
  - Áp dụng cho **cả Kiosk lẫn Quầy** (phòng nhân viên tạo đơn nháp quầy rồi bỏ quên).
- Mở Transaction lặp qua từng đơn:
  - Cập nhật `OrderStatus = Cancelled`.
  - Nhả tồn kho: `ReservedQuantity = ReservedQuantity - quantity`.
  - Commit.
