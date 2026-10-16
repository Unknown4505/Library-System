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

- **Base URL:** `https://localhost:7111/` (HTTP fallback: `http://localhost:5014/`)
- **Content-Type:** `application/json`
- **Xác thực:** 
  - Kiosk gọi API: Gửi Header `X-Api-Key: <kiosk_secret_key>`
  - CMS (nếu gọi bằng js/ajax): `Authorization: Bearer <JWT_Token>`
  - Webhook SePay: Header chứa `Signature` xác thực.

---

## 2. Chuẩn Response (Standard Wrapper)

Mọi API trả về (kể cả lỗi) đều phải được wrap trong object `ApiResponseDto<T>`:

```json
{
  "success": true,               // true/false
  "code": "SUCCESS",             // Mã lỗi/thành công chuẩn hóa
  "message": "Thành công",       // Lời nhắn hiển thị cho người dùng (nếu cần)
  "data": { ... }                // Payload thực tế (nếu success=true)
}
```

HTTP status và `code` là hai lớp khác nhau: HTTP status mô tả kết quả giao thức; `code` là mã máy đọc trong response body. Ví dụ lỗi chuẩn:

```json
{
  "success": false,
  "code": "NOT_FOUND",
  "message": "Không tìm thấy thông tin đơn hàng.",
  "data": null
}
```

**Đối với API có phân trang (Pagination):**
`data` sẽ trả về `PaginatedResultDto<T>`:
```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "Lấy dữ liệu thành công",
  "data": {
    "items": [ { ... }, { ... } ],
    "totalCount": 105,
    "page": 1,
    "pageSize": 20,
    "totalPages": 6,
    "hasNextPage": true
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

#### Lấy danh sách Danh mục
- **Endpoint:** `GET /api/categories`
- **Response (Success - 200 OK):** Mảng các object chứa `categoryId`, `name`, `description`.

#### Lấy danh sách Khu vực kệ
- **Endpoint:** `GET /api/areas`
- **Response (Success - 200 OK):** Mảng các object chứa `areaId`, `name`, `mapCoordinates` (Tọa độ trên UI map).

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
  "pointsToUse": 50,               // Số điểm khách muốn trừ (tối đa 100 điểm/đơn)
  "items": [
    { "bookId": 1, "quantity": 2 }, // Lưu ý: Kiosk gọi /books/barcode/{barcode} lấy bookId trước
    { "bookId": 5, "quantity": 1 }
  ]
}
```
- **Xử lý Backend:** 
  - Tự động quét Promotion (`IsActive = true`, ngày hợp lệ) và áp dụng loại ngon nhất cho đơn hàng.
  - Validate `pointsToUse` không âm, không vượt quá **100 điểm/đơn** và không vượt quá số điểm hiện có của thành viên.
  - Quy đổi điểm: **1 điểm = 1.000 VNĐ**.
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
- **Trạng thái:** `Planned` — chưa có endpoint trên `main`.
- **Endpoint:** `POST /api/orders/counter/checkout`
- **Auth:** `Bearer <token>` (Role Staff)
- **Request Body:** Giống Kiốsk, thêm `promotionId` (nhân viên bấm áp KM thủ công) và `paymentMethod` (Cash / QR).
- **Xử lý Backend:**
  - Giữ chỗ kho: `ReservedQuantity += quantity` (giống Kiosk).
  - Lưu Order trạng thái **`Pending`**.
- **Response:** Trả về `orderId`, thông tin giỏ hàng, tổng tiền để nhân viên xác nhận với khách.

#### Xác nhận thanh toán tại quầy (Thu tiền mặt hoặc QR khách đã trả)
- **Trạng thái:** `Planned` — chưa có endpoint trên `main`.
- **Endpoint:** `POST /api/orders/counter/{orderId}/confirm-payment`
- **Auth:** `Bearer <token>` (Role Staff)
- **Xử lý Backend:**
  - Kiểm tra `OrderStatus == Pending`, nếu không → 409.
  - Đổi `OrderStatus = Paid`, cập nhật `CompletedAt`.
  - Trừ kho: `StockQuantity -= quantity`, `ReservedQuantity -= quantity`.
  - Tích điểm cho Member (nếu có).
- **Response:** Trả về thông tin Order hoàn chỉnh để in bill.

#### Hủy đơn nháp tại quầy (Khách từ chối)
- **Trạng thái:** `Planned` — chưa có endpoint trên `main`.
- **Endpoint:** `POST /api/orders/counter/{orderId}/cancel`
- **Auth:** `Bearer <token>` (Role Staff)
- **Xử lý Backend:**
  - Kiểm tra `OrderStatus == Pending`, nếu không → 409.
  - Đổi `OrderStatus = Cancelled`.
  - Nhả kho: `ReservedQuantity -= quantity`.

#### Kiểm tra trạng thái đơn (Kiosk polling sau khi hiện QR)
- **Endpoint:** `GET /api/orders/kiosk/{orderId}/payment-status`
- **Auth:** `X-Api-Key`
- **Response:**
```json
"data": {
  "orderId": 105,
  "orderCode": "ORD-20261101-0105",
  "status": "Paid" // Enum: Pending, Paid, Cancelled
}
```

#### Lấy hóa đơn đã thanh toán (contract đã chốt, Backend chưa implement)
- **Endpoint:** `GET /api/orders/kiosk/{orderId}/receipt`
- **Auth:** `X-Api-Key`
- **Nguồn dữ liệu:** Backend đọc `Orders` và `OrderDetails`; không nhận danh sách sách, giá hoặc tổng tiền từ Kiosk.
- **Điều kiện:** Chỉ order có `OrderStatus = Paid` và `CompletedAt` hợp lệ mới được trả hóa đơn.
- **Response (200 OK):** `ApiResponseDto<ReceiptDto>`.

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "Lấy hóa đơn thành công.",
  "data": {
    "orderId": 105,
    "orderCode": "ORD-20261101-0105",
    "createdAt": "2026-11-01T03:14:00Z",
    "completedAt": "2026-11-01T03:15:00Z",
    "items": [
      {
        "bookId": 1,
        "bookTitle": "Cây Cam Ngọt Của Tôi",
        "quantity": 2,
        "unitPriceAtTime": 105000,
        "lineTotal": 210000
      }
    ],
    "subTotal": 210000,
    "discountAmount": 50000,
    "pointsUsed": 50,
    "pointsUsedAmount": 50000,
    "totalAmount": 110000,
    "paymentMethod": "QR"
  }
}
```

- Order không tồn tại: `404 NOT_FOUND`.
- Order còn `Pending` hoặc đã `Cancelled`: `409 CONFLICT`; không trả payload hóa đơn.
- Không trả `CostPrice`, số điện thoại hoặc dữ liệu cá nhân thành viên.
- `UnitPriceAtTime`, `Quantity`, `LineTotal` và các tổng tiền lấy từ snapshot hiện có. Schema hiện tại chưa có `BookTitleAtTime`; `BookTitle` tạm đọc qua quan hệ `OrderDetails.Book`. Vì vậy việc đổi tên sách sau khi bán có thể đổi phần tiêu đề khi in lại. Không thêm cột khi checkout hiện tại chưa ghi snapshot; Thiện phải ghi snapshot trong cùng transaction nếu triển khai mở rộng schema sau này.

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
  - Check Idempotent: Nếu `referenceCode` đã lưu trong `PaymentTransactions` -> không xử lý lại và trả `200` với response success; không dùng `409 DUPLICATE_WEBHOOK`.
  - So khớp `amountIn` với `Order.TotalAmount`:
    - **Nếu ĐÚNG:** Cập nhật Order -> `Paid`. Trừ kho thật sự (`StockQuantity -= qty`, `ReservedQuantity -= qty`). Cộng điểm cho Member.
    - **Nếu LỆCH (Khách tự sửa số tiền):** Vẫn giữ Order -> `Pending`. Insert `PaymentTransactions` để lưu vết giao dịch lệch. Gửi Notification cho Staff kiểm tra và xử lý thủ công. Kiosk hiển thị màn hình "Chờ nhân viên hỗ trợ". Không nhả sách.

#### F3 mô phỏng thanh toán thành công trong Development (contract đã chốt, Backend chưa implement)
- **Endpoint:** `POST /api/payments/dev-simulate-success/{orderCode}`
- **Auth:** `X-Api-Key` theo cùng `ApiSettings:ApiKey` của Kiosk; endpoint phải tự áp dụng xác thực nếu middleware hiện tại chưa bao phủ `/api/payments/*`.
- **Request body:** Không có. Client không được gửi payload webhook SePay giả.
- **Development:** Route chỉ được map khi `IHostEnvironment.IsDevelopment()` là `true`.
- **Production/Release:** Không map route, kết quả phải là `404`; không có config/bypass để bật lại trong Production.
- **Xử lý:** Tìm order theo `orderCode`, chỉ hoàn tất order `Pending`, rồi gọi đúng một transaction/finalization path dùng chung với webhook thật. Transaction phải ghi một reference DEV duy nhất, chuyển `Paid`, trừ `StockQuantity` và `ReservedQuantity` đúng một lần, cập nhật điểm đúng một lần và đặt `CompletedAt` UTC.
- **Idempotency:** Gọi lại với order đã `Paid` trả `200` cùng trạng thái hiện tại, không lặp tác dụng phụ. Order `Cancelled` trả `409`. Order không tồn tại trả `404`.
- **Response (200 OK):** `ApiResponseDto<PaymentStatusDto>` với `orderId`, `orderCode`, `status = "Paid"`.
- **Security:** Không ghi API key vào log/source; không gọi ngược public webhook; không tạo giao dịch ngân hàng thật.

---

### 3.5 Quản lý thiết bị (Kiosk Heartbeat)

- **Trạng thái:** Backend đã có trên `main` qua PR #17; Heartbeat worker của Kiosk vẫn ở PR #11/HOLD và chưa được nghiệm thu runtime.
- **Endpoint:** `POST /api/kiosk/heartbeat`
- **Auth:** `X-Api-Key`
- **Request Body:**
```json
{
  "kioskId": 1,
  "status": 1,             // Enum: 1=Online, 2=Offline, 3=Error
  "errorCode": null,       // "CAM_DISCONNECTED", "PRINTER_OUT_OF_PAPER"
  "errorMessage": null
}
```
- **Validation:** `kioskId` là số nguyên dương đã tồn tại; `status` chỉ nhận `1`, `2`, `3`. Giá trị khác phải trả `400 BAD_REQUEST`; ID không tồn tại trả `404 NOT_FOUND`.
- **Xử lý Backend:** Update `LastPingAt` bằng UTC và tạo `KioskIncident` nếu có lỗi chưa fix. `kioskId` là khóa database được cấp ở bước đăng ký/cấu hình thiết bị và lưu trong cấu hình Kiosk; không hardcode `1` và không gửi mã hiển thị như `KIOSK-01`.

### 3.6 Admin Catalog CRUD (contract nghiệm thu Task 9.1)

- Tất cả endpoint ghi dữ liệu yêu cầu JWT; `Admin` được tạo/sửa/vô hiệu hóa, `Staff` chỉ có quyền được cấp trong `docs/07-security.md`.
- Categories: `POST /api/categories`, `PUT /api/categories/{id}`, `DELETE /api/categories/{id}` dùng `CreateCategoryDto`/`UpdateCategoryDto` (`name` bắt buộc, tối đa 255 ký tự).
- Areas: `POST /api/areas`, `PUT /api/areas/{id}`, `DELETE /api/areas/{id}` dùng `CreateAreaDto`/`UpdateAreaDto` (`name` bắt buộc, tối đa 255; `mapCoordinates` tối đa 500 ký tự).
- Xóa/vô hiệu hóa phải tôn trọng FK: Category đang được Book tham chiếu không được hard-delete; Area bị xóa áp dụng quan hệ `SET NULL` theo schema. Ưu tiên deactivate nếu entity có `IsActive`.
- Categories/Areas write endpoints đã được bổ sung trên nhánh `c8` với JWT role `Admin`, response wrapper, duplicate-name validation và xử lý FK khi xóa. Books write endpoints trên `main` vẫn chưa có authorization tương ứng, nên Task 9.1 chưa đủ điều kiện nghiệm thu toàn bộ.

### 3.7 Reports (contract typed response)

- `GET /api/reports/revenue?year={year}` → `ApiResponseDto<RevenueReportDto>` gồm `year` và `data[] { month, revenue, totalOrders }`.
- `GET /api/reports/top-books?top={n}` → `ApiResponseDto<List<TopSellingBookDto>>` gồm `bookId`, `title`, `imageUrl`, `totalSold`, `totalRevenue`.
- Cả hai endpoint chỉ dành cho JWT role `Admin`; tiền chỉ tính từ order `Paid`.
- Dashboard trạng thái thiết bị dùng contract `KioskStatusSummaryDto { online, offline, error, openIncidents }`. Backend hiện chưa có endpoint tổng hợp tương ứng; không giả lập dữ liệu CMS và không tự coi contract này là endpoint đã tồn tại.

### 3.8 Auth, Users, Employees và SalaryPayments — PLANNED

Các route dưới đây chỉ là namespace contract mục tiêu, **chưa phải endpoint đã tồn tại** và cần review riêng trước khi member triển khai:

- `POST /api/auth/login`: xác thực tài khoản active, trả JWT có role `Admin`, `Manager` hoặc `Staff`.
- `/api/users`: quản lý tài khoản; Admin-only.
- `/api/employees`: hồ sơ nhân viên; Admin/Manager theo policy, Staff chỉ được đọc hồ sơ của chính mình khi có identity mapping.
- `/api/salary-payments`: sổ lương tháng; API/DTO/controller/service đều **PLANNED**.

**Contract tạo Draft — APPLICATION REQUIRED:**

- Chỉ Admin được tạo; mỗi `(EmployeeId, PayYear, PayMonth)` có tối đa một phiếu (`409` khi trùng).
- `PayMonth` phải thuộc 1..12; theo **APPROVED DESIGN HR-DEC-01**, `PayYear` phải thuộc 2000..2100 (bao gồm hai đầu) khi tạo hoặc cập nhật Draft. Ngoài khoảng trả validation error theo response wrapper hiện có.
- Các khoản tiền không âm; Backend tính và kiểm `NetSalary = BaseSalarySnapshot + AllowanceAmount - DeductionAmount`, kết quả không âm.
- Phải xác định `BaseSalarySnapshot` hợp lệ. Nếu `CurrentBaseSalary` null thì trả validation error hoặc áp dụng quy tắc riêng đã được Leader duyệt; không ngầm đổi thành 0.
- Draft phải có `Status=Draft`, `PaidByUserId=null`, `PaidAt=null`.

**Contract xác nhận Paid — APPLICATION REQUIRED:**

- Chỉ JWT role `Admin`; chỉ chuyển `Draft → Paid`.
- Ghi `PaidByUserId` từ UserId của Admin trong token và `PaidAt` theo UTC, cùng thay đổi status trong một transaction.
- Request lặp không được xác nhận Paid lần hai. Phiếu Paid không được sửa/xóa qua API thông thường.

**Quyền đọc — APPLICATION REQUIRED:**

- Admin xem toàn bộ.
- Manager chỉ được xem phiếu Paid của chính mình khi mapping `Employees.UserId` khớp UserId JWT; không xem Draft hoặc phiếu người khác.
- Staff chỉ xem phiếu **Paid** của chính Employee liên kết User khi self-service được triển khai.
- Manager/Staff không có Employee liên kết thì deny. Backend lấy identity từ JWT và enforce row ownership; không tin EmployeeId/UserId do client gửi và không cho đổi ID trong URL để đọc lương người khác.
- Manager không được xác nhận Paid. Quyền xác nhận Paid chỉ thuộc Admin dù Manager có quyền quản lý vận hành.

**DB ENFORCED hiện tại:** unique kỳ lương, tháng 1..12, tiền không âm, status 1/2 và FK/RESTRICT. Theo **APPROVED DESIGN HR-DEC-01/02/03**, Backend phải enforce PayYear 2000..2100, công thức `NetSalary`, actor/time theo status, state transition/concurrency và JWT row authorization. Leader chốt giữ migration hiện tại, không thêm SQL CHECK trong V6.3. Validation trùng `EmployeeCode`, trùng kỳ lương hoặc input sai trả `409`/`400` theo nguyên nhân; không trả password hash hoặc lương người khác. Đây là ledger ghi nhận trả lương, không phải API chuyển tiền ngân hàng; toàn bộ Salary API/Service vẫn **PLANNED**.

---

## 4. Bảng mã Lỗi Chuẩn (Error Codes)

Nếu `success = false`, Backend nên trả về HTTP Status Code chuẩn:
- `400 Bad Request`: Validation Error (Gửi thiếu/sai tham số).
- `401 Unauthorized`: Thiếu thông tin xác thực (không có Token/API Key) hoặc JWT không hợp lệ/hết hạn.
- `403 Forbidden`: API Key được gửi nhưng sai, hoặc danh tính đã xác thực không đủ quyền.
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
