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
- Tiền tệ (Giá bán, Giá vốn, Tổng tiền): Luôn dùng `decimal(18,0)` (Do VNĐ không có số lẻ thập phân).
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

### 2.2 Quản lý Kho (Inventory)

**Bảng `StockReceipts`** (Phiếu nhập kho)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `ReceiptId` | `int` | `int` | PK |
| `UserId` | `int` | `int` | FK -> Users (Người tạo phiếu) |
| `TotalAmount` | `decimal` | `decimal(18,0)` | Bắt buộc |
| `Note` | `string?` | `nvarchar(max)` | |

**Bảng `StockReceiptDetails`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `DetailId` | `int` | `int` | PK |
| `ReceiptId` | `int` | `int` | FK -> StockReceipts |
| `BookId` | `int` | `int` | FK -> Books |
| `Quantity` | `int` | `int` | Bắt buộc, `> 0` |
| `CostPrice` | `decimal` | `decimal(18,0)` | Giá nhập thực tế tại thời điểm này |

**Bảng `StockHistories`** (Sổ cái thẻ kho - mọi biến động đều ghi)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `HistoryId` | `int` | `int` | PK |
| `BookId` | `int` | `int` | FK -> Books |
| `ChangeType` | `StockChangeType`| `int` | Enum: Import(1), Sale(2), Adjustment(3), Cancel(4) |
| `QuantityChanged`| `int` | `int` | Dương (nhập/hủy đơn) hoặc Âm (bán hàng) |
| `ReferenceId` | `int?` | `int` | Chứa `OrderId` hoặc `ReceiptId` tùy ChangeType |
| `Note` | `string?` | `nvarchar(max)` | |

---

### 2.3 Quản lý Khách hàng & Thành viên

**Bảng `Members`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `MemberId` | `int` | `int` | PK |
| `PhoneNumber` | `string` | `varchar(20)` | UNIQUE Index. Bắt buộc |
| `FullName` | `string` | `nvarchar(255)` | Bắt buộc |
| `Points` | `int` | `int` | Số điểm hiện tại. Mặc định `0` |

**Bảng `PointTransactions`** (Lịch sử điểm)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `PointTransactionId`| `int`| `int` | PK |
| `MemberId` | `int` | `int` | FK -> Members |
| `OrderId` | `int?` | `int` | FK -> Orders |
| `Type` | `PointTransactionType`| `int` | Enum: Earned(1), Redeemed(2) |
| `Points` | `int` | `int` | Số điểm cộng (Earned) hoặc trừ (Redeemed) |
| `Description` | `string?` | `nvarchar(255)` | Bắt buộc |

---

### 2.4 Quản lý Đơn hàng & Thanh toán

**Bảng `Orders`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `OrderId` | `int` | `int` | PK |
| `OrderCode` | `string` | `varchar(50)` | UNIQUE. Generate: `ORD-yyyyMMdd-XXXX` |
| `SaleChannel` | `SaleChannel`| `int` | Enum: Kiosk(1), Counter(2) |
| `OrderStatus` | `OrderStatus`| `int` | Enum: Pending, Paid, Cancelled, NeedsReview |
| `PaymentMethod`| `PaymentMethod`| `int`| Enum: Cash(1), QR(2) |
| `MemberId` | `int?` | `int` | FK -> Members |
| `SubTotal` | `decimal` | `decimal(18,0)` | Tổng tiền hàng |
| `DiscountAmount`| `decimal` | `decimal(18,0)` | Tổng tiền giảm từ KM (Promotion) |
| `PointsUsed` | `int` | `int` | Số điểm đã dùng quy đổi |
| `TotalAmount` | `decimal` | `decimal(18,0)` | Thực trả = Sub - Discount - (Points * 1000) |
| `CompletedAt` | `DateTime?` | `datetime2` | Nullable. Cập nhật khi Paid |

**Bảng `OrderDetails`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `OrderDetailId` | `int` | `int` | PK |
| `OrderId` | `int` | `int` | FK -> Orders |
| `BookId` | `int` | `int` | FK -> Books |
| `UnitPriceAtTime`| `decimal` | `decimal(18,0)` | Giá bán tại thời điểm mua |
| `Quantity` | `int` | `int` | Bắt buộc |

**Bảng `PaymentTransactions`** (Lưu lịch sử webhook)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `TransactionId` | `int` | `int` | PK |
| `OrderId` | `int` | `int` | FK -> Orders |
| `ReferenceCode` | `string` | `varchar(50)` | Từ SePay gửi sang. UNIQUE Index (Idempotent) |
| `Amount` | `decimal` | `decimal(18,0)` | Tiền khách thực tế chuyển |
| `Gateway` | `string` | `varchar(50)` | "SePay" |

---

### 2.5 Khuyến Mãi (Promotions)

**Bảng `Promotions`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `PromotionId` | `int` | `int` | PK |
| `Name` | `string` | `nvarchar(255)` | VD: "Giảm 50K cho đơn từ 500K" |
| `Description` | `string?` | `nvarchar(max)` | |
| `DiscountAmount`| `decimal` | `decimal(18,0)` | Số tiền VNĐ được giảm cố định (VD: 50000) |
| `MinOrderValue`| `decimal` | `decimal(18,0)` | Đơn tối thiểu để áp dụng. Bằng 0 nếu áp dụng cho mọi đơn |
| `StartDate` | `DateTime` | `datetime2` | Bắt buộc |
| `EndDate` | `DateTime` | `datetime2` | Bắt buộc |
| `IsActive` | `bool` | `bit` | Bắt buộc |

**Bảng `PromotionUsages`**
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `UsageId` | `int` | `int` | PK |
| `PromotionId` | `int` | `int` | FK -> Promotions |
| `OrderId` | `int` | `int` | FK -> Orders |
| `DiscountAmountApplied`| `decimal`| `decimal(18,0)`| Số tiền thực sự được giảm cho đơn này |

---

### 2.6 Hệ thống & Thiết bị

**Bảng `Users`** (Web Admin)
| Cột | Kiểu C# | Kiểu SQL | Ràng buộc / Ghi chú |
|---|---|---|---|
| `UserId` | `int` | `int` | PK |
| `Username` | `string` | `varchar(100)` | UNIQUE Index |
| `PasswordHash` | `string` | `nvarchar(max)` | BCrypt |
| `FullName` | `string` | `nvarchar(255)` | Bắt buộc |
| `Role` | `UserRole`| `int` | Enum: Admin(1), Staff(2) |
| `IsActive` | `bool` | `bit` | Mặc định true |

**Bảng `RefreshTokens`**
*(Chứa token phân giải phiên đăng nhập của User)*
(Cột: TokenId, UserId (FK), Token, ExpiresAt, IsRevoked)

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

## 3. Các Index và Constraints Bắt Buộc

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

## 4. Giải thích cơ chế: Giữ chỗ kho (Reserved)

Để chống Race Condition (nhiều Kiosk cùng thanh toán 1 cuốn sách cuối cùng).
Khi Kiosk bấm "Thanh toán":
1. Tìm sách. Check `(StockQuantity - ReservedQuantity) >= Quantity Yêu cầu`
2. Đủ kho -> `ReservedQuantity += Quantity Yêu cầu`. Lưu Order (Pending).
3. Đếm ngược 3 phút cho thanh toán SePay.
   - Trạng thái 1 (Thành công): SePay webhook gọi về. `StockQuantity -= Yêu cầu`, `ReservedQuantity -= Yêu cầu`.
   - Trạng thái 2 (Thất bại / Hết giờ): Background job chạy mỗi phút, quét Order `Pending` > 3 phút. Đổi thành `Cancelled`, `ReservedQuantity -= Yêu cầu`.

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
