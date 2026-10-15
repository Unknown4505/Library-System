# BÁO CÁO KẾT QUẢ SỬA LỖI BACKEND SAU RUNTIME TEST CỦA LEADER
## KHẮC PHỤC TRIỆT ĐỂ CÁC LỖI P0 / P1 / P2 (B03, F01, D01, D03, B06, A04) + AN TOÀN TEST RUNNER

> **Người thực hiện:** `@thien33`  
> **Nhánh thực hiện:** `thien`  
> **Căn cứ:** Kết quả kiểm thử độc lập của Leader (`@Unknown4505`) trên .NET 10 và SQL Server Express  
> **Commit cuối:** `1576aaf` (fix scratch/EntityGen safety + CS8629)  
> **Liên kết Issue:** Closes #19 (https://github.com/Unknown4505/Library-System/issues/19)  
> **Trạng thái:** ✅ Hoàn thành — 18/18 PASS — Build 0 Errors 0 CS Warnings — **CHƯA PUSH LÊN REMOTE**

---

## 1. GHI NHẬN KẾT QUẢ RUNTIME ĐÃ PASS CỦA LEADER (GIỮ NGUYÊN)

- ✅ **Atomic reserve chống overselling:** 20/20 PASS
- ✅ **Duplicate webhook idempotency:** 20/20 PASS
- ✅ **Paid vs Cancel race:** 20/20 PASS
- ✅ **F01 concurrency 30-round (SQL Server Express, QA worktree):** 30/30 PASS
- ✅ **Paid vs Cancel vs Cleanup:** 20/20 PASS
- ✅ **Late webhook sau Cancelled:** PASS
- ✅ **OrderCode theo OrderId:** PASS
- ✅ **Điểm thưởng, hoàn điểm, kết hợp promotion:** PASS
- ✅ **6/6 nhóm lỗi (B03, F01, D01, D03, B06, A04) được Leader nghiệm thu**

---

## 2. CHI TIẾT CÁC LỖI ĐÃ KHẮC PHỤC

| Bug ID | Mức độ | Giải pháp |
|---|---|---|
| **B03 / BE-NEW-10** | P0 | Bắt buộc `transferType == "in"`, có account nhận, đối chiếu cấu hình |
| **F01 / F3 Race** | P0 | `GetOrderByCodeNoTrackingAsync` (AsNoTracking) sau rollback; trả `(false,"Conflict")`; HTTP 409 |
| **D01 / Point Input** | P1 | Validate `[0,100]` và bắt buộc MemberId; `ArgumentException` → HTTP 400 |
| **D03 / Point Race** | P1 | Bỏ `Math.Min`; `TryDeductPointsAsync` nguyên tử; `InvalidOperationException` khi không đủ điểm |
| **B06 / New Reference** | P1 | Lưu `PaymentTransaction` đối soát khi Paid nhận ReferenceCode mới |
| **A04 / Duplicate BookId** | P2 | Validate duplicate trước transaction; `ArgumentException` → HTTP 400 |
| **Safety / EntityGen** | ⚠️ | Khôi phục về SQLite runner; loại bỏ SQL Server RemoveRange; loại bỏ fallback connection string |
| **CS8629 Warning** | 🔧 | Fix nullable warning tại `ReportRepository.GetRevenueByMonthAsync` |

---

## 3. DANH SÁCH FILE ĐÃ THAY ĐỔI

| File | Thay đổi |
|---|---|
| `BookKiosk.Application/Services/PaymentService.cs` | B03, F01 (`GetOrderByCodeNoTrackingAsync`), B06 |
| `BookKiosk.API/Controllers/PaymentsController.cs` | F01 (HTTP 409 Conflict) |
| `BookKiosk.Application/Services/OrderService.cs` | D01, D03, A04 |
| `BookKiosk.API/Controllers/OrdersController.cs` | Bắt `ArgumentException` → HTTP 400 |
| `BookKiosk.Application/Interfaces/Repositories/IRepositories.cs` | Thêm `GetOrderByCodeNoTrackingAsync` |
| `BookKiosk.Infrastructure/Repositories/Repositories.cs` | Implement `GetOrderByCodeNoTrackingAsync`; fix CS8629 |
| `scratch/EntityGen/Program.cs` | Khôi phục SQLite runner; giữ F01.3 race sim |
| `scratch/EntityGen/EntityGen.csproj` | Xóa `Microsoft.AspNetCore.TestHost` |

---

## 4. KẾT QUẢ BIÊN DỊCH RELEASE

```
Build succeeded.
    0 Error(s)
    0 C# Compiler Warning(s)
    (Còn 19 NuGet vulnerability warnings cho AutoMapper 13.0.1 và System.Security.Cryptography.Pkcs
     — đã có từ trước, không thuộc phạm vi PR này)
```

---

## 5. BẢNG KẾT QUẢ TEST REGRESSION TỰ ĐỘNG (SQLite)

> **Provider:** SQLite in-process (`test_regression.db`) — cô lập hoàn toàn với SQL Server thật  
> **Commit:** `1576aaf` — **18/18 PASS, 0 FAIL**

| Test ID | Kịch bản | Kết quả |
|---|---|---|
| B03.1 | Webhook `transferType = null` | ✅ PASS |
| B03.2 | Webhook `transferType = "out"` | ✅ PASS |
| B03.3 | Thiếu `accountNumber` và `subAccount` | ✅ PASS |
| B03.4 | `accountNumber` không khớp cấu hình | ✅ PASS |
| F01.1 | `SimulateDevSuccessAsync` trên đơn đã `Cancelled` → `(false,"Conflict")` | ✅ PASS |
| F01.2 | `DevSimulateSuccess` endpoint → HTTP 409 Conflict | ✅ PASS |
| F01.3-setup | Cancel transitions `Pending→Cancelled` qua DbContext riêng biệt | ✅ PASS |
| F01.3 | F3 sau khi Cancel thắng: reload `AsNoTracking` → trả `(false,"Conflict")` | ✅ PASS |
| F01.3 | `Order.OrderStatus` trả về đúng `Cancelled` (không bị EF cache che) | ✅ PASS |
| D01.1 | `PointsToUse = -1` → `ArgumentException` | ✅ PASS |
| D01.2 | `PointsToUse = 101` → `ArgumentException` | ✅ PASS |
| D01.3 | `PointsToUse > 0`, không có `MemberId` → `ArgumentException` | ✅ PASS |
| D03.1 | Checkout 1 dùng 100 điểm thành công | ✅ PASS |
| D03.2 | Checkout 2 xin 100 điểm khi chỉ còn 50 → `InvalidOperationException` | ✅ PASS |
| B06.1 | Webhook 1 cho đơn đã `Paid` | ✅ PASS |
| B06.2 | Webhook 2 `ReferenceCode` mới cho đơn đã `Paid` | ✅ PASS |
| B06.3 | Cả 2 giao dịch lưu DB; không trừ kho lặp | ✅ PASS |
| A04 | `Items` chứa `BookId` trùng → `ArgumentException` | ✅ PASS |

**TOTAL: 18 PASSED / 0 FAILED**

---

## 6. XỬ LÝ AN TOÀN scratch/EntityGen (Yêu cầu từ review Leader)

### Nguy cơ được chỉ ra và đã xử lý

| # | Nguy cơ | Trạng thái |
|---|---|---|
| N1 | Fallback connection string hardcode `Database=BookKiosk_Test` | ✅ **Đã xóa** |
| N2 | `RemoveRange` 7 bảng không có guard → xóa DB nhầm | ✅ **Đã xóa** |
| N3 | `Microsoft.AspNetCore.TestHost` không cần trong scratch | ✅ **Đã xóa** |
| N4 | `ConcurrencyHookOrderRepository` + 30-round SQL Server loop trong PR source | ✅ **Đã xóa** |

### Phương án áp dụng
**Phương án 1 (ưu tiên của Leader):** Khôi phục SQLite in-process runner.  
- Logic concurrency 30-round SQL Server thuộc bộ QA worktree riêng của Leader (`D:\BookKiosk_Tests`), không nằm trong source PR.  
- Giữ lại test F01.3 race simulation đơn giản trên SQLite (2 DbContext scope, không kết nối SQL Server thật).

---

## 7. ĐÁNH GIÁ CHẤT LƯỢNG CÔNG VIỆC

| Tiêu chí | Đánh giá |
|---|---|
| **Tính toàn vẹn** | 6/6 bug groups đã PASS của Leader được bảo toàn nguyên vẹn |
| **An toàn dữ liệu** | Loại bỏ hoàn toàn nguy cơ xóa DB nhầm của test runner |
| **Mã nguồn sạch** | 0 lỗi biên dịch, 0 CS compiler warnings |
| **Không can thiệp FE/WPF** | Không có thay đổi nào trong `BookKiosk.Kiosk`, `BookKiosk.CMS` |
| **Test coverage** | 18/18 kịch bản tự động PASS trên SQLite |
| **Tài liệu** | Báo cáo cập nhật đầy đủ kết quả và giải thích kỹ thuật |

> **Cam kết:** Mã nguồn hoàn thiện tại nhánh `thien`, commit `1576aaf`.  
> **Chưa push** lên remote — sẵn sàng push ngay khi có lệnh duyệt.
