# KẾ HOẠCH TRIỂN KHAI VÀ BÁO CÁO NGHIỆM THU BACKEND CONTRACTS TÍCH HỢP CMS (PR #33)
## BỔ SUNG CÁC CONTRACTS CÒN THIẾU CHO PHÂN HỆ QUẢN TRỊ & BÁN HÀNG TẠI QUẦY (POS COUNTER)

> **Backend Owner:** `@thien33`  
> **CMS Frontend Owner:** `@lehuukhang` (PR #33, commit `58d49cc` / `0de0dbe`)  
> **Leader phụ trách:** `@Unknown4505`  
> **Căn cứ tài liệu:** [docs/04-api-reference.md](file:///d:/Downloads/CNPM/Library-System/docs/04-api-reference.md), [docs/07-security.md](file:///d:/Downloads/CNPM/Library-System/docs/07-security.md), [docs/09-task-assignments.md](file:///d:/Downloads/CNPM/Library-System/docs/09-task-assignments.md), [docs/12-workflow-handoff-and-scope.md](file:///d:/Downloads/CNPM/Library-System/docs/12-workflow-handoff-and-scope.md)  
> **Trạng thái thực thi:** ✅ **ĐÃ TRIỂN KHAI HOÀN TẤT — BIÊN DỊCH 0 LỖI — PASS 100% KIỂM ĐỊNH TIER 1**

---

## 1. PHÂN TÍCH HIỆN TRẠNG & ĐỐI CHIẾU PHẠM VI (GAP ANALYSIS)

### 1.1 Những gì Backend ĐÃ CÓ (Tái sử dụng 100%, không làm lại)
- **Catalog Core:** Sách (`BooksController`), Danh mục (`CategoriesController`), Khu vực kệ (`AreasController`) với đầy đủ GET, POST, PUT, DELETE (Task 9.1 thuộc Leader nghiệm thu).
- **Members:** Tra cứu theo SĐT, Tạo mới, Cập nhật, Lịch sử điểm thưởng (`MembersController`).
- **Kiosk Pipeline:** Checkout giữ kho, Polling trạng thái, Hủy đơn timeout/khách bấm hủy, Webhook SePay (HMAC/Idempotency), Dev simulate F3, Hóa đơn Kiosk Task 5.6 (`GET /api/orders/kiosk/{orderId}/receipt`).
- **Promotions:** Đã có CRUD cơ bản (`PromotionsController`).
- **Inventory:** Đã có `POST /api/inventory/import` cơ bản (`InventoryController`).
- **Reports:** Đã có `GET /api/reports/revenue` và `GET /api/reports/top-books` (`ReportsController`).
- **Authentication Infrastructure:** Đã cấu hình xác thực JWT Bearer trong [Program.cs](file:///d:/Downloads/CNPM/Library-System/BookKiosk.API/Program.cs#L91-L125) (hỗ trợ Issuer, Audience, Signing Key).

### 1.2 Những gì CÒN THIẾU để tích hợp CMS PR #33
Dựa trên việc kiểm tra chi tiết mã nguồn CMS Client ([CmsApiClient.cs](file:///d:/Downloads/CNPM/Library-System/BookKiosk.CMS/Services/CmsApiClient.cs), [CmsPosApi.cs](file:///d:/Downloads/CNPM/Library-System/BookKiosk.CMS/Services/CmsPosApi.cs), [CmsOrderApi.cs](file:///d:/Downloads/CNPM/Library-System/BookKiosk.CMS/Services/CmsOrderApi.cs)) từ PR #33:

| Mức độ | Phân hệ | Endpoint yêu cầu | Trạng thái Backend ban đầu | Giải pháp triển khai |
|---|---|---|---|---|
| **P0** | **Auth / RBAC** | `POST /api/auth/login` | ❌ Chưa có `AuthController` | Xây dựng `AuthController`, xác thực User active bằng BCrypt, cấp JWT token chứa Claims (`UserId`, `Username`, `FullName`, `Role`), trả về `accessToken`, `expiresAt`, `fullName`, `role`. |
| **P0** | **POS Counter** | `POST /api/orders/counter/checkout`<br/>`POST /api/orders/counter/{id}/confirm-payment`<br/>`POST /api/orders/counter/{id}/cancel`<br/>`GET /api/orders/{id}` | ❌ Chưa có các endpoint Counter trong `OrdersController` | - Checkout tạo đơn `Pending`, kênh `Counter`, giữ kho `ReservedQuantity`, BE tự tính tiền.<br/>- Confirm thanh toán Cash: chuyển `Paid`, trừ kho vật lý & reserved atomically, cộng điểm, sinh `PaymentTransaction`.<br/>- Cancel: chỉ hủy đơn `Counter` đang `Pending`, nhả kho.<br/>- Polling: `GET /api/orders/{id}`. |
| **P1** | **Orders CMS** | `GET /api/orders` (Paging + Filter)<br/>`GET /api/orders/{id}` (Detail) | ❌ Chưa có query đa tiêu chí cho CMS | - Triển khai `GET /api/orders` hỗ trợ lọc: `orderCode`, `status`, `channel`, `paymentMethod`, khoảng ngày, và cờ `needsSupport`.<br/>- Tính toán cờ `NeedsSupport` (đơn Pending/Cancelled nhưng có tiền chuyển vào).<br/>- Chặn CMS staff hủy đơn Kiosk (chỉ được hủy đơn Counter Pending). |
| **P1** | **Suppliers** | `GET /api/suppliers`<br/>`POST /api/suppliers`<br/>`PUT /api/suppliers/{id}`<br/>`DELETE /api/suppliers/{id}` | ❌ Chưa có `SuppliersController` | Triển khai CRUD nhà cung cấp theo DTO chuẩn của CMS. Khi xóa NCC đã phát sinh phiếu nhập kho (`ImportReceipts`), áp dụng cơ chế soft-delete / disable (`IsActive = false`) để bảo vệ toàn vẹn lịch sử. |
| **P1** | **Inventory** | `GET /api/inventory/stock`<br/>`POST /api/inventory/adjust`<br/>`GET /api/inventory/receipts`<br/>`GET /api/inventory/receipts/{id}`<br/>`POST /api/inventory/receipts` | ⚠️ Mới có duy nhất `POST /api/inventory/import` | - Bổ sung danh sách tồn kho (`GET /stock`) kèm cờ `lowOnly` & `keyword`.<br/>- Bổ sung kiểm kê điều chỉnh kho (`POST /adjust`) trong transaction.<br/>- Bổ sung danh sách & chi tiết phiếu nhập kho (`GET /receipts`, `GET /receipts/{id}`).<br/>- Bổ sung tạo phiếu nhập (`POST /receipts`) tăng tồn kho và cập nhật giá vốn; giữ alias `/import`. |
| **P1** | **Reports RBAC** | `GET /api/reports/revenue`<br/>`GET /api/reports/top-books` | ⚠️ Đang để `[Authorize]` chung | Đổi thành `[Authorize(Roles = "Admin")]`. Staff và Manager truy cập sẽ bị từ chối với HTTP 403 Forbidden. |
| **P2** | **Promotions RBAC & Toggle** | `PATCH /api/promotions/{id}/active`<br/>Phân quyền CRUD KM | ⚠️ Đang để `[Authorize]` chung | - Đặt quyền thay đổi (POST, PUT, DELETE, PATCH) là `Admin-only`. Staff chỉ được quyền đọc (`GET`).<br/>- Bổ sung route `PATCH /api/promotions/{id}/active` để CMS toggle trạng thái trực tiếp mà không cần fallback. |

### 1.3 Phạm vi KHÔNG thuộc Issue này (Out-of-Scope)
- **Không động vào Kiosk Receipt (#28):** Đã hoàn thành và nghiệm thu.
- **Không sửa giao diện FE:** Toàn bộ Views, Razor, JS, CSS, client-side validation của CMS thuộc trách nhiệm của Khang.
- **Không tự mở rộng HR / Salary / Employee:** Hồ sơ nhân viên và sổ lương tháng vẫn ở trạng thái `PLANNED` (chờ Leader duyệt chính sách và migration riêng).
- **Không mở endpoint quản lý tài khoản `/api/users`:** Sẽ làm ở task riêng khi Leader chốt CRUD policy.
- **Không thay đổi Task 9.1 (Books/Category/Area):** Do Leader chịu trách nhiệm nghiệm thu chung.

---

## 2. MA TRẬN PHÂN QUYỀN HỆ THỐNG (ROLE MATRIX)

Hệ thống sử dụng enum `UserRole` gồm: `Admin = 1`, `Staff = 2`, `Manager = 3`. Phân quyền tại Backend được kiểm soát bằng thuộc tính `[Authorize(Roles = "...")]`:

| Nhóm chức năng | Route / Endpoint | HTTP Method | Admin | Manager | Staff | Ghi chú bảo mật |
|---|---|:---:|:---:|:---:|:---:|---|
| **Auth** | `/api/auth/login` | POST | ✅ | ✅ | ✅ | Endpoint mở, xác thực Username/Password, cấp JWT token |
| **POS Counter** | `/api/orders/counter/checkout` | POST | ✅ | ❌ | ✅ | Chỉ Admin và Thu ngân quầy (Staff) |
| | `/api/orders/counter/{id}/confirm-payment` | POST | ✅ | ❌ | ✅ | Chốt thanh toán tiền mặt tại quầy |
| | `/api/orders/counter/{id}/cancel` | POST | ✅ | ❌ | ✅ | Hủy đơn nháp quầy (chỉ đơn Counter + Pending) |
| | `/api/orders/{id}` | GET | ✅ | ✅ | ✅ | Tra cứu trạng thái đơn hàng phục vụ polling |
| **Orders CMS** | `/api/orders` | GET | ✅ | ✅ | ✅ | Xem danh sách đơn hàng toàn hệ thống (Kiosk + Quầy) |
| | `/api/orders/{id}` | GET | ✅ | ✅ | ✅ | Xem chi tiết, dòng hàng và lịch sử thanh toán |
| **Suppliers** | `/api/suppliers` | GET | ✅ | ✅ | ✅ | Tra cứu danh sách nhà cung cấp |
| | `/api/suppliers` | POST | ✅ | ✅ | ❌ | Tạo mới nhà cung cấp (Admin, Manager) |
| | `/api/suppliers/{id}` | PUT | ✅ | ✅ | ❌ | Cập nhật thông tin nhà cung cấp |
| | `/api/suppliers/{id}` | DELETE | ✅ | ✅ | ❌ | Vô hiệu hóa NCC (Soft-delete nếu đã có phiếu nhập) |
| **Inventory** | `/api/inventory/stock` | GET | ✅ | ✅ | ✅ | Xem số lượng tồn kho thực tế và khả dụng |
| | `/api/inventory/adjust` | POST | ✅ | ✅ | ❌ | Điều chỉnh kiểm kê kho vật lý |
| | `/api/inventory/receipts` | GET | ✅ | ✅ | ❌ | Xem danh sách phiếu nhập kho |
| | `/api/inventory/receipts/{id}` | GET | ✅ | ✅ | ❌ | Xem chi tiết phiếu nhập kho |
| | `/api/inventory/receipts` | POST | ✅ | ✅ | ❌ | Tạo phiếu nhập kho và tăng tồn kho sách |
| **Reports** | `/api/reports/revenue` | GET | ✅ | ❌ | ❌ | Doanh thu theo tháng: **Admin-only** (403 với Staff/Manager) |
| | `/api/reports/top-books` | GET | ✅ | ❌ | ❌ | Top sách bán chạy: **Admin-only** (403 với Staff/Manager) |
| **Promotions** | `/api/promotions` | GET | ✅ | ✅ | ✅ | Xem danh sách chương trình khuyến mãi |
| | `/api/promotions` | POST | ✅ | ❌ | ❌ | Tạo khuyến mãi: **Admin-only** (403 với Staff) |
| | `/api/promotions/{id}` | PUT | ✅ | ❌ | ❌ | Cập nhật khuyến mãi: **Admin-only** |
| | `/api/promotions/{id}/active` | PATCH | ✅ | ❌ | ❌ | Bật/Tắt khuyến mãi: **Admin-only** |
| | `/api/promotions/{id}` | DELETE | ✅ | ❌ | ❌ | Xóa khuyến mãi: **Admin-only** |

---

## 3. THIẾT KẾ KỸ THUẬT CHI TIẾT TỪNG PHÂN HỆ

### 3.1 Phân hệ P0 — Auth / RBAC cho CMS (`POST /api/auth/login`)
- **Tầng Application:**
  - DTOs: `LoginRequestDto { Username, Password }`, `LoginResponseDto { AccessToken, ExpiresAt, FullName, Role }`.
  - Service: `IAuthService` và `AuthService` với method `Task<LoginResponseDto> LoginAsync(LoginRequestDto request)`.
- **Logic xử lý:**
  1. Tìm kiếm User theo `Username` (truy vấn không phân biệt hoa thường).
  2. Kiểm tra `user == null` hoặc `user.IsActive == false`: trả về lỗi HTTP 401 Unauthorized (`"Tài khoản hoặc mật khẩu không chính xác hoặc tài khoản đã bị khóa"`).
  3. Xác thực mật khẩu: Dùng `BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)` đối chiếu hash BCrypt. Tuyệt đối không log mật khẩu hoặc hash ra console/file.
  4. Tạo JWT Token với các Claims chuẩn:
     - `ClaimTypes.NameIdentifier`: `user.UserId.ToString()`
     - `ClaimTypes.Name`: `user.Username`
     - `ClaimTypes.GivenName`: `user.FullName`
     - `ClaimTypes.Role`: `user.Role.ToString()` (`Admin`, `Staff`, `Manager`)
  5. Ký token bằng SymmetricSecurityKey từ cấu hình `Jwt:Key`, thời hạn token mặc định 8 giờ (hoặc theo cấu hình).
  6. Tuyệt đối **không trả về `PasswordHash`** trong phản hồi.

### 3.2 Phân hệ P0 — POS Counter (Quầy bán hàng)
- **1. Tạo đơn nháp tại quầy (`POST /api/orders/counter/checkout`):**
  - Nhận `CounterCheckoutRequestDto { MemberId, PromotionId, PointsToUse, PaymentMethod, Items }`.
  - Validate: Danh sách sách không rỗng, kiểm tra BookId trùng lặp (A04), validate tồn kho khả dụng (`AvailableStock >= quantity`).
  - Phân quyền: Lấy `UserId` của thu ngân từ Claims của JWT token.
  - Xử lý khuyến mãi: Nếu có `PromotionId` do thu ngân chọn, kiểm tra tính hợp lệ và áp dụng chiết khấu.
  - Xử lý điểm: Nếu có `MemberId` và `PointsToUse > 0`, kiểm tra số dư điểm, trừ điểm nguyên tử bằng `TryDeductPointsAsync` (1 điểm = 1.000 VNĐ, tối đa 100 điểm).
  - Transaction SQL:
    - Tăng `ReservedQuantity` của từng sách tương ứng (`TryIncreaseReservedQuantityAsync`).
    - Tạo `Order` với `SaleChannel = SaleChannel.Counter`, `OrderStatus = OrderStatus.Pending`, `PaymentMethod = Cash/QR`, `UserId = cashierId`.
    - Tạo các dòng `OrderDetails` lưu snapshot `UnitPriceAtTime` và `LineTotal`.
    - Sinh mã `OrderCode` chuẩn `ORD-yyyyMMdd-XXXX` theo `OrderId`.
    - Nếu là QR: sinh kèm `sepayQrCodeUrl` và `expiresAt` (mặc định 10 phút cho quầy).
  - Trả về `CounterCheckoutResponseDto` để CMS hiển thị và in bill nháp.

- **2. Xác nhận thanh toán tiền mặt (`POST /api/orders/counter/{orderId}/confirm-payment`):**
  - Kiểm tra `orderId` tồn tại.
  - Kiểm tra điều kiện: `order.SaleChannel == SaleChannel.Counter` và `order.OrderStatus == OrderStatus.Pending`. Nếu đơn đã `Paid` $\rightarrow$ Trả về hóa đơn hiện tại (Idempotency); nếu đơn `Cancelled` $\rightarrow$ Trả về 409 Conflict.
  - Transaction SQL nguyên tử:
    - Chuyển `OrderStatus = OrderStatus.Paid` và `CompletedAt = DateTime.UtcNow`.
    - Chốt kho: `StockQuantity -= quantity` và `ReservedQuantity -= quantity` (giải phóng giữ chỗ, trừ tồn vật lý).
    - Tích điểm cho Member: Cứ mỗi 10.000đ của `TotalAmount` cộng 1 điểm thưởng mới, ghi nhận `PointTransaction` (Type: `Earned`).
    - Ghi nhận `PaymentTransaction`: `Gateway = "Cash"`, `Amount = order.TotalAmount`, `ReferenceCode = $"CASH-{order.OrderCode}"`.
  - Trả về `PosReceiptDto` đầy đủ thông tin để máy in nhiệt in hóa đơn K80 ngay tại quầy.

- **3. Hủy đơn hàng quầy (`POST /api/orders/counter/{orderId}/cancel`):**
  - Chỉ cho phép hủy đơn `Counter` đang ở trạng thái `Pending`.
  - **Tuyệt đối từ chối nếu đơn là `Kiosk`** $\rightarrow$ Trả 409 Conflict (`"Chỉ cho phép hủy đơn hàng tại quầy thu ngân"`).
  - Nếu đơn đã `Paid` $\rightarrow$ Trả 409 Conflict (`"Không thể hủy đơn hàng đã hoàn tất thanh toán"`).
  - Transaction SQL: Chuyển `OrderStatus = OrderStatus.Cancelled`, giải phóng tồn kho `ReservedQuantity -= quantity`, hoàn trả điểm cho member nếu đơn có dùng điểm tích lũy.

- **4. Hợp nhất Contract Tra cứu chi tiết & Polling trạng thái (`GET /api/orders/{orderId}`):**
  - **Tránh lỗi AmbiguousMatchException trong ASP.NET Core:** Trong PR #33, `CmsPosApi.cs` (quầy POS) gọi `GET /api/orders/{orderId}` để polling trạng thái thanh toán (`OrderPaymentStatusDto`), còn `CmsOrderApi.cs` (quản trị đơn) cũng gọi `GET /api/orders/{orderId}` để lấy chi tiết (`OrderDetailDto`). Nếu định nghĩa 2 endpoint trùng route sẽ gây sập ứng dụng!
  - **Giải pháp kỹ thuật duy nhất:** Thiết kế **duy nhất một endpoint `GET /api/orders/{orderId}`** trả về `ApiResponseDto<OrderDetailDto>`. 
  - Trong DTO này có chứa đầy đủ cả nhóm trường polling (`OrderId`, `OrderCode`, `Status` / `OrderStatus`, `CompletedAt`, `ServerTime`) lẫn nhóm trường chi tiết (`Items`, `PaymentTransactions`, `MemberName`, `CashierName`, `SubTotal`, `DiscountAmount`, `TotalAmount`).
  - Nhờ cơ chế `PropertyNameCaseInsensitive = true` của `System.Text.Json`, `CmsPosApi` tự động deserialize các trường trạng thái mà không quan tâm các trường dư thừa, còn `CmsOrderApi` nạp trọn vẹn chi tiết đơn hàng. Cả 2 Client của PR #33 đều chạy mượt mà trên cùng 1 endpoint.

### 3.3 Phân hệ P1 — Orders CMS (Quản lý & Đối soát đơn hàng)
- **1. Danh sách đơn hàng phân trang (`GET /api/orders`):**
  - Query parameters: `page`, `pageSize`, `keyword` (tìm theo OrderCode), `status`, `channel`, `paymentMethod`, `fromDate`, `toDate`, `needsSupport` (bool).
  - Query tối ưu bằng EF Core với `.AsNoTracking()`:
    - Lọc `SaleChannel`: Kiosk (1), Counter (2).
    - Lọc `OrderStatus`: Pending (1), Paid (2), Cancelled (3).
    - Lọc `PaymentMethod`: Cash (1), QR (2).
    - Lọc khoảng thời gian tạo đơn `CreatedAt`.
    - Lọc `needsSupport = true`: Những đơn có `OrderStatus != Paid` nhưng bảng `PaymentTransactions` có ghi nhận giao dịch `Amount > 0` (lệch tiền hoặc chuyển tiền trễ sau timeout).
  - Trả về `PaginatedResultDto<OrderListItemDto>` kèm tổng số dòng và cờ `NeedsSupport` trên từng đơn để CMS hiển thị badge cảnh báo.

- **2. Xem chi tiết đơn hàng (`GET /api/orders/{orderId}`):**
  - Sử dụng chung endpoint thống nhất với Mục 3.2.4 (xem chi tiết ở trên).
  - Quyền truy cập: `[Authorize(Roles = "Admin,Manager,Staff")]`.

### 3.4 Phân hệ P1 — Suppliers (Quản lý Nhà cung cấp)
- **Controller:** `SuppliersController` với phân quyền `[Authorize(Roles = "Admin,Manager")]` cho các thao tác ghi, `[Authorize]` cho tra cứu.
- **Endpoints:**
  - `GET /api/suppliers`: Danh sách nhà cung cấp (mặc định lấy active, hoặc toàn bộ).
  - `GET /api/suppliers/{id}`: Chi tiết nhà cung cấp.
  - `POST /api/suppliers`: Tạo mới nhà cung cấp (validate trùng tên/SĐT).
  - `PUT /api/suppliers/{id}`: Cập nhật thông tin NCC.
  - `DELETE /api/suppliers/{id}`:
    - Kiểm tra xem NCC đã có phiếu nhập `ImportReceipts` liên kết hay chưa.
    - Nếu **đã có phiếu nhập**: Không xóa vật lý để tránh gãy FK và mất lịch sử kiểm toán $\rightarrow$ Thực hiện Soft-delete / Vô hiệu hóa: `supplier.IsActive = false; await _context.SaveChangesAsync();`.
    - Nếu **chưa có phiếu nhập**: Cho phép xóa bản ghi nếu cần thiết.

### 3.5 Phân hệ P1 — Inventory (Quản lý Kho & Nhập hàng)
- **Endpoints:**
  - `GET /api/inventory/stock`: Danh sách tồn kho sách. Hỗ trợ lọc `keyword` (tìm theo tên, tác giả, mã vạch) và `lowOnly = true` (lọc sách có `AvailableStock <= 5`).
  - `POST /api/inventory/adjust`: Điều chỉnh tồn kho thực tế sau kiểm kê (`AdjustStockDto { BookId, ActualQuantity, Reason }`).
    - Chạy trong Transaction: Cập nhật `StockQuantity = ActualQuantity`.
    - Ghi log kiểm kê / lý do điều chỉnh.
  - `GET /api/inventory/receipts`: Danh sách phiếu nhập hàng phân trang, lọc theo `supplierId`, `fromDate`, `toDate`.
  - `GET /api/inventory/receipts/{id}`: Chi tiết phiếu nhập kho kèm danh sách các đầu sách, số lượng và giá nhập.
  - `POST /api/inventory/receipts`: Tạo phiếu nhập hàng (`CreateImportReceiptDto { SupplierId, Note, Items }`).
    - Chạy trong Transaction: Tạo `ImportReceipt` và `ImportReceiptDetails`.
    - Tự động cộng tồn kho: `book.StockQuantity += item.Quantity`.
    - Cập nhật giá vốn mới nhất: `book.CostPrice = item.CostPrice` (nếu > 0).
    - Tính tổng tiền `TotalAmount = sum(item.Quantity * item.CostPrice)`.
  - `POST /api/inventory/import`: Tiếp tục duy trì route này làm alias tương thích ngược với code cũ.

### 3.6 Phân hệ P1 & P2 — Reports & Promotions RBAC
- **Reports:**
  - Cập nhật [ReportsController.cs](file:///d:/Downloads/CNPM/Library-System/BookKiosk.API/Controllers/ReportsController.cs): Đặt `[Authorize(Roles = "Admin")]` cho cả class.
  - Bảo đảm Staff và Manager khi gọi `GET /api/reports/revenue` hoặc `GET /api/reports/top-books` đều nhận HTTP 403 Forbidden.
- **Promotions:**
  - Cập nhật [PromotionsController.cs](file:///d:/Downloads/CNPM/Library-System/BookKiosk.API/Controllers/PromotionsController.cs):
    - `[HttpGet]`: Cho phép `Admin,Staff,Manager` xem danh sách KM.
    - `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`: Đặt `[Authorize(Roles = "Admin")]` (Staff chỉ được xem, thử sửa sẽ bị 403).
    - **Thống nhất Contract Toggle Khuyến mãi:**
      - CMS Client (`CmsApiClient.TogglePromotionActiveAsync`) ưu tiên gọi `PATCH /api/promotions/{id}/active` với body `{ isActive }`.
      - Tài liệu kiến trúc [docs/02-project-structure.md (L579)](file:///d:/Downloads/CNPM/Library-System/docs/02-project-structure.md#L579) đã định nghĩa sẵn `GET/POST/PATCH /api/promotions`.
      - Do đó, triển khai chính thức `[HttpPatch("{id:int}/active")]` với `[Authorize(Roles = "Admin")]` nhận `TogglePromotionActiveDto { IsActive }`, xử lý chuẩn xác các mã lỗi:
        + `400 Bad Request`: Body không hợp lệ.
        + `401 Unauthorized`: Chưa đăng nhập / thiếu token.
        + `403 Forbidden`: Token là Staff/Manager gọi thao tác thay đổi.
        + `404 Not Found`: Không tìm thấy promotion với `id`.
        + `409 Conflict`: Vi phạm quy tắc nghiệp vụ (ví dụ: kích hoạt chương trình có `EndDate < DateTime.UtcNow`).

---

## 4. CHIẾN LƯỢC KIỂM THỬ CHẤT LƯỢNG (TARGETED QA & ACCEPTANCE CRITERIA)

Tuân thủ nghiêm ngặt quy định kiểm thử của Leader: **Không dùng SQLite/InMemory để kết luận tính năng Transaction/Concurrency của SQL Server!** Phân tách môi trường rõ ràng:

### 4.1 Phân cấp Môi trường Kiểm thử
1. **Tier 1 — Contract & RBAC Tests (SQLite / WebApplicationFactory):**
   - Dùng để kiểm tra: Format JWT, Claims, Middleware 401/403/404, Validation DTO, Serialize/Deserialize dữ liệu.
2. **Tier 2 — SQL Server Transaction & Race Tests (BẮT BUỘC SQL Server QA Disposable):**
   - Chỉ chạy khi có biến môi trường `BOOKKIOSK_TEST_SQLSERVER` trỏ tới SQL Server QA. Tự sinh DB tạm thời `BookKiosk_QA_<guid>` và DROP sau khi chạy.
   - Dùng để chứng minh: Atomic reserve, chống double-pay, chống oversell, rollback khi lỗi phiếu nhập kho.
   - **Quy tắc:** Nếu môi trường chưa có `BOOKKIOSK_TEST_SQLSERVER`, ghi nhận trung thực trạng thái là **`BLOCKED — ENV`**, tuyệt đối **KHÔNG** tự nhận PASS.
3. **E2E — Full Live System Integration:**
   - Dựng đồng thời: Web UI CMS (`BookKiosk.CMS`) + Backend API (`BookKiosk.API`) + SQL Server QA DB.
   - Khi chưa ghép nhánh và chạy đồng thời cả 3 thành phần trên môi trường staging, ghi nhận trung thực là **`BLOCKED — INTEGRATION`**.

---

## 5. LỘ TRÌNH THỰC HIỆN & TIẾN ĐỘ THỰC TẾ

| Bước | Hạng mục công việc | Trạng thái thực hiện | Chi tiết triển khai |
|:---:|---|:---:|---|
| **1** | **Auth & RBAC CMS (P0)** | ✅ Hoàn thành 100% | `AuthController`, `IAuthService`, `AuthService`, BCrypt password verification, sinh JWT token với Role claims. |
| **2** | **POS Counter & Polling (P0)** | ✅ Hoàn thành 100% | `/counter/checkout` (giữ kho, trừ điểm), `/confirm-payment` (chốt Cash, trừ kho vật lý, cộng điểm, sinh PaymentTransaction, idempotent), `/cancel` (chỉ hủy Counter Pending, nhả kho), hợp nhất duy nhất route `GET /orders/{id}` chống AmbiguousMatchException. |
| **3** | **Orders CMS (P1)** | ✅ Hoàn thành 100% | `GET /orders` phân trang, đa bộ lọc (kênh, trạng thái, ngày), cờ cảnh báo `needsSupport`. |
| **4** | **Suppliers & Inventory (P1)** | ✅ Hoàn thành 100% | `SuppliersController` CRUD với soft-delete bảo vệ khóa ngoại. `InventoryController` `/stock` (kèm cờ `lowOnly`), `/adjust` (kiểm kê), `/receipts` (danh sách, chi tiết, tạo phiếu tăng tồn & cập nhật giá vốn). |
| **5** | **Reports & Promotions RBAC (P1/P2)** | ✅ Hoàn thành 100% | `ReportsController` `[Authorize(Roles = "Admin")]`. `PromotionsController` admin-only mutations + `PATCH /api/promotions/{id}/active`. |
| **6** | **Build Release Kiểm định** | ✅ Hoàn thành 100% | `dotnet build BookKiosk.slnx -c Release -m:1` $\rightarrow$ **0 Errors** (tất cả 3 dự án API, CMS, Kiosk đều compile thành công). |
| **7** | **Chạy Bộ Test Phân tầng** | ✅ Hoàn thành 100% | Tier 1 PASS 25/25; Tier 2 Concurrency ghi nhận `BLOCKED — ENV` theo đúng quy định Leader; Full Live E2E ghi nhận `BLOCKED — INTEGRATION`. |

---

## 6. BÁO CÁO NGHIỆM THU & ĐÁNH GIÁ CHẤT LƯỢNG THỰC HIỆN KẾ HOẠCH (EXECUTION & QUALITY REPORT)

> **Thời điểm nghiệm thu:** 2026-10-18  
> **Người thực hiện:** `@thien33` (Backend)  
> **Target Branch:** `thien`  
> **Backend Base Commit:** `6ef1def` | **CMS PR #33 Head:** `58d49cc` / `0de0dbe`  
> **Feature HEAD SHA:** `5e73ee1` (`5e73ee1505c249a4604eb651c8e7e1ddbe65dff8`)  
> **Bộ công cụ thực thi:** .NET SDK 10.0.402, MSBuild 17.13.20  

### 6.1 Lệnh Thực thi & Bộ lọc Kiểm thử Cụ thể (Commands & Test Filters)

```powershell
# 1. Lệnh kiểm tra biên dịch Release toàn bộ solution (0 Errors)
& 'C:\Users\pnt19\AppData\Local\Microsoft\dotnet\dotnet.exe' build "d:\Downloads\CNPM\Library-System\BookKiosk.slnx" -c Release -m:1

# 2. Lệnh thực thi Targeted Integration Test Suite phân tầng cho CMS PR #33
& 'C:\Users\pnt19\AppData\Local\Microsoft\dotnet\dotnet.exe' run --project "d:\Downloads\CNPM\Library-System\scratch\CmsIntegrationTests\CmsIntegrationTests.csproj" -c Release

# 3. Lệnh chạy Regression Test Kiosk Pipeline & Task 5.6
& 'C:\Users\pnt19\AppData\Local\Microsoft\dotnet\dotnet.exe' run --project "d:\Downloads\CNPM\Library-System\scratch\EntityGen\EntityGen.csproj" -c Release
```

### 6.2 Bảng Kết quả Kiểm thử Phân tầng Chi tiết (Targeted Test Results)

```
================================================================================
  BOOKKIOSK — CMS PR #33 BACKEND INTEGRATION TARGETED TEST SUITE
  TỔNG KẾT: PASS = 25 | BLOCKED = 4 | FAIL = 0
================================================================================
```

| Nhóm chức năng | Mã Test | Kịch bản kiểm thử | Phân tầng | Kết quả | Evidence Log / Thực chứng HTTP Pipeline |
|---|---|---|:---:|:---:|---|
| **Auth & RBAC** | `AUTH-01` | Đăng nhập đúng User Admin $\rightarrow$ cấp JWT hợp lệ | Tier 1 | ✅ **PASS** | HTTP 200 OK \| Token length=655, Role=Admin, FullName=Quản trị viên |
| | `AUTH-02` | Đăng nhập sai mật khẩu $\rightarrow$ 401 Unauthorized | Tier 1 | ✅ **PASS** | HTTP 401 Unauthorized \| Code=UNAUTHORIZED, Success=false |
| | `AUTH-03` | Đăng nhập tài khoản bị khóa (`IsActive=false`) $\rightarrow$ 401 | Tier 1 | ✅ **PASS** | HTTP 401 Unauthorized \| Code=UNAUTHORIZED, Success=false |
| | `AUTH-04` | Token Staff/Manager gọi endpoint Admin-only (Reports) | Tier 1 | ✅ **PASS** | HTTP 403 Forbidden \| Policy Roles=Admin, User Roles=Staff |
| | `AUTH-05` | POST `/api/auth/login` cấp token Staff đúng định dạng | Tier 1 | ✅ **PASS** | HTTP 200 OK \| Staff token role=Staff |
| **POS Counter** | `POS-01` | Checkout tạo đơn quầy Pending, giữ kho, tính tiền BE | Tier 1 | ✅ **PASS** | OrderId=1, Code=ORD-20261017-0001, Total=140.000đ |
| | `POS-07` | Hợp nhất endpoint `GET /api/orders/{id}` | Tier 1 | ✅ **PASS** | Trả đủ `Status` cho POS polling và `Items` cho CMS Detail |
| | `POS-03` | Xác nhận thanh toán tiền mặt `/confirm-payment` | Tier 1 | ✅ **PASS** | Chuyển Paid, SubTotal=150k, Total=140k, sinh PaymentTx |
| | `POS-04` | Confirm-payment lặp (Idempotency) | Tier 1 | ✅ **PASS** | Trả lại hóa đơn hợp lệ, không trừ kho lặp, không lỗi |
| | `POS-06A`| CMS cố hủy đơn Kiosk (Pending) qua endpoint quầy | Tier 1 | ✅ **PASS** | HTTP 409 Conflict \| Chỉ cho phép hủy đơn hàng tại quầy thu ngân |
| | `POS-06B`| CMS cố hủy đơn Counter đã Paid | Tier 1 | ✅ **PASS** | HTTP 409 Conflict \| Không thể hủy đơn hàng đã hoàn tất thanh toán |
| | `POS-05` | Hủy đơn Pending `/counter/{id}/cancel` $\rightarrow$ nhả kho | Tier 1 | ✅ **PASS** | Chuyển Cancelled, nhả ReservedQuantity thành công |
| | `POS-02` | Chống Oversell khi nhiều quầy checkout đồng thời | Tier 2 | ⚠️ **BLOCKED — ENV** | Cần disposable SQL Server QA database; không chạy trên DB chung |
| | `POS-04-R` | Chống Double-Pay cấp transaction DB RDBMS | Tier 2 | ⚠️ **BLOCKED — ENV** | Cần disposable SQL Server QA database BookKiosk_QA_<guid> |
| **Orders CMS** | `ORD-01` | Danh sách đơn hàng phân trang `GET /api/orders` | Tier 1 | ✅ **PASS** | Trả `PaginatedResultDto<OrderListItemDto>`, TotalCount=3 |
| | `ORD-02` | Bộ lọc đơn cần hỗ trợ (`needsSupport = true`) | Tier 1 | ✅ **PASS** | Lọc chuẩn các đơn Pending/Cancelled có Payment > 0 |
| | `ORD-03` | Chi tiết đơn hàng `GET /api/orders/{id}` nạp đầy đủ | Tier 1 | ✅ **PASS** | Nạp snapshot dòng hàng, Cashier=Thu ngân 01, Member=Nguyễn Văn Khách |
| **Suppliers** | `SUP-01` | Tạo mới (POST) và Cập nhật (PUT) nhà cung cấp | Tier 1 | ✅ **PASS** | SupplierId=1, Tên="Công ty Sách Kim Đồng" |
| | `SUP-02` | Xóa NCC đã có phiếu nhập $\rightarrow$ Soft-delete an toàn | Tier 1 | ✅ **PASS** | SupplierId=1 vẫn tồn tại trong DB với `IsActive = false`, bảo vệ FK |
| **Inventory** | `INV-01` | Xem tồn kho (`/stock`) lọc `lowOnly = true` | Tier 1 | ✅ **PASS** | Lọc chính xác các sách có `AvailableStock <= 5` |
| | `INV-02` | Điều chỉnh tồn kho thực tế (`POST /inventory/adjust`) | Tier 1 | ✅ **PASS** | BookId=1, StockQuantity mới=25 |
| | `INV-03` | Tạo phiếu nhập kho (`/receipts`) tăng tồn & đổi giá vốn | Tier 1 | ✅ **PASS** | Book2 Stock tăng lên 13, CostPrice cập nhật thành 32.000đ |
| | `INV-03-R` | Rollback transaction phiếu nhập kho khi lỗi dòng hàng | Tier 2 | ⚠️ **BLOCKED — ENV** | Chờ môi trường live SQL Server QA |
| **Promotions**| `PROMO-01`| Staff đọc danh sách khuyến mãi (`GET /promotions`) | Tier 1 | ✅ **PASS** | HTTP 200 OK |
| | `PROMO-02`| Staff cố gắng tạo/sửa/xóa/toggle KM | Tier 1 | ✅ **PASS** | Bị từ chối HTTP 403 Forbidden |
| | `PROMO-03`| Admin gọi `PATCH /api/promotions/{id}/active` | Tier 1 | ✅ **PASS** | Bật/tắt KM chuẩn RESTful; trả 409 khi kích hoạt KM đã quá hạn |
| **Regression**| `REG-ALL` | Hóa đơn Kiosk Task 5.6 & Kiosk Pipeline cũ | Tier 1 | ✅ **PASS** | Khớp 100% snapshot, Kiosk pipeline & DbInitializer chạy mượt mà |
| **E2E Smoke** | `E2E-CMS-SMOKE` | In-Process Smoke Test DTOs từ CMS Client PR #33 | Tier 1 | ✅ **PASS** | HTTP Status 200/400/401/403/409, JSON Contracts mapping 100% khớp |
| **Live E2E** | `E2E-LIVE-SYSTEM` | Full Live CMS App + Live Backend API + SQL Server QA DB | E2E | ⚠️ **BLOCKED — INTEGRATION** | Chờ ghép nhánh PR #33 trên môi trường staging/live; không tự nhận E2E PASS |

### 6.3 Bảng Đối chiếu Hợp đồng API (API Compliance Matrix) với PR #33
Toàn bộ các yêu cầu tích hợp từ CMS Client của `@lehuukhang` đã được Backend đáp ứng đầy đủ và chuẩn xác:

| CMS Client / File gọi | Endpoint Backend | HTTP Method | Cấu trúc DTO & Tham số | Trạng thái Contract & Smoke | Trạng thái Live E2E |
|---|---|:---:|---|:---:|:---:|
| `CmsApiClient.LoginAsync` | `/api/auth/login` | POST | `LoginRequestDto` $\rightarrow$ `LoginResponseDto` (`accessToken`, `fullName`, `role`) | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsPosApi.CheckoutAsync` | `/api/orders/counter/checkout` | POST | `CounterCheckoutRequestDto` $\rightarrow$ `CounterCheckoutResponseDto` | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsPosApi.ConfirmPaymentAsync` | `/api/orders/counter/{id}/confirm-payment` | POST | Empty body $\rightarrow$ `PosReceiptDto` (Hóa đơn K80) | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsPosApi.CancelOrderAsync` | `/api/orders/counter/{id}/cancel` | POST | Empty body $\rightarrow$ HTTP 200 OK | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsPosApi.GetPaymentStatusAsync` | `/api/orders/{id}` | GET | Phục vụ Polling trạng thái (`status`, `completedAt`, `serverTime`) | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsOrderApi.GetOrdersAsync` | `/api/orders` | GET | Paging + lọc đa tiêu chí + cờ `needsSupport` | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsOrderApi.GetOrderDetailAsync` | `/api/orders/{id}` | GET | `OrderDetailDto` (Full snapshot, dòng hàng, thu ngân, thành viên) | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsApiClient` (Suppliers) | `/api/suppliers` | GET/POST/PUT/DELETE | `SupplierDto`, `CreateSupplierDto`, `UpdateSupplierDto` | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsApiClient` (Inventory) | `/api/inventory/stock`<br/>`/api/inventory/adjust`<br/>`/api/inventory/receipts` | GET/POST | `StockItemDto` (lọc `lowOnly`), `AdjustStockDto`, `CreateImportReceiptDto` | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsApiClient` (Reports) | `/api/reports/revenue`<br/>`/api/reports/top-books` | GET | `[Authorize(Roles = "Admin")]` (Staff/Manager nhận 403) | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |
| `CmsApiClient.TogglePromotionActiveAsync` | `/api/promotions/{id}/active` | PATCH | `TogglePromotionActiveDto { isActive }` | ✅ 100% Khớp (PASS Smoke) | ⚠️ BLOCKED — INTEGRATION |

> **Ghi chú về kiểm thử tích hợp Live E2E:**  
> Tuân thủ tuyệt đối quy định của Leader: *"Không tự nhận E2E PASS khi chưa chạy đồng thời cả CMS App + Backend API + Live SQL Server QA"*. Mặc dù 100% các Contract DTO và HTTP Status đã đạt `PASS (In-Process Smoke)`, trạng thái toàn hệ thống Live E2E vẫn được ghi nhận minh bạch là **`BLOCKED — INTEGRATION`** để chờ phiên đối soát trực tiếp cùng `@lehuukhang` khi ghép nhánh.

---

### 6.4 Đánh giá Tuân thủ Quy định & Chất lượng Kiến trúc
1. **Tuân thủ Clean Architecture:** Tách bạch chặt chẽ Domain $\rightarrow$ Application (DTOs, Interfaces, Services) $\rightarrow$ Infrastructure (Repositories) $\rightarrow$ API (Controllers). Không để lọt logic nghiệp vụ vào Controllers.
2. **Bảo mật & Phân quyền (RBAC):**
   - Xác thực mật khẩu thông qua BCrypt an toàn.
   - Tuyệt đối không log hoặc trả về `PasswordHash` ra ngoài Client.
   - Token JWT được cấu hình chuẩn Role Claims (`Admin`, `Staff`, `Manager`), bảo vệ nghiêm ngặt các endpoint nhạy cảm (Reports và Promotion mutations là Admin-only).
3. **Bảo toàn Dữ liệu & Toàn vẹn Lịch sử (Integrity):**
   - Nhà cung cấp đã phát sinh phiếu nhập kho được áp dụng Soft-delete (`IsActive = false`), ngăn chặn lỗi Foreign Key constraint và bảo vệ sổ sách kế toán.
   - Điều chỉnh tồn kho và tạo phiếu nhập đều cập nhật chuẩn xác ChangeTracker và cơ sở dữ liệu.
4. **Giải quyết Triệt để Xung đột Route (No AmbiguousMatchException):**
   - Thiết kế 1 endpoint duy nhất `GET /api/orders/{orderId}` phục vụ đồng thời cho cả nhu cầu Polling nhanh của POS (`CmsPosApi`) lẫn xem chi tiết sâu của CMS (`CmsOrderApi`).
5. **Zero Regression:**
   - Luồng bán hàng Kiosk, Webhook SePay, F3 Dev, và Hóa đơn Kiosk Task 5.6 (`GET /api/orders/kiosk/{orderId}/receipt`) hoạt động hoàn hảo, không có bất kỳ xung đột nào.
6. **Tính Minh bạch về Môi trường Kiểm thử:**
   - Không giả lập sai lệch: Ghi nhận trung thực trạng thái `BLOCKED — ENV` cho các bài test locking/race Tier 2 do chưa có biến môi trường `BOOKKIOSK_TEST_SQLSERVER`, tuân thủ 100% chỉ thị của Leader.

---

## 7. BÁO CÁO KHẮC PHỤC 4 ĐIỂM RUNTIME THEO ĐÁNH GIÁ CỦA QA LEADER TRÊN PR #35

> **Căn cứ phúc tra:** Review của QA Leader trên PR #35 (HEAD `a585741`).  
> **Môi trường thực thi:** Microsoft SQL Server 2025 Standard Developer Edition (`localhost`), Disposable QA Database `BookKiosk_QA_<guid>`, .NET 10 Release mode.  
> **Quy chuẩn:** Xác minh đồng thời cả HTTP response status và trạng thái thực tế trong database SQL Server (`Orders`, `PaymentTransactions`, `Books`).

### 7.1 Tóm tắt Thay đổi Kỹ thuật
1. **POS-01 (P0):** Chặn thẳng đơn `PaymentMethod.QR` tại luồng xác nhận thanh toán tiền mặt (`ConfirmCounterPaymentAsync`). Nếu đơn hàng chưa ở trạng thái `Paid` (qua webhook SePay), ném `InvalidOperationException` $\rightarrow$ API trả về **HTTP 409 Conflict**. Tuyệt đối không sinh giao dịch `PaymentTransaction Gateway="Cash"` và không đổi trạng thái đơn.
2. **POS-02 (P0):** Đọc cấu hình `SePay:AccountNumber` (fallback `0366994409`) và `SePay:BankCode` / `SePay:BankName` (fallback `MB`) động từ `IConfiguration` trong `CheckoutCounterAsync`. Giữ nguyên 100% `CheckoutKioskAsync`.
3. **POS-03 (P1):** Đọc thời hạn đơn từ `OrderSettings:OrderTimeoutMinutes` (mặc định 4 phút / 240s) thay vì hardcode 10 phút. Đồng bộ 100% giữa thời gian hết hạn hiển thị trên giao diện POS (`RemainingSeconds`) và chu kỳ quét tự động của `ExpiredOrderCleanupService`.
4. **INV-01 (P1):** Validate Invariant nghiệp vụ tại `InventoryService.AdjustStockAsync`: nếu `dto.ActualQuantity < book.ReservedQuantity`, lập tức ném `InvalidOperationException` $\rightarrow$ API trả về **HTTP 409 Conflict**. Ngăn chặn hoàn toàn lỗi văng HTTP 500 do vi phạm Check Constraint SQL Server `CK_Books_ReservedQuantity`.
5. **Chống gãy build test cũ:** Constructor `OrderService` đặt `IConfiguration? configuration = null` ở cuối danh sách tham số.

### 7.2 Bảng Kết quả Thực chứng Targeted Runtime trên SQL Server Thật

```
================================================================================
  BOOKKIOSK — TARGETED QA RUNTIME VERIFICATION (SQL SERVER DEVELOPER/EXPRESS)
  DATABASE DISPOSABLE: BookKiosk_QA_dd69252a32d | CLEANUP: DROPPED CLEANLY
  TỔNG KẾT: PASS = 6 | FAIL = 0
================================================================================
```

| Mã Lỗi / Test ID | Mức độ | Kịch bản kiểm thử | Trạng thái HTTP | Trạng thái SQL Server Thực tế (DB Evidence) | Kết luận |
|---|:---:|---|---|---|:---:|
| **POS-01** | **P0** | Chặn confirm-payment tiền mặt cho đơn QR khi chưa có webhook | **HTTP 409 Conflict** (`code: CONFLICT`) | `Orders.OrderStatus = 1 (Pending)`<br/>`PaymentTransactions COUNT = 0` (Không ghi Cash transaction) | ✅ **PASS** |
| **POS-02** | **P0** | Counter QR URL đọc tài khoản động từ cấu hình (không hardcode) | **HTTP 200 OK** (`SepayQrCodeUrl` chứa `acc=0366994409&bank=MB`) | URL khớp 100% cấu hình runtime; không chứa số tài khoản cũ `0123456789` | ✅ **PASS** |
| **POS-03** | **P1** | Đồng bộ Timeout Counter QR theo `OrderTimeoutMinutes` (4 phút) | **HTTP 200 OK** (`ExpiresAt = CreatedAt + 4 phút`, `RemainingSeconds = 239s`) | Ngưỡng đếm ngược khớp hoàn toàn với chu kỳ quét của `ExpiredOrderCleanupService` (4 phút) | ✅ **PASS** |
| **INV-01** | **P1** | Validate Invariant kho (`ActualQuantity < ReservedQuantity`) | **HTTP 409 Conflict** (`code: CONFLICT`) | `Books.StockQuantity = 10`, `ReservedQuantity = 7` (Bảo toàn check constraint `CK_Books_ReservedQuantity`, không văng 500) | ✅ **PASS** |
| **POS_A1_B1** | **Reg** | Xác nhận thanh toán tiền mặt (Cash) hợp lệ tại quầy | **HTTP 200 OK** (Trả `PosReceiptDto` chuẩn K80) | `Orders.OrderStatus = 2 (Paid)`<br/>`PaymentTransactions Gateway = 'Cash'`, `Amount = 190000.00` | ✅ **PASS** |
| **POS_A1_IDEM**| **Reg** | Gọi lại confirm-payment cho đơn Cash đã Paid (Idempotency) | **HTTP 200 OK** (Trả về hóa đơn đã lưu) | `PaymentTransactions COUNT = 1` (Không trừ kho lặp, không sinh giao dịch trùng) | ✅ **PASS** |

---

> [!NOTE]
> **Kết luận sau phúc tra:** Cả 4 điểm thiếu sót (POS-01, POS-02, POS-03, INV-01) đã được giải quyết triệt để và kiểm chứng thành công trên SQL Server Developer thật. Hệ thống đảm bảo 0 lỗi biên dịch, 0 regression luồng Kiosk, sẵn sàng cho Leader phê duyệt.
