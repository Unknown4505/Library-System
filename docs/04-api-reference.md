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
  "success": true,
  "code": "SUCCESS",
  "message": "Thành công",
  "data": {}
}
```

`success` là boolean; `code` và `message` là string không rỗng; `data` là payload thực tế hoặc `null` khi lỗi.

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
    "items": [{}, {}],
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
  - `pageSize` (int): Mặc định `12` theo source hiện tại.
- **Response (Success - 200 OK):**
```json
{
  "data": {
    "items": [
      {
        "bookId": 1,
        "barcode": "9786043652873",
        "title": "Cây Cam Ngọt Của Tôi",
        "author": "José Mauro de Vasconcelos",
        "imageUrl": "https://example.test/uploads/books/cay-cam.jpg",
        "sellingPrice": 105000,
        "availableStock": 15,
        "areaName": "Kệ A1 - Văn học"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 12,
    "totalPages": 1,
    "hasNextPage": false
  }
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
{
  "data": {
    "memberId": 12,
    "fullName": "Nguyễn Văn A",
    "phoneNumber": "0901234567",
    "points": 150
  }
}
```

`points=150` tương đương 150.000 VNĐ theo tỷ giá 1 điểm = 1.000 VNĐ.

---

### 3.3 Đơn hàng & Thanh toán (Checkout Flow)

#### Tạo đơn hàng từ Kiosk (Tự phục vụ)
- **Endpoint:** `POST /api/orders/kiosk/checkout`
- **Auth:** `X-Api-Key`
- **Request Body:**
```json
{
  "memberId": 12,
  "pointsToUse": 50,
  "items": [
    { "bookId": 1, "quantity": 2 }
  ]
}
```
`memberId` nullable nếu khách không nhập số điện thoại; `pointsToUse` tối đa 100 điểm/đơn. Kiosk gọi `/api/books/barcode/{barcode}` để lấy `bookId` trước khi checkout.
- **Xử lý Backend:** 
  - Tự động tính benefit của mọi Promotion hợp lệ bằng thời gian UTC, chọn đúng một Promotion giảm nhiều nhất; đồng hạng chọn `promotionId` nhỏ nhất.
  - Validate `pointsToUse` không âm, không vượt quá **100 điểm/đơn** và không vượt quá số điểm hiện có của thành viên.
  - Quy đổi điểm: **1 điểm = 1.000 VNĐ**.
  - Tính toán lại giá, trừ `pointsToUse * 1000` VNĐ.
  - Tăng `ReservedQuantity` cho các sách. Lưu trạng thái `Pending`.
- **Response (Success - 200 OK):**
```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "Tạo đơn hàng thành công. Vui lòng quét mã QR để thanh toán.",
  "data": {
    "orderId": 105,
    "orderCode": "ORD-20261101-0105",
    "subTotal": 210000,
    "promotionId": 8,
    "promotionName": "Tuần lễ sách Việt",
    "promotionType": 2,
    "discountAmount": 50000,
    "pointsUsedAmount": 50000,
    "totalAmount": 110000,
    "sepayQrCodeUrl": "https://qr.sepay.vn/img?bank=demo&amount=110000&code=ORD-20261101-0105",
    "items": [
      {
        "bookId": 1,
        "bookTitle": "Cây Cam Ngọt Của Tôi",
        "quantity": 2,
        "unitPriceAtTime": 105000,
        "discountPerUnit": 25000,
        "lineTotal": 210000,
        "lineDiscountAmount": 50000,
        "netLineTotal": 160000
      }
    ]
  }
}
```

`discountPerUnit` là field API ánh xạ từ snapshot `OrderDetails.DiscountPerUnitAtTime`; không phải nguồn tính tiền thứ hai. Nếu kết quả sau Promotion và điểm `<= 0`, Backend trả HTTP 400 với wrapper `BAD_REQUEST`, `data=null`, rollback transaction và không tạo QR/Pending.

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
  - Chỉ áp `promotionId` nhân viên chọn; không tự thay bằng Promotion khác. Promotion hết hạn, sai type/detail hoặc không thỏa điều kiện phải trả lỗi có message.
  - Lưu Order trạng thái **`Pending`**.
- **Response:** Dùng cùng promotion metadata và item discount breakdown như Kiosk (`discountPerUnit`, `lineDiscountAmount`, `netLineTotal`) để nhân viên xác nhận với khách.

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
{
  "data": {
    "orderId": 105,
    "orderCode": "ORD-20261101-0105",
    "status": "Paid"
  }
}
```
`status` nhận một trong các giá trị `Pending`, `Paid`, `Cancelled`.

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
    "promotionId": 8,
    "promotionName": "Tuần lễ sách Việt",
    "promotionType": 2,
    "items": [
      {
        "bookId": 1,
        "bookTitle": "Cây Cam Ngọt Của Tôi",
        "quantity": 2,
        "unitPriceAtTime": 105000,
        "discountPerUnit": 25000,
        "lineTotal": 210000,
        "lineDiscountAmount": 50000,
        "netLineTotal": 160000
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
- Đơn mới đọc snapshot `BookTitleAtTime`, `PromotionNameAtTime`, `PromotionTypeAtTime`, `DiscountPerUnitAtTime`, `PointsUsedAmountAtTime` và các tổng persisted; không tính lại từ Promotion hiện tại.
- Đơn legacy: title fallback `Book.Title` hiện tại hoặc nhãn an toàn theo `BookId`; tiền điểm fallback `PointsUsed × 1000`; promotion name nullable/`"Khuyến mãi"`; promotion type nullable. Không suy đoán ProductDiscount và không sửa ngược đơn Paid.

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
  "status": 1,
  "errorCode": null,
  "errorMessage": null
}
```
- `status`: `1=Online`, `2=Offline`, `3=Error`.
- `errorCode` có thể là `CAM_DISCONNECTED`, `PRINTER_OUT_OF_PAPER` hoặc `null`.
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

### 3.9 Kiosk Idle Advertising API — Approved Contract V1

Trạng thái: **APPROVED DESIGN / CONTRACT V1 / NOT IMPLEMENTED**. Đây là source of truth đã được Leader phê duyệt cho [Issue #37](https://github.com/Unknown4505/Library-System/issues/37) và [Issue #38](https://github.com/Unknown4505/Library-System/issues/38); tài liệu không chứng minh endpoint, DTO hoặc UI đã tồn tại trong source.

| Decision ID | Quyết định Contract V1 đã duyệt |
|---|---|
| ADS-DEC-01 | New Arrivals và Best Sellers dùng cửa sổ rolling 30 ngày theo UTC. |
| ADS-DEC-02 | Mỗi nhóm tối đa 10 đầu sách, mỗi sách chỉ xuất hiện một lần trong cùng nhóm. |
| ADS-DEC-03 | New Arrivals sort lần nhập gần nhất giảm dần rồi `BookId ASC`; Best Sellers sort `totalSold DESC` rồi `BookId ASC`. |
| ADS-DEC-04 | Một phần nguồn lỗi trả HTTP 200, `success=true`, `code=PARTIAL_SUCCESS`, shape cố định và allow-list `unavailableSections`; rỗng hợp lệ không phải lỗi. |
| ADS-DEC-05 | FE cache tối đa 5 phút, không giữ promotion quá hạn, không fetch theo slide và dispose timer khi rời Idle. |
| ADS-DEC-06 | Tối đa một retry tự động mỗi lượt Idle, sau ít nhất 30 giây; sau đó fallback/cooldown an toàn. |
| ADS-DEC-07 | Toàn bộ ba nguồn không khả dụng trả HTTP 503, `SERVICE_UNAVAILABLE`, message tiếng Việt và `data=null`; lỗi ngoài dự kiến vẫn là 500 an toàn. |

#### 3.9.1 Đối chiếu source và convention hiện hữu

**EXISTING CONVENTION:**

- `KioskController` dùng route gốc `api/[controller]`; vì vậy action `[HttpGet("advertising")]` sẽ tạo `GET /api/kiosk/advertising`.
- `ApiKeyMiddleware` bảo vệ mọi path bắt đầu bằng `/api/kiosk` và `/api/orders/kiosk`; header HTTP không phân biệt hoa/thường, tên convention là `X-Api-Key`.
- Thiếu key hiện trả HTTP 401, code `UNAUTHORIZED`; key sai trả HTTP 403, code `FORBIDDEN`. Cả hai dùng `ApiResponseDto<object>` và JSON camelCase.
- `ApiResponseDto<T>` có shape cố định `success`, `code`, `message`, `data`; `Ok(...)` hiện chỉ sinh code `SUCCESS`, `Error(...)` đặt `data = null`.
- ASP.NET Core controller hiện dùng System.Text.Json web defaults; enum không có string-enum converter, nên `PromotionType` phải là JSON number: `1 = OrderDiscount`, `2 = ProductDiscount`.

**IMPLEMENTATION GAP:** chưa có advertising action/service/DTO/query. Middleware auth hiện wrap lỗi nhưng message 401/403 và lỗi thiếu server API key còn tiếng Anh; lỗi thiếu cấu hình còn lộ chi tiết cấu hình. Global exception handler có `INTERNAL_ERROR` cho 500 nhưng chưa có mapping 503. `PARTIAL_SUCCESS` và `SERVICE_UNAVAILABLE` chưa tồn tại trong factory/convention thực thi.

| Dữ liệu | Source hiện có | Phân loại | Giới hạn hiện tại |
|---|---|---|---|
| Ảnh và thông tin sách | `GET /api/books`; `BookDto.ImageUrl`, `SellingPrice`, `AvailableStock`; static files | **AVAILABLE để reuse** | Chưa tạo nhóm quảng cáo chuyên biệt |
| Khuyến mãi | `Promotion`, `PromotionOrderDiscount`, `PromotionProductDiscount`; CMS `GET /api/promotions` | **PARTIAL** | CMS endpoint dùng JWT và DTO thiếu loại/chi tiết giảm; không nới quyền endpoint CMS |
| Sách mới về | `ImportReceipt.CreatedAt` + `ImportReceiptDetail.BookId`; tạo phiếu và tăng kho cùng transaction | **APPROVED DESIGN / API GAP** | Chưa có query Kiosk; Contract V1 quy định 30 ngày, distinct, active/còn hàng, limit 10 và tie-break |
| Sách bán chạy | `Orders.CompletedAt`, `OrderStatus`, `OrderDetails.Quantity`; Admin reports query Paid-only | **APPROVED DESIGN / API GAP** | Endpoint reports Admin-only không được reuse trực tiếp; query Kiosk 30 ngày/active/còn hàng/limit 10 chưa implement |

#### 3.9.2 Endpoint

| Thuộc tính | Contract V1 đã duyệt |
|---|---|
| Method/route | `GET /api/kiosk/advertising` |
| Request body | Không có |
| Authentication | `X-Api-Key: <kiosk_secret_key>`; không dùng JWT CMS |
| Success response | `ApiResponseDto<KioskAdvertisingDto>`; JSON camelCase |
| Side effect | Không; read-only và gọi lặp không ghi dữ liệu |
| Dữ liệu cấm | `CostPrice`, API key, thông tin thành viên/nhân viên, email, số điện thoại và chi tiết nội bộ |

Request mẫu:

```http
GET /api/kiosk/advertising HTTP/1.1
Host: localhost:7111
Accept: application/json
X-Api-Key: <kiosk_secret_key>
```

#### 3.9.3 DTO C# và JSON mapping

```csharp
public sealed class KioskAdvertisingDto
{
    public List<AdvertisingPromotionDto> ActivePromotions { get; init; } = new();
    public List<AdvertisingNewArrivalDto> NewArrivals { get; init; } = new();
    public List<AdvertisingBestSellerDto> BestSellers { get; init; } = new();
    public List<string> UnavailableSections { get; init; } = new();
}

public sealed class AdvertisingPromotionDto
{
    public int PromotionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public PromotionType PromotionType { get; init; }
    public AdvertisingOrderDiscountDto? OrderDiscount { get; init; }
    public List<AdvertisingProductDiscountDto> ProductDiscounts { get; init; } = new();
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

public sealed class AdvertisingOrderDiscountDto
{
    public decimal MinOrderValue { get; init; }
    public decimal DiscountAmount { get; init; }
}

public class AdvertisingBookDto
{
    public int BookId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public decimal SellingPrice { get; init; }
    public int AvailableStock { get; init; }
}

public sealed class AdvertisingProductDiscountDto : AdvertisingBookDto
{
    public decimal DiscountAmount { get; init; }
}

public sealed class AdvertisingNewArrivalDto : AdvertisingBookDto
{
    public DateTime LastImportedAt { get; init; }
}

public sealed class AdvertisingBestSellerDto : AdvertisingBookDto
{
    public int TotalSold { get; init; }
}
```

| JSON field | C# type/nullability | Nguồn | Quy tắc |
|---|---|---|---|
| `activePromotions` | array, không null | `Promotions` | Chỉ active và trong thời gian hiệu lực |
| `promotionType` | number (`PromotionType`) | `Promotion.PromotionType` | `1=OrderDiscount`, `2=ProductDiscount` |
| `orderDiscount` | object hoặc `null` | `PromotionOrderDiscount` | Có object khi type 1; type 2 trả `null` |
| `productDiscounts` | array, không null | `PromotionProductDiscounts` + `Books` | Type 1 trả `[]`; type 2 có từng sách/mức giảm |
| `description`, `imageUrl` | `string?` | entity tương ứng | JSON field luôn tồn tại và có thể là `null` |
| các field tiền | `decimal` | entity tương ứng | JSON number; FE format VNĐ, không trả chuỗi đã format |
| `startDate`, `endDate`, `lastImportedAt` | `DateTime`, không null | timestamp tương ứng | ISO 8601 UTC có hậu tố `Z` |
| `newArrivals`, `bestSellers` | array, không null | nhập kho; order detail | Không dữ liệu trả `[]` |
| `unavailableSections` | array, không null | trạng thái tổng hợp service | Chỉ chứa `activePromotions`, `newArrivals`, `bestSellers`; rỗng nếu không có section lỗi |

Response shape luôn cố định: không bỏ field. `null` chỉ dùng cho field được khai báo nullable; collection luôn là `[]` khi rỗng; lỗi toàn endpoint mới trả `data: null`.

#### 3.9.4 Business rules

**ĐÃ ĐỐI CHIẾU SOURCE / GIỮ NGUYÊN:**

- Promotion chỉ hợp lệ khi `IsActive=true` và UTC hiện tại nằm trong khoảng bao gồm hai biên `StartDate <= now <= EndDate`.
- `OrderDiscount`: `orderDiscount` chứa `minOrderValue` và `discountAmount`; `productDiscounts=[]`.
- `ProductDiscount`: `orderDiscount=null`; `productDiscounts` chứa từng `bookId` và `discountAmount`, không gộp thành một mức giảm chung.
- Best sellers chỉ tổng `OrderDetails.Quantity` của order `Paid`; loại `Pending`, `Cancelled` và cart local.
- Chỉ trả sách active, `AvailableStock > 0`; không trả `CostPrice`.

**APPROVED DESIGN / CONTRACT V1:**

- New arrivals lấy lần nhập gần nhất từ `MAX(ImportReceipt.CreatedAt)` theo `ImportReceiptDetails.BookId`, không dùng `Books.CreatedAt`; cửa sổ rolling 30 ngày UTC; mỗi sách một dòng; sort `LastImportedAt DESC, BookId ASC`; tối đa 10.
- Source đủ để query vì `ImportReceipt` có UTC `CreatedAt`, detail có `BookId`, navigation tới `Book`, và service hiện tạo phiếu/tăng kho trong cùng transaction. Không có blocker schema; query/API vẫn chưa implement.
- Best sellers dùng order `Paid` có `CompletedAt >= utcNow - 30 ngày`, tổng `OrderDetails.Quantity` theo sách, tối đa 10; sort `TotalSold DESC, BookId ASC`.
- Không có dữ liệu phù hợp trả HTTP 200 với ba array rỗng, `unavailableSections=[]`; không dùng 404.
- Partial success: HTTP 200, nhóm lỗi trả `[]`, nhóm tốt vẫn có dữ liệu, `unavailableSections` ghi nhóm lỗi, `success=true`, code `PARTIAL_SUCCESS`. Mô hình đã duyệt nhưng factory/convention hiện tại chưa sinh code success khác `SUCCESS`: **IMPLEMENTATION GAP**.
- `ProductDiscount` vẫn có DTO shape theo từng sách, nhưng source Checkout Kiosk hiện chưa chứng minh tự áp dụng loại giảm này. Advertising V1 phải lọc/không quảng cáo `ProductDiscount` như ưu đãi áp dụng tại Kiosk cho đến khi checkout support được xác minh và duyệt ở scope riêng; task #37 không sửa checkout.

#### 3.9.5 Response mẫu — contract sample, chưa runtime-test

HTTP 200 có đủ ba nhóm. Mẫu nội dung Kiosk chỉ quảng cáo `OrderDiscount` mà checkout hiện đã có bằng chứng áp dụng:

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "Lấy dữ liệu quảng cáo thành công.",
  "data": {
    "activePromotions": [
      {
        "promotionId": 11,
        "name": "Giảm 20000 cho đơn từ 200000",
        "description": "Áp dụng cho toàn đơn đủ điều kiện.",
        "promotionType": 1,
        "orderDiscount": {
          "minOrderValue": 200000,
          "discountAmount": 20000
        },
        "productDiscounts": [],
        "startDate": "2026-10-01T00:00:00Z",
        "endDate": "2026-10-31T23:59:59Z"
      }
    ],
    "newArrivals": [
      {
        "bookId": 8,
        "title": "Nhà Giả Kim",
        "imageUrl": null,
        "sellingPrice": 79000,
        "availableStock": 7,
        "lastImportedAt": "2026-10-20T08:30:00Z"
      }
    ],
    "bestSellers": [
      {
        "bookId": 5,
        "title": "Clean Code",
        "imageUrl": "/uploads/books/clean-code.jpg",
        "sellingPrice": 300000,
        "availableStock": 4,
        "totalSold": 18
      }
    ],
    "unavailableSections": []
  }
}
```

Shape DTO dành cho `ProductDiscount` được giữ trong contract để không làm mất semantics theo từng sách, nhưng payload này **không được đưa vào nội dung Kiosk hiện tại** cho đến khi luồng Checkout hỗ trợ đã được xác minh:

```json
{
  "promotionId": 12,
  "name": "Ưu đãi sách chọn lọc",
  "description": null,
  "promotionType": 2,
  "orderDiscount": null,
  "productDiscounts": [
    {
      "bookId": 5,
      "title": "Clean Code",
      "imageUrl": "/uploads/books/clean-code.jpg",
      "sellingPrice": 300000,
      "availableStock": 4,
      "discountAmount": 30000
    },
    {
      "bookId": 8,
      "title": "Nhà Giả Kim",
      "imageUrl": null,
      "sellingPrice": 79000,
      "availableStock": 7,
      "discountAmount": 10000
    }
  ],
  "startDate": "2026-10-10T00:00:00Z",
  "endDate": "2026-11-10T23:59:59Z"
}
```

HTTP 200 không có nội dung phù hợp:

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "Hiện chưa có nội dung quảng cáo phù hợp.",
  "data": {
    "activePromotions": [],
    "newArrivals": [],
    "bestSellers": [],
    "unavailableSections": []
  }
}
```

HTTP 200 partial success — **APPROVED CONTRACT / NOT IMPLEMENTED**:

```json
{
  "success": true,
  "code": "PARTIAL_SUCCESS",
  "message": "Một số nội dung quảng cáo tạm thời không khả dụng.",
  "data": {
    "activePromotions": [],
    "newArrivals": [
      {
        "bookId": 8,
        "title": "Nhà Giả Kim",
        "imageUrl": null,
        "sellingPrice": 79000,
        "availableStock": 7,
        "lastImportedAt": "2026-10-20T08:30:00Z"
      }
    ],
    "bestSellers": [],
    "unavailableSections": [
      "activePromotions",
      "bestSellers"
    ]
  }
}
```

#### 3.9.6 HTTP status, code và message

Mọi response Advertising phải có `message` tiếng Việt, không rỗng, không chứa stack trace, SQL detail, API key hoặc cấu hình nội bộ.

| HTTP | `success` | `code` | `message` chuẩn | Trạng thái convention |
|---:|:---:|---|---|---|
| 200 | true | `SUCCESS` | `Lấy dữ liệu quảng cáo thành công.` | Code hiện có |
| 200 empty | true | `SUCCESS` | `Hiện chưa có nội dung quảng cáo phù hợp.` | Code hiện có |
| 200 partial | true | `PARTIAL_SUCCESS` | `Một số nội dung quảng cáo tạm thời không khả dụng.` | **APPROVED CONTRACT / IMPLEMENTATION GAP** |
| 400 | false | `BAD_REQUEST` | `Thông tin yêu cầu không hợp lệ.` | Code hiện có; V1 không có body/query nên bình thường không phát sinh |
| 401 | false | `UNAUTHORIZED` | `Thiếu thông tin xác thực Kiosk.` | Status/code hiện có; message target khác source |
| 403 | false | `FORBIDDEN` | `Kiosk không có quyền truy cập chức năng này.` | Status/code hiện có; message target khác source |
| 500 | false | `INTERNAL_ERROR` | `Hệ thống gặp sự cố. Vui lòng thử lại sau.` | Code hiện có; message target cần đồng bộ |
| 503 | false | `SERVICE_UNAVAILABLE` | `Nội dung quảng cáo tạm thời không khả dụng.` | **APPROVED CONTRACT / IMPLEMENTATION GAP** |

Mẫu lỗi xác thực target (401; 403 có cùng shape với code/message tương ứng):

```json
{
  "success": false,
  "code": "UNAUTHORIZED",
  "message": "Thiếu thông tin xác thực Kiosk.",
  "data": null
}
```

Mẫu lỗi 500:

```json
{
  "success": false,
  "code": "INTERNAL_ERROR",
  "message": "Hệ thống gặp sự cố. Vui lòng thử lại sau.",
  "data": null
}
```

Mẫu lỗi 503 — **APPROVED CONTRACT / NOT IMPLEMENTED**:

```json
{
  "success": false,
  "code": "SERVICE_UNAVAILABLE",
  "message": "Nội dung quảng cáo tạm thời không khả dụng.",
  "data": null
}
```

#### 3.9.7 Cache, retry và hiệu năng — APPROVED DESIGN

- Backend giới hạn tối đa 10 item mỗi nhóm, query projection trực tiếp và tránh N+1; không sửa/nới quyền CMS reports/promotions.
- FE cache payload tối đa 5 phút nhưng thời điểm hết hạn thực tế là mốc sớm hơn giữa 5 phút và `endDate` gần nhất của promotion; không hiển thị promotion quá hạn từ cache.
- Chỉ tải khi vào `IdlePage` và cache không còn hợp lệ; không gọi API theo mỗi slide 8 giây.
- Khi lỗi, FE dùng fallback và tối đa một background retry trong một lượt vào Idle, không sớm hơn 30 giây. Nếu vẫn lỗi thì giữ fallback/cooldown; không retry theo slideshow timer hoặc tạo request storm khi vào lại Idle.
- Dừng/dispose slideshow timer khi rời `IdlePage`; không giữ nhiều timer/request song song.
- FE phải xét cả HTTP status và `success`/`code`/`message`/`data`; HTTP 200 không đồng nghĩa thành công nếu wrapper báo lỗi. Background refresh/slideshow/inactivity không hiện toast lặp; chỉ log an toàn và hiển thị fallback thân thiện.

Owner: Backend [#37](https://github.com/Unknown4505/Library-System/issues/37) `@thien33`; Frontend [#38](https://github.com/Unknown4505/Library-System/issues/38) `@lehuukhang`. Contract đã được Leader duyệt; implementation và runtime QA vẫn **PLANNED / NOT RUN**. Không tạo bảng `Advertisements`, migration, endpoint hay DTO thật trong lượt docs này.

---

### 3.10 Promotion CRUD & ProductDiscount Shared Contract — APPROVED V7.1

Các route hiện hữu `GET/POST /api/promotions` và `PUT /api/promotions/{id}` tiếp tục dùng `ApiResponseDto<T>`; không tạo endpoint update detail riêng nếu PUT hiện tại mở rộng được.

#### Create/Update request

```json
{
  "name": "Tuần lễ sách Việt",
  "description": "Giảm theo từng đầu sách trong chương trình.",
  "promotionType": 2,
  "startDate": "2026-11-01T00:00:00Z",
  "endDate": "2026-11-07T23:59:59Z",
  "isActive": true,
  "orderDiscount": null,
  "productDiscounts": [
    { "bookId": 12, "discountAmount": 15000 },
    { "bookId": 18, "discountAmount": 20000 }
  ]
}
```

- `promotionType=1`: `orderDiscount` bắt buộc; `productDiscounts=[]`.
- `promotionType=2`: `orderDiscount=null`; `productDiscounts` bắt buộc không rỗng, `bookId` không trùng, mỗi `discountAmount > 0`.
- Money là JSON number và phải là số tiền nguyên VNĐ ở validation layer; timestamp là ISO 8601 UTC.
- `PUT` dùng **replace-all**: validate toàn payload rồi đồng bộ toàn bộ detail trong một transaction; lỗi phải rollback và không để detail cũ sót lại.

Response trả lại cùng shape và thêm `promotionId`. `PromotionType` là JSON number `1/2`, không serialize thành chuỗi.

#### Selection, non-stacking và zero payable

- Một Order tối đa một Promotion. Kiosk chọn benefit cao nhất; đồng hạng chọn `PromotionId ASC`. POS chỉ dùng Promotion được chọn thủ công.
- OrderDiscount và ProductDiscount không cộng chung; không cộng hai Promotion cùng loại. Cả hai được kết hợp với điểm thành viên.
- ProductDiscount benefit: `SUM(MIN(configuredDiscount, unitPriceAtTime) × quantity)` cho các sách thuộc Promotion.
- Nếu `TotalAmount <= 0`, không clamp, không tạo Pending/QR và không giữ kho/trừ điểm vĩnh viễn.

```json
{
  "success": false,
  "code": "BAD_REQUEST",
  "message": "Tổng tiền thanh toán sau khuyến mãi và điểm thành viên phải lớn hơn 0 đồng.",
  "data": null
}
```

Shared DTO/Domain/mapping là foundation; Promotion CRUD, calculation, persistence và Receipt runtime vẫn thuộc các task Backend/Frontend tiếp theo, chưa được đánh dấu PASS.

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
