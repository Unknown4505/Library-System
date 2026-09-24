# Database Schema & Data Dictionary — BookKiosk

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

> **Đọc trước khi tạo Entity và Migration.** 
> Đảm bảo kiểu dữ liệu và tên cột đúng chính xác như thiết kế để không bị conflict Migration giữa các thành viên.

## 1. Nguyên tắc thiết kế Data Type

- `Id` (Khóa chính): Luôn dùng `int` IDENTITY(1,1).
- Tiền tệ (Giá bán, Giá vốn, Tổng tiền): Luôn dùng `decimal(18,0)` (Do VNĐ không có số lẻ thập phân). **Tất cả giảm giá đều là số tiền VNĐ cố định — không dùng %.**
- Chuỗi ký tự chuẩn: 
  - Mã (Barcode, OrderCode): `varchar(50)`
  - Tên/Tiêu đề ngắn: `nvarchar(255)`
  - Ghi chú/Mô tả: `nvarchar(max)`
  - SĐT: `varchar(20)`
- Audit Fields (áp dụng cho 100% các bảng):
  - `CreatedAt` (`datetime2`)
  - `UpdatedAt` (`datetime2`, có thể nullable tùy bảng)
- Mọi enum trong code C# sẽ map xuống database là `int`.

---

## 2. Lược đồ các Bảng (Tables)

### 2.1 Quản lý Sách & Danh mục

**Bảng `Categories`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `CategoryId` | `int` | `int` | PK |
| `Name` | `string` | `nvarchar(255)` | Bắt buộc |
| `Description` | `string?` | `nvarchar(max)` | Nullable |

**Bảng `Areas`** (Khu vực / Tủ / Kệ sách)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `AreaId` | `int` | `int` | PK |
| `Name` | `string` | `nvarchar(255)` | Bắt buộc (VD: "Kệ A1", "Tầng 2") |
| `MapCoordinates` | `string?` | `varchar(500)` | Tọa độ trên UI map (JSON X, Y) |

**Bảng `Books`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `BookId` | `int` | `int` | PK |
| `Barcode` | `string` | `varchar(50)` | UNIQUE Index. Bắt buộc |
| `Title` | `string` | `nvarchar(255)` | Bắt buộc |
| `Author` | `string` | `nvarchar(255)` | Bắt buộc |
| `Publisher`| `string?` | `nvarchar(255)` | Nullable |
| `ImageUrl` | `string?` | `nvarchar(500)` | Link ảnh bìa |
| `CostPrice` | `decimal` | `decimal(18,0)` | Giá vốn nhập kho |
| `SellingPrice`| `decimal` | `decimal(18,0)` | Giá bán Kiosk/POS |
| `StockQuantity`| `int` | `int` | Tồn kho vật lý thực tế. `Check >= 0` |
| `ReservedQuantity`| `int`| `int` | Tồn kho đang bị giữ chỗ chờ TT. `Check >= 0` |
| `CategoryId` | `int` | `int` | FK -> Categories |
| `AreaId` | `int?` | `int` | FK -> Areas (Nullable) |
| `IsActive` | `bool` | `bit` | Mặc định `true` |

*(Công thức Tồn kho khả dụng để bán = `StockQuantity - ReservedQuantity`)*

---

### 2.2 Quản lý Nhà cung cấp & Nhập kho

**Bảng `Suppliers`** (Nhà cung cấp)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `SupplierId` | `int` | `int` | PK |
| `Name` | `string` | `nvarchar(255)` | Bắt buộc |
| `PhoneNumber` | `string?` | `varchar(20)` | Nullable |
| `Email` | `string?` | `varchar(255)` | Nullable |
| `Address` | `string?` | `nvarchar(500)` | Nullable |
| `IsActive` | `bool` | `bit` | Mặc định `true` |

**Bảng `ImportReceipts`** (Phiếu nhập hàng — ghi nhận từng lần nhập hàng từ nhà cung cấp)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `ImportReceiptId` | `int` | `int` | PK |
| `SupplierId` | `int` | `int` | FK -> Suppliers. Bắt buộc |
| `UserId` | `int` | `int` | FK -> Users (Nhân viên lập phiếu). Bắt buộc |
| `TotalAmount` | `decimal` | `decimal(18,0)` | Tổng tiền nhập = Σ(CostPrice × Quantity) |
| `Note` | `string?` | `nvarchar(max)` | Nullable |

**Bảng `ImportReceiptDetails`** (Chi tiết từng dòng sách trong phiếu nhập)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `ImportReceiptId` | `int` | `int` | PK (Composite) + FK -> ImportReceipts |
| `BookId` | `int` | `int` | PK (Composite) + FK -> Books |
| `Quantity` | `int` | `int` | Bắt buộc, `> 0` |
| `CostPrice` | `decimal` | `decimal(18,0)` | Giá vốn nhập thực tế tại thời điểm này |

> **Ghi chú thiết kế:** `ImportReceiptDetails` dùng **Composite PK = (ImportReceiptId, BookId)** — không có cột Id riêng. Biến động tồn kho được theo dõi qua ngày tạo phiếu (`ImportReceipts.CreatedAt`) và số lượng trong từng dòng chi tiết, không cần bảng log riêng.

---

### 2.3 Quản lý Khách hàng & Thành viên

**Bảng `Members`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `MemberId` | `int` | `int` | PK |
| `PhoneNumber` | `string` | `varchar(20)` | UNIQUE Index. Bắt buộc |
| `FullName` | `string` | `nvarchar(255)` | Bắt buộc |
| `Points` | `int` | `int` | Số điểm tích lũy hiện tại. Mặc định `0` |

**Bảng `PointTransactions`** (Lịch sử tích/dùng điểm — dùng để tra cứu, đối soát)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `PointTransactionId`| `int`| `int` | PK |
| `MemberId` | `int` | `int` | FK -> Members |
| `OrderId` | `int?` | `int` | FK -> Orders (Nullable) |
| `Type` | `PointTransactionType`| `int` | Enum: **Earned(1)** = tích điểm sau khi Paid, **Redeemed(2)** = dùng điểm khi thanh toán |
| `Points` | `int` | `int` | Số điểm thay đổi (luôn dương — Type xác định chiều cộng/trừ) |
| `Description` | `string?` | `nvarchar(255)` | Nullable. VD: "Tích điểm đơn ORD-20260923-0001" |

> **Business Rule điểm thưởng:**
> - **Tích:** mỗi **10.000đ** chi tiêu thực trả (sau KM, sau dùng điểm) = **1 điểm** (làm tròn xuống).
> - **Dùng:** **1 điểm = 1.000đ** giảm. Tối đa **100 điểm (= 100.000đ)** / đơn hàng. Không có ngưỡng điểm tối thiểu.
> - Vẫn tích điểm trên phần tiền thực trả kể cả khi đơn có dùng điểm.
> - **Luồng xử lý:** `Members.Points` là số điểm hiện tại (real-time). `PointTransactions` là log để trace lịch sử, không dùng để tính tổng điểm.

---

### 2.4 Quản lý Đơn hàng & Thanh toán

**Bảng `Orders`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `OrderId` | `int` | `int` | PK |
| `OrderCode` | `string` | `varchar(50)` | UNIQUE. Format: `ORD-yyyyMMdd-XXXX` — trong đó `XXXX` là số thứ tự tăng dần **toàn hệ thống** (không reset theo ngày). VD: `ORD-20260923-0042`. Sinh bằng cách lấy `OrderId` padding 4 chữ số. Dùng làm nội dung chuyển khoản SePay để đối soát webhook |
| `SaleChannel` | `SaleChannel`| `int` | Enum: **Kiosk(1)** = tự phục vụ, **Counter(2)** = bán tại quầy |
| `OrderStatus` | `OrderStatus`| `int` | Enum: **Pending(1)** = chờ thanh toán, **Paid(2)** = đã thanh toán xong, **Cancelled(3)** = đã hủy |
| `PaymentMethod`| `PaymentMethod`| `int`| Enum: **Cash(1)** = tiền mặt (chỉ quầy), **QR(2)** = chuyển khoản |
| `UserId` | `int?` | `int` | FK -> Users. **Nullable** — chỉ có giá trị khi `SaleChannel = Counter` (nhân viên tạo đơn). Kiosk tự phục vụ = `null` |
| `MemberId` | `int?` | `int` | FK -> Members. **Nullable** — khách vãng lai không có |
| `PromotionId` | `int?` | `int` | FK -> Promotions. **Nullable** — **1 đơn chỉ được áp tối đa 1 chương trình KM** |
| `SubTotal` | `decimal` | `decimal(18,0)` | **Tổng tiền hàng gốc** = Σ(`LineTotal`) của tất cả dòng `OrderDetails` |
| `DiscountAmount`| `decimal` | `decimal(18,0)` | **Tiền giảm từ KM** (VNĐ cố định). = 0 nếu không có KM |
| `PointsUsed` | `int` | `int` | Số điểm thành viên đã dùng để thanh toán. = 0 nếu không dùng |
| `TotalAmount` | `decimal` | `decimal(18,0)` | **Tiền thực trả** = `SubTotal - DiscountAmount - (PointsUsed × 1.000đ)`. Tối thiểu = 0 |
| `CompletedAt` | `DateTime?` | `datetime2` | Timestamp khi đơn được xác nhận **Paid**. Null khi Pending/Cancelled |

> **Giải thích rõ các field:**
> - `OrderStatus` = **trạng thái hiện tại** của đơn. 3 trạng thái, không phức tạp hơn.
> - `CompletedAt` = **mốc thời gian** để tính doanh thu, in hóa đơn — bổ sung cho `OrderStatus`, không trùng lặp.
> - `SubTotal` = tổng tiền trước giảm giá; `TotalAmount` = tiền sau tất cả giảm — số tiền khách trả thực tế.
> - `DiscountAmount` = số tiền VNĐ được giảm — luôn là số nguyên, không bao giờ là %.
> - **Tích điểm** = insert `PointTransactions(Earned)` + cộng `Members.Points` sau khi đơn Paid.
> - **Dùng điểm** = ghi `PointsUsed` trên đơn + insert `PointTransactions(Redeemed)` + trừ `Members.Points`.

> **Tại sao cần trạng thái `Cancelled`? — Áp dụng cho cả Kiosk lẫn Quầy**
> - **Kiosk (tự động):** Background Job quét Order `Pending` quá **4 phút** (config key: `OrderTimeoutMinutes` trong `appsettings.json`) → đổi thành `Cancelled` + nhả `ReservedQuantity`. Kiosk đang poll sẽ nhận `Cancelled` → hiển thị "Hết hạn thanh toán" → về Idle.
> - **Quầy (thủ công):** Nhân viên tạo đơn nháp (Pending) để khách xem giỏ hàng. Khách từ chối → nhân viên bấm "Hủy đơn" → Backend đổi thành `Cancelled` + nhả `ReservedQuantity`.
> - **Audit Trail:** Giữ lại record để thống kê đơn bị hủy — không xóa.

**Bảng `OrderDetails`** (Chi tiết từng dòng sách trong đơn hàng)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `OrderId` | `int` | `int` | PK (Composite) + FK -> Orders |
| `BookId` | `int` | `int` | PK (Composite) + FK -> Books |
| `UnitPriceAtTime`| `decimal` | `decimal(18,0)` | **Giá bán tại thời điểm mua** — snapshot để bảo toàn lịch sử khi giá sách thay đổi sau này |
| `Quantity` | `int` | `int` | Bắt buộc, `> 0` |
| `LineTotal` | `decimal` | `decimal(18,0)` | = `UnitPriceAtTime × Quantity`. Tính sẵn để tránh tính lại trong code |

> **Ghi chú thiết kế:** Dùng **Composite PK = (OrderId, BookId)** — không có cột `OrderDetailId` riêng. 1 đơn hàng không thể có 2 dòng cùng 1 cuốn sách; nếu khách thêm cùng cuốn thì cộng `Quantity` vào dòng đã có.

**Bảng `PaymentTransactions`** (Lưu lịch sử webhook thanh toán — chống gian lận & trace lỗi)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `TransactionId` | `int` | `int` | PK |
| `OrderId` | `int` | `int` | FK -> Orders |
| `ReferenceCode` | `string` | `varchar(50)` | Mã tham chiếu từ SePay. **UNIQUE Index** (Idempotent — webhook gọi lặp không insert 2 lần) |
| `Amount` | `decimal` | `decimal(18,0)` | Số tiền khách thực tế chuyển khoản |
| `Gateway` | `string` | `varchar(50)` | Cổng thanh toán, VD: "SePay" |

---

### 2.5 Khuyến Mãi (Promotions)

Hệ thống hỗ trợ **2 loại khuyến mãi**. Tất cả đều giảm bằng **tiền mặt VNĐ cố định**, không dùng %.

- **Loại 1 — KM Hóa đơn (`OrderDiscount`):** Áp khi tổng đơn đạt ngưỡng tối thiểu. Backend tự chọn KM tốt nhất tại Kiosk; nhân viên bấm "Áp KM" tại quầy.
- **Loại 2 — KM Sản phẩm (`ProductDiscount`):** Áp khi giỏ hàng chứa sản phẩm được KM; Backend tự động áp khi scan.

**Bảng `Promotions`** (Chương trình khuyến mãi — bảng mẹ)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `PromotionId` | `int` | `int` | PK |
| `Name` | `string` | `nvarchar(255)` | VD: "Giảm 50K cho đơn từ 500K", "KM Sách Thiếu Nhi Tháng 9" |
| `Description` | `string?` | `nvarchar(max)` | Nullable |
| `PromotionType` | `PromotionType` | `int` | Enum: **OrderDiscount(1)** = KM theo hóa đơn, **ProductDiscount(2)** = KM theo sản phẩm |
| `StartDate` | `DateTime` | `datetime2` | Bắt buộc |
| `EndDate` | `DateTime` | `datetime2` | Bắt buộc |
| `IsActive` | `bool` | `bit` | Admin bật/tắt thủ công |

**Bảng `PromotionOrderDiscounts`** (Chi tiết KM hóa đơn — chỉ dùng khi `PromotionType = OrderDiscount`)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `PromotionId` | `int` | `int` | PK + FK -> Promotions (quan hệ 1-1) |
| `MinOrderValue` | `decimal` | `decimal(18,0)` | Ngưỡng `SubTotal` tối thiểu để áp dụng. VD: `500000` |
| `DiscountAmount` | `decimal` | `decimal(18,0)` | Số tiền VNĐ được giảm cố định. VD: `50000` |

**Bảng `PromotionProductDiscounts`** (Chi tiết KM sản phẩm — chỉ dùng khi `PromotionType = ProductDiscount`)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `PromotionId` | `int` | `int` | PK (Composite) + FK -> Promotions |
| `BookId` | `int` | `int` | PK (Composite) + FK -> Books |
| `DiscountAmount` | `decimal` | `decimal(18,0)` | Số tiền VNĐ giảm cho sản phẩm này. VD: `20000` |

> **Business Rule áp dụng KM:**
> - **1 đơn hàng chỉ được áp tối đa 1 chương trình KM** — nhưng có thể dùng đồng thời với đổi điểm thành viên.
> - Kiosk: Backend tự chọn KM hóa đơn có `DiscountAmount` lớn nhất thỏa `MinOrderValue`. KM sản phẩm tự động áp khi scan sách.
> - Quầy (POS): Nhân viên bấm "Áp KM" thủ công để chọn KM hóa đơn.
> - `Orders.DiscountAmount` = số tiền giảm thực tế áp cho đơn (= `PromotionOrderDiscounts.DiscountAmount` hoặc Σ `PromotionProductDiscounts.DiscountAmount` × số lượng từng sách).

---

### 2.6 Hệ thống & Thiết bị

**Bảng `Users`** (Tài khoản Web Admin — Thủ thư và Admin)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `UserId` | `int` | `int` | PK |
| `Username` | `string` | `varchar(100)` | UNIQUE Index |
| `PasswordHash` | `string` | `nvarchar(max)` | BCrypt |
| `FullName` | `string` | `nvarchar(255)` | Bắt buộc |
| `Role` | `UserRole`| `int` | Enum: Admin(1), Staff(2) |
| `IsActive` | `bool` | `bit` | Mặc định true |

> **Xác thực:** Dùng **JWT stateless** (Access Token 15–30 phút). Không lưu Refresh Token vào DB — khi Access Token hết hạn, nhân viên đăng nhập lại. Phù hợp với môi trường nội bộ, tập trung vào tính đơn giản.

**Bảng `Kiosks`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `KioskId` | `int` | `int` | PK |
| `KioskName` | `string` | `nvarchar(255)` | VD: "Kiosk Tầng 1" |
| `MacAddress` | `string` | `varchar(50)` | UNIQUE Index. Định danh Kiosk |
| `Status` | `KioskStatus`| `int` | Enum: Online(1), Offline(2), Error(3) |
| `LastPingAt` | `DateTime?` | `datetime2` | Backend cập nhật mỗi 30s khi Kiosk gọi heartbeat |

**Bảng `KioskIncidents`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `IncidentId` | `int` | `int` | PK |
| `KioskId` | `int` | `int` | FK -> Kiosks |
| `ErrorCode` | `string` | `varchar(50)` | VD: "CAM_DISCONNECTED" |
| `Description` | `string` | `nvarchar(max)` | |
| `ResolvedAt` | `DateTime?` | `datetime2` | Nullable. Đánh dấu đã fix |

---

## 3. Quan hệ giữa các Bảng (Relationships)

> Dùng section này để vẽ **Class Diagram** và viết **init.sql** (FK constraints).

### 3.1 Bảng quan hệ đầy đủ

| # | Bảng "Nhiều" (FK bên này) | FK Column | → Bảng "Một" (PK bên kia) | Bắt buộc? | ON DELETE | Ghi chú / Nav Property |
|---|---|---|---|---|---|---|
| 1 | `Books` | `CategoryId` | `Categories` | ✅ Bắt buộc | RESTRICT | `Book.Category` / `Category.Books` |
| 2 | `Books` | `AreaId` | `Areas` | ❌ Nullable | SET NULL | `Book.Area` / `Area.Books` |
| 3 | `ImportReceipts` | `SupplierId` | `Suppliers` | ✅ Bắt buộc | RESTRICT | `ImportReceipt.Supplier` |
| 4 | `ImportReceipts` | `UserId` | `Users` | ✅ Bắt buộc | RESTRICT | `ImportReceipt.CreatedByUser` |
| 5 | `ImportReceiptDetails` | `ImportReceiptId` | `ImportReceipts` | ✅ Composite PK | CASCADE | `ImportReceiptDetail.ImportReceipt` |
| 6 | `ImportReceiptDetails` | `BookId` | `Books` | ✅ Composite PK | RESTRICT | `ImportReceiptDetail.Book` |
| 7 | `PointTransactions` | `MemberId` | `Members` | ✅ Bắt buộc | CASCADE | `PointTransaction.Member` |
| 8 | `PointTransactions` | `OrderId` | `Orders` | ❌ Nullable | SET NULL | `PointTransaction.Order` |
| 9 | `Orders` | `UserId` | `Users` | ❌ Nullable | SET NULL | `Order.CreatedByUser` — null khi Kiosk |
| 10 | `Orders` | `MemberId` | `Members` | ❌ Nullable | SET NULL | `Order.Member` — null khi khách vãng lai |
| 11 | `Orders` | `PromotionId` | `Promotions` | ❌ Nullable | SET NULL | `Order.Promotion` — null khi không có KM |
| 12 | `OrderDetails` | `OrderId` | `Orders` | ✅ Composite PK | CASCADE | `OrderDetail.Order` |
| 13 | `OrderDetails` | `BookId` | `Books` | ✅ Composite PK | RESTRICT | `OrderDetail.Book` |
| 14 | `PaymentTransactions` | `OrderId` | `Orders` | ✅ Bắt buộc | CASCADE | `PaymentTransaction.Order` |
| 15 | `PromotionOrderDiscounts` | `PromotionId` | `Promotions` | ✅ PK (1-1) | CASCADE | `PromotionOrderDiscount.Promotion` |
| 16 | `PromotionProductDiscounts` | `PromotionId` | `Promotions` | ✅ Composite PK | CASCADE | `PromotionProductDiscount.Promotion` |
| 17 | `PromotionProductDiscounts` | `BookId` | `Books` | ✅ Composite PK | RESTRICT | `PromotionProductDiscount.Book` |
| 18 | `KioskIncidents` | `KioskId` | `Kiosks` | ✅ Bắt buộc | CASCADE | `KioskIncident.Kiosk` |

### 3.2 Giải thích ON DELETE

| Hành vi | Ý nghĩa | Khi nào dùng |
|---|---|---|
| **CASCADE** | Xóa bảng cha → tự xóa luôn các dòng con | Bảng detail phụ thuộc hoàn toàn vào cha (VD: `OrderDetails` mất nghĩa khi không có `Order`) |
| **RESTRICT** | Không cho xóa bảng cha nếu còn dòng con tham chiếu | Bảng cha là master data cần bảo toàn (VD: không xóa `Books` khi còn đơn hàng) |
| **SET NULL** | Xóa bảng cha → set FK = null ở bảng con | Quan hệ optional — bản ghi con vẫn có nghĩa khi cha bị xóa |

### 3.3 Composite PK — lưu ý khi dùng EF Core

3 bảng dùng **Composite PK** (không có cột `Id` riêng):

| Bảng | Composite PK | Cách config trong EF Core |
|---|---|---|
| `ImportReceiptDetails` | `(ImportReceiptId, BookId)` | `modelBuilder.Entity<ImportReceiptDetail>().HasKey(x => new { x.ImportReceiptId, x.BookId })` |
| `OrderDetails` | `(OrderId, BookId)` | `modelBuilder.Entity<OrderDetail>().HasKey(x => new { x.OrderId, x.BookId })` |
| `PromotionProductDiscounts` | `(PromotionId, BookId)` | `modelBuilder.Entity<PromotionProductDiscount>().HasKey(x => new { x.PromotionId, x.BookId })` |

`PromotionOrderDiscounts` dùng `PromotionId` làm **PK đơn** (quan hệ 1-1 với `Promotions`):
```csharp
modelBuilder.Entity<PromotionOrderDiscount>().HasKey(x => x.PromotionId);
modelBuilder.Entity<PromotionOrderDiscount>()
    .HasOne(x => x.Promotion)
    .WithOne(x => x.OrderDiscount)
    .HasForeignKey<PromotionOrderDiscount>(x => x.PromotionId);
```

### 3.4 ERD dạng sơ đồ

```
[Categories] ──────────────────────────────── [Books] ──── [Areas]
                                                 │ │
              [Suppliers] ── [ImportReceipts] ───┘ │
                               │                   │
                            [Users] ────────── [ImportReceiptDetails]

[Members] ──────────────────── [Orders] ──────────── [OrderDetails]
    │                           │  │  │                    │
[PointTransactions]    [Promotions] │ [PaymentTransactions] └── [Books]
                           │        └────── [Users] (nullable, Counter only)
              [PromotionOrderDiscounts]
              [PromotionProductDiscounts] ──── [Books]

[Kiosks] ──── [KioskIncidents]
```

---

## 4. Các Index và Constraints Bắt Buộc

1. **Unique Indexes:**
   - `IX_Books_Barcode` (UNIQUE)
   - `IX_Members_PhoneNumber` (UNIQUE)
   - `IX_Orders_OrderCode` (UNIQUE)
   - `IX_PaymentTransactions_ReferenceCode` (UNIQUE)
   - `IX_Kiosks_MacAddress` (UNIQUE)
   - `IX_Users_Username` (UNIQUE)

2. **Check Constraints Tồn Kho:**
   - Tồn kho không thể âm: `ALTER TABLE Books ADD CONSTRAINT CK_Books_StockQuantity CHECK (StockQuantity >= 0)`
   - Tồn kho giữ chỗ không thể âm và không được lớn hơn tổng tồn: `CHECK (ReservedQuantity >= 0 AND ReservedQuantity <= StockQuantity)`

---

## 5. Giải thích cơ chế: Giữ chỗ kho (Reserved)

Để chống Race Condition (nhiều Kiosk cùng thanh toán 1 cuốn sách cuối cùng).
Khi Kiosk bấm "Thanh toán" (hoặc Nhân viên tạo đơn nháp tại quầy):
1. Tìm sách. Check `(StockQuantity - ReservedQuantity) >= Quantity Yêu cầu`
2. Đủ kho -> `ReservedQuantity += Quantity Yêu cầu`. Lưu Order (Pending).
3. **Kiosk:** Đếm ngược **4 phút** cho thanh toán SePay (config: `OrderTimeoutMinutes`).
   - Trạng thái 1 (Thành công): SePay webhook gọi về. `StockQuantity -= Yêu cầu`, `ReservedQuantity -= Yêu cầu`.
   - Trạng thái 2 (Hết giờ): Background job quét Order `Pending` > `OrderTimeoutMinutes`. Đổi thành `Cancelled`, `ReservedQuantity -= Yêu cầu`.
4. **Quầy:** Nhân viên xác nhận thu tiền → Backend đổi thành `Paid`, trừ kho ngay. Hoặc nhân viên hủy → `Cancelled`, nhả kho.

Cơ chế này đảm bảo không bao giờ bị bán lố sách (overselling) và không bị khóa cứng tồn kho. Mọi người code cần chú ý không sửa trực tiếp cột `StockQuantity` lúc đang checkout.

### LƯU Ý KHI CODE ĐỂ CHỐNG DEADLOCK & RACE CONDITION:

1. **Tránh Deadlock (Khóa chéo):** 
   Khi khách mua nhiều sách cùng lúc, nguyên tắc kinh điển là **LUÔN SORT (Sắp xếp) BookId** trước khi thực hiện Transaction giữ chỗ. Việc khóa dòng (lock row) trong Database theo thứ tự đồng nhất (từ nhỏ đến lớn) sẽ triệt tiêu hoàn toàn 100% nguy cơ Deadlock.
   ```csharp
   var sortedBookIds = cart.Items.OrderBy(x => x.BookId).ToList();
   // Bắt đầu Transaction và loop qua sortedBookIds để xử lý
   ```

2. **Chống Race Condition bằng Atomic Update:**
   **Tuyệt đối không** code theo pattern `Select` sách -> kiểm tra if đủ kho -> gán giá trị biến -> `SaveChanges()`. Tại vì ở môi trường đa luồng, từ lúc bạn Select đến lúc Save, số liệu có thể đã bị thread khác đổi mất.
   Thay vào đó, phải dùng **Update trực tiếp (Atomic)** bằng `ExecuteUpdate` trong EF Core (hoặc Raw SQL). Cách này đẩy trách nhiệm phân luồng Locking cho SQL Server xử lý an toàn tuyệt đối:
   ```csharp
   int rowsAffected = dbContext.Books
       .Where(b => b.BookId == id && (b.StockQuantity - b.ReservedQuantity) >= requestedQty)
       .ExecuteUpdate(b => b.SetProperty(x => x.ReservedQuantity, x => x.ReservedQuantity + requestedQty));
   
   if (rowsAffected == 0) {
       throw new Exception("Hết sách hoặc bị người khác mua mất!");
   }
   ```
