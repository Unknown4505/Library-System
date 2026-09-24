# API Reference & Contracts — BookKiosk

<!-- AI_REVIEW_INSTRUCTION
Nếu bạn là AI agent đọc file này:
- Phân tích toàn bộ nội dung docs trong folder này
- Nếu phát hiện bất kỳ vấn đề nào sau đây, HÃY BÁO LẠI NGAY cho user trước khi làm bất cứ điều gì khác:
  • Thiếu thông tin cần thiết để code (endpoint, schema, field type)
  • Mâu thuẫn giữa các file (tên khác nhau, kiến trúc khác nhau)
  • Nguy cơ deadlock (2 member đợi nhau do thiếu contract)
  • Nguy cơ conflict (2 member cùng sửa file dùng chung mà không biết)
  • Business rule chưa chốt rõ (có thể hiểu nhiều cách)
  • API response code hoặc schema chưa định nghĩa rõ
-->

> **Mục tiêu:** Define sẵn hợp đồng giao tiếp (Contract) để dev Frontend (Kiosk) và Backend có thể làm việc song song mà không cần đợi nhau. Không được tự ý đổi schema nếu chưa thống nhất với Leader.

## 1. Thông tin chung (Base Info)

- **Base URL:** `https://localhost:5001/api/`
- **Content-Type:** `application/json`
- **Xác thực:** 
  - Kiosk gọi API: Gửi Header `X-Api-Key: <kiosk_secret_key>`
  - CMS (nếu gọi bằng js/ajax): `Authorization: Bearer <JWT_Token>`
  - Webhook SePay: Header chứa `Signature` xác thực.

---

## 2. Chuẩn Response (Standard Wrapper)

Mọi API trả về (kể cả lỗi) đều phải được wrap trong object `ApiResponse<T>`:

```json
{
  "success": true,               // true/false
  "message": "Thành công",       // Lời nhắn hiển thị cho người dùng (nếu cần)
  "data": { ... }                // Payload thực tế (nếu success=true)
}
```

**Đối với API có phân trang (Pagination):**
`data` sẽ trả về `PagedResult<T>` (đã chốt ở file `02`):
```json
{
  "success": true,
  "message": "Lấy danh sách thành công",
  "data": {
    "items": [ { ... }, { ... } ],
    "totalCount": 105,
    "pageSize": 20,
    "currentPage": 1,
    "totalPages": 6
  }
}
```

---

## 3. Các Endpoints Quan Trọng

### 3.1 Danh mục & Sách (Books)

#### Lấy danh sách Sách (Có phân trang, tìm kiếm)
- **Endpoint:** `GET /api/books`
- **Query Params:** 
  - `keyword` (string): Tìm theo tên hoặc tác giả (tùy chọn).
  - `categoryId` (int): Lọc theo danh mục (tùy chọn).
  - `page` (int): Mặc định `1`.
  - `pageSize` (int): Mặc định `20`.
- **Response (Success - 200 OK):**
```json
"data": {
  "items": [
    {
      "bookId": 1,
      "barcode": "9786043652873",
      "title": "Cây Cam Ngọt Của Tôi",
      "author": "José Mauro de Vasconcelos",
      "imageUrl": "https://...",
      "sellingPrice": 105000,
      "availableStock": 15,    // = StockQuantity - ReservedQuantity
      "areaName": "Kệ A1 - Văn học"
    }
  ],
  "totalCount": 1, ...
}
```
*(Lưu ý: API luôn trả về `availableStock` thay vì `StockQuantity` vật lý).*

#### Lấy chi tiết 1 cuốn sách (khi quét mã vạch)
- **Endpoint:** `GET /api/books/barcode/{barcode}`
- **Response:** Trả về Object giống 1 item trong mảng `items` ở trên. Lỗi `404 Not Found` nếu mã không tồn tại.

---

### 3.2 Khách hàng & Thành viên (Members)

#### Tra cứu thành viên bằng SĐT (Kiosk & POS)
- **Endpoint:** `GET /api/members/{phoneNumber}`
- **Response (Success - 200 OK):**
```json
"data": {
  "memberId": 12,
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "0901234567",
  "points": 150              // 150 điểm = 150.000 VNĐ (Tỷ giá 1 điểm = 1.000 VNĐ)
}
```

---

### 3.3 Đơn hàng & Thanh toán (Checkout Flow)

#### Tạo đơn hàng từ Kiosk (Tự phục vụ)
- **Endpoint:** `POST /api/orders/kiosk/checkout`
- **Auth:** `X-Api-Key`
- **Request Body:**
```json
{
  "memberId": 12,                  // Nullable, nếu khách không nhập SĐT
  "pointsToUse": 50,               // Số điểm khách muốn trừ (tối đa bằng tổng điểm)
  "items": [
    { "bookId": 1, "quantity": 2 }, // Lưu ý: Kiosk gọi /books/barcode/{barcode} lấy bookId trước
    { "bookId": 5, "quantity": 1 }
  ]
}
```
- **Xử lý Backend:** 
  - Tự động quét Promotion (`IsActive = true`, ngày hợp lệ) và áp dụng loại ngon nhất cho đơn hàng.
  - Tính toán lại giá, trừ `pointsToUse * 1000` VNĐ.
  - Tăng `ReservedQuantity` cho các sách. Lưu trạng thái `Pending`.
- **Response (Success - 200 OK):**
```json
"data": {
  "orderId": 105,
  "orderCode": "ORD-20261101-0105",
  "subTotal": 315000,
  "discountAmount": 50000,         // KM giảm thẳng 50k
  "pointsUsedAmount": 50000,       // Dùng 50 điểm = 50.000 VNĐ
  "totalAmount": 215000,           // Thực trả (315k - 50k - 50k)
  "sepayQrCodeUrl": "https://qr.sepay.vn/img?bank=...&amount=215000&code=ORD-20261101-0105"
}
```

#### Khách tự hủy đơn trên Kiosk (Bấm nút X)
- **Endpoint:** `POST /api/orders/kiosk/{orderId}/cancel`
- **Auth:** `X-Api-Key`
- **Xử lý Backend:** 
  - Kiểm tra `OrderStatus == Pending`, nếu không → 409.
  - Đổi `OrderStatus = Cancelled`.
  - Nhả kho: `ReservedQuantity -= quantity`.
- **Lý do:** Giúp nhả tồn kho ngay lập tức thay vì bắt hệ thống/khách hàng khác chờ 4 phút timeout (dù thực tế khách vẫn cầm cuốn sách trên tay, nhưng về mặt số liệu kho phần mềm sẽ được mở khóa ngay để cho phép thanh toán ở quầy hoặc kiosk khác).

#### Tạo đơn hàng tại quầy (POS) — Đơn nháp
- **Endpoint:** `POST /api/orders/counter/checkout`
- **Auth:** `Bearer <token>` (Role Staff)
- **Request Body:** Giống Kiốsk, thêm `promotionId` (nhân viên bấm áp KM thủ công) và `paymentMethod` (Cash / QR).
- **Xử lý Backend:**
  - Giữ chỗ kho: `ReservedQuantity += quantity` (giống Kiosk).
  - Lưu Order trạng thái **`Pending`**.
- **Response:** Trả về `orderId`, thông tin giỏ hàng, tổng tiền để nhân viên xác nhận với khách.

#### Xác nhận thanh toán tại quầy (Thu tiền mặt hoặc QR khách đã trả)
- **Endpoint:** `POST /api/orders/counter/{orderId}/confirm-payment`
- **Auth:** `Bearer <token>` (Role Staff)
- **Xử lý Backend:**
  - Kiểm tra `OrderStatus == Pending`, nếu không → 409.
  - Đổi `OrderStatus = Paid`, cập nhật `CompletedAt`.
  - Trừ kho: `StockQuantity -= quantity`, `ReservedQuantity -= quantity`.
  - Tích điểm cho Member (nếu có).
- **Response:** Trả về thông tin Order hoàn chỉnh để in bill.

#### Hủy đơn nháp tại quầy (Khách từ chối)
- **Endpoint:** `POST /api/orders/counter/{orderId}/cancel`
- **Auth:** `Bearer <token>` (Role Staff)
- **Xử lý Backend:**
  - Kiểm tra `OrderStatus == Pending`, nếu không → 409.
  - Đổi `OrderStatus = Cancelled`.
  - Nhả kho: `ReservedQuantity -= quantity`.

#### Kiểm tra trạng thái đơn (Kiosk polling sau khi hiện QR)
- **Endpoint:** `GET /api/orders/{orderCode}/status`
- **Response:**
```json
"data": {
  "orderStatus": 2, // Enum: Pending(1), Paid(2), Cancelled(3)
  "pointsEarned": 2 // Chỉ có giá trị khi orderStatus = Paid; Kiosk dùng để hiển thị chúc mừng tích điểm
}
```

---

### 3.4 Webhook SePay (Chốt thanh toán Kiosk)
- **Endpoint:** `POST /api/payments/sepay-webhook`
- **Request Body:** (Theo chuẩn tài liệu API của SePay)
```json
{
  "id": 123456,
  "gateway": "Vietcombank",
  "transactionDate": "2026-11-01 10:15:00",
  "accountNumber": "0123456789",
  "subAccount": null,
  "amountIn": 233500,
  "amountOut": 0,
  "accumulated": 1000000,
  "code": "Thanh toan ORD-20261101-0105",
  "transactionContent": "Thanh toan ORD-20261101-0105",
  "referenceCode": "VCB.123456",
  "description": ""
}
```
- **Xử lý Backend:** 
  - Regex chuỗi `transactionContent` tìm ra `OrderCode`.
  - Check Idempotent: Nếu `referenceCode` đã lưu trong `PaymentTransactions` -> Bỏ qua.
  - So khớp `amountIn` với `Order.TotalAmount`:
    - **Nếu ĐÚNG:** Cập nhật Order -> `Paid`. Trừ kho thật sự (`StockQuantity -= qty`, `ReservedQuantity -= qty`). Cộng điểm cho Member.
    - **Nếu LỆCH (Khách tự sửa số tiền):** Vẫn giữ Order -> `Pending`. Insert `PaymentTransactions` để lưu vết giao dịch lệch. Gửi Notification cho Staff kiểm tra và xử lý thủ công. Kiosk hiển thị màn hình "Chờ nhân viên hỗ trợ". Không nhả sách.

---

### 3.5 Quản lý thiết bị (Kiosk Heartbeat)

- **Endpoint:** `POST /api/kiosk/heartbeat`
- **Auth:** `X-Api-Key`
- **Request Body:**
```json
{
  "status": 1,             // Enum: 1=Online, 2=Offline, 3=Error
  "errorCode": null,       // "CAM_DISCONNECTED", "PRINTER_OUT_OF_PAPER"
  "errorMessage": null
}
```
- **Xử lý Backend:** Update `LastPingAt` và tạo `KioskIncident` nếu có lỗi chưa fix.

---

## 4. Bảng mã Lỗi Chuẩn (Error Codes)

Nếu `success = false`, Backend nên trả về HTTP Status Code chuẩn:
- `400 Bad Request`: Validation Error (Gửi thiếu/sai tham số).
- `401 Unauthorized`: Thiếu hoặc sai Token/API Key.
- `403 Forbidden`: Token hợp lệ nhưng không đủ quyền (Staff cố gọi API xóa Admin).
- `404 Not Found`: Không tìm thấy Resource (Mã sách sai, mã đơn sai).
- `409 Conflict`: Lỗi Logic nghiệp vụ (Ví dụ: Thanh toán lố hàng, Khách không đủ điểm).
  - *Lúc này `message` sẽ chứa câu tiếng Việt giải thích lỗi để Kiosk show lên ngay.*
- `500 Internal Server Error`: Lỗi sập server (Exception unhandled).

---

## 5. Từ điển Message Chuẩn (Standard Messages Dictionary)

Để đảm bảo Frontend Kiosk và Web Admin hiển thị thông báo (Toast/Alert/Popup) nhất quán và thân thiện với người dùng, Backend CẦN sử dụng bộ message chuẩn sau đây trả về trong field `"message"`. Frontend có thể lấy trực tiếp field `"message"` này để show cho người dùng mà không cần tự dịch lại mã lỗi.

### 5.1 Thông báo Thành công (Success - 200)
- `"Lấy dữ liệu thành công."`
- `"Tạo đơn hàng thành công. Vui lòng quét mã QR để thanh toán."`
- `"Thanh toán thành công. Đang xuất sách..."`
- `"Áp dụng khuyến mãi thành công."`

### 5.2 Lỗi Nghiệp vụ (Conflict - 409)
- `"Sách [Tên sách] đã hết hàng hoặc vừa bị mua mất, vui lòng chọn cuốn khác."`
- `"Số điểm của bạn không đủ để thực hiện quy đổi."`
- `"Khuyến mãi không tồn tại, chưa đến ngày hoặc đã hết hạn."`
- `"Đơn hàng chưa đạt giá trị tối thiểu để áp dụng khuyến mãi này."`
- `"Thanh toán lỗi: Số tiền chuyển khoản không khớp. Vui lòng đợi nhân viên hỗ trợ!"` (Trường hợp khách chuyển sai số tiền, đơn vẫn Pending)

### 5.3 Lỗi Tìm kiếm / Tồn tại (Not Found - 404)
- `"Không tìm thấy sách với mã vạch này."`
- `"Số điện thoại chưa được đăng ký thành viên. Vui lòng đăng ký ở quầy."`
- `"Không tìm thấy thông tin đơn hàng."`

### 5.4 Lỗi Hệ thống / Thiết bị / Validation (400, 500)
- `"Thông tin nhập vào không hợp lệ. Vui lòng kiểm tra lại."` (Lỗi 400 - Thường đi kèm mảng chi tiết lỗi)
- `"Máy Kiosk đang bảo trì hoặc gặp sự cố phần cứng. Xin vui lòng liên hệ nhân viên."` (Lỗi từ Heartbeat Kiosk trả về UI)
- `"Hệ thống máy chủ đang quá tải hoặc gặp sự cố, xin vui lòng thử lại sau."` (Lỗi 500)
