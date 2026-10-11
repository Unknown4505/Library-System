# BÁO CÁO KẾT QUẢ SỬA LỖI BACKEND (ĐỢT SỬA LỖI THEO REVIEW PR #26)

> **Người thực hiện:** `@thien33`  
> **Nhánh thực hiện:** `thien`  
> **Cơ sở:** Sửa lỗi theo phản hồi của Leader (`@Unknown4505`) trên PR #26 và đồng bộ PR [#11](https://github.com/Unknown4505/Library-System/pull/11) của Khang  
> **Liên kết Issue:** Closes #19 (https://github.com/Unknown4505/Library-System/issues/19)  
> **Trạng thái:** Đã hoàn tất sửa lỗi tại local, Build Release thành công **0 Lỗi**, sẵn sàng nghiệm thu  

---

## 1. DANH SÁCH BUG ID ĐÃ XỬ LÝ TRONG LẦN NÀY

| Bug ID | Mức độ | Mô tả tóm tắt | Giải pháp kỹ thuật đã áp dụng |
|---|---|---|---|
| **BE-NEW-10** | **P0** | Validate giao dịch tiền vào & đối chiếu tài khoản nhận | - Chỉ xác nhận thanh toán khi `transferType == "in"`. Giao dịch tiền ra (`"out"`) bị từ chối chuyển `Paid`.<br>- Đối chiếu số tài khoản thụ hưởng `accountNumber` và `subAccount` với cấu hình trong `appsettings.json`. Khác tài khoản thụ hưởng sẽ bị từ chối chuyển `Paid`. |
| **BE-NEW-11** | **P0** | Xử lý DbUpdateException an toàn (không nuốt lỗi DB) | - Triệt tiêu việc bắt chuỗi `DbUpdateException` chung chung.<br>- Sau khi rollback, query lại DB bằng `CheckTransactionExistsAsync(refCode)`: Chỉ trả `true` (Idempotent) nếu transaction thực sự đã tồn tại trong DB do tiến trình song song commit thành công.<br>- Nếu chưa tồn tại trong DB: Đây là lỗi DB thật sự (FK, Check Constraint, dữ liệu sai định dạng) ➔ `throw;` lại lỗi, tuyệt đối không nuốt lỗi. |
| **BE-NEW-12** | **P0** | Webhook thanh toán đến sau khi Order đã Cancelled | - Bổ sung `GetOrderByCodeAsync` để tra cứu đơn hàng ở mọi trạng thái.<br>- Nếu đơn đã `Cancelled`: Vẫn lưu `PaymentTransaction` vào DB để lưu vết đối soát cho nhân viên kế toán hoàn tiền/tạo đơn bù.<br>- Không chuyển lại thành `Paid`, không trừ kho thật, không nhả kho lần 2 và không đụng điểm thành viên. Trả 200 cho SePay để tránh spam retry. |
| **BE-NEW-13** | **P1** | Concurrent member points & sửa vi phạm Check Constraint | - Sửa lỗi vi phạm `CONSTRAINT [CK_PointTransactions_Points] CHECK ([Points] > 0)`: Luôn lưu giá trị `Points` dương (> 0) khi lưu giao dịch loại `Redeemed`.<br>- Triển khai mô hình Hold & Release nguyên tử tại DB:<br>  + Lúc Checkout: Trừ giữ điểm nguyên tử bằng `TryDeductPointsAsync` (`UPDATE Members SET Points = Points - @used WHERE MemberId = @id AND Points >= @used`). Không đủ điểm sẽ bị chặn ngay lúc checkout, triệt tiêu race condition 2 đơn cùng xài chung 1 số điểm.<br>  + Lúc Cancel (Kiosk cancel hoặc Expired cleanup): Hoàn lại điểm nguyên tử qua `AddPointsAsync` và ghi nhận `PointTransaction` hoàn điểm.<br>  + Lúc Payment: Điểm đã giữ an toàn lúc checkout, chỉ cộng thêm điểm thưởng tích lũy mới (`TotalAmount / 10000`). |
| **BE-NEW-14** | **P1** | Tuyệt đối duy nhất cho OrderCode (chống collision 100%) | - Đồng bộ 100% định dạng `docs/03-database.md` (§3.2, dòng 140): `ORD-yyyyMMdd-XXXX` (trong đó `XXXX` là `OrderId` toàn hệ thống padding 4 chữ số).<br>- Sinh mã dựa trên khóa chính `OrderId` tự tăng (Identity) của SQL Server, bảo đảm tính duy nhất tuyệt đối về mặt toán học (100% không thể trùng lặp). |
| **ENDPOINT F3** | **Dev** | Endpoint F3 Kiosk Dev Simulate Success | - Triển khai `POST /api/payments/dev-simulate-success/{orderCode}` theo đúng `docs/04-api-reference.md` (§3.4) cho FE của Khang (PR #11):<br>  + Chỉ bật trong Development (`IHostEnvironment.IsDevelopment()`), Production trả `404`.<br>  + Bắt buộc xác thực qua header `X-Api-Key` của Kiosk.<br>  + Dùng chung luồng hoàn tất giao dịch an toàn với Webhook thật: chuyển `Paid` nguyên tử, trừ kho thật, tích điểm thưởng, trả về `ApiResponseDto<PaymentStatusDto>`.<br>  + Đơn không tồn tại trả `404`; Đơn đã hủy trả `409 Conflict`; Đơn đã thanh toán trả `200 OK`. |

---

## 2. DANH SÁCH FILE THAY ĐỔI

```text
BookKiosk.API/Controllers/PaymentsController.cs
BookKiosk.API/HostedServices/ExpiredOrderCleanupService.cs
BookKiosk.API/appsettings.Development.json
BookKiosk.Application/Interfaces/Repositories/IRepositories.cs
BookKiosk.Application/Services/OrderService.cs
BookKiosk.Application/Services/PaymentService.cs
BookKiosk.Infrastructure/Repositories/Repositories.cs
docs/report-kiem-tra-sua-loi-backend.md
docs/thong-nhat-leader-va-team.md
```

*(Cam kết: Không sửa đổi bất kỳ file nào thuộc `BookKiosk.Kiosk/**`, `BookKiosk.CMS/**` hoặc file FE của Khang).*

---

## 3. LỆNH BUILD & KẾT QUẢ KIỂM THỬ (TEST COMMAND & OUTPUT)

### Lệnh biên dịch Release:
```powershell
& "C:\Users\pnt19\AppData\Local\Microsoft\dotnet\dotnet.exe" build BookKiosk.API/BookKiosk.API.csproj -c Release
```

### Kết quả Output thực tế:
```text
  Determining projects to restore...
  All projects are up-to-date for restore.
  BookKiosk.Domain -> D:\Downloads\CNPM\Library-System\BookKiosk.Domain\bin\Release\net10.0\BookKiosk.Domain.dll
  BookKiosk.Application -> D:\Downloads\CNPM\Library-System\BookKiosk.Application\bin\Release\net10.0\BookKiosk.Application.dll
  BookKiosk.Infrastructure -> D:\Downloads\CNPM\Library-System\BookKiosk.Infrastructure\bin\Release\net10.0\BookKiosk.Infrastructure.dll
  BookKiosk.API -> D:\Downloads\CNPM\Library-System\BookKiosk.API\bin\Release\net10.0\BookKiosk.API.dll

Build succeeded.
    11 Warning(s)  (chỉ có cảnh báo AutoMapper cũ từ main; code mới không phát sinh warning nào)
    0 Error(s)

Time Elapsed 00:00:11.02
```

---

## 4. BẢNG TRẠNG THÁI NGHIỆM THU (PASS / FAIL / NOT RUNTIME TESTED)

- **Database Provider:** SQL Server (`Microsoft.EntityFrameworkCore.SqlServer` — Target: `Server=.\\SQLEXPRESS02;Database=BookKiosk_Dev;...`).

| Test ID / Kịch bản kiểm tra | Môi trường / Provider | Trạng thái | Chi tiết / Bằng chứng |
|---|---|---|---|
| **Build & Compilation** | .NET 10 (Release) | **PASS** | Biên dịch thành công 0 lỗi, 11 cảnh báo cũ. |
| **BE-NEW-10: Inbound Transfer & Account Check** | Code Logic / Security Filter | **PASS (Static Verified)** | Chỉ nhận `transferType == "in"`, kiểm tra `accountNumber` cấu hình. |
| **BE-NEW-11: Safe DbUpdateException** | Code Logic / Repository Query | **PASS (Static Verified)** | Kiểm tra tồn tại trong DB sau rollback; không nuốt lỗi DB thật. |
| **BE-NEW-12: Late Webhook after Cancelled** | Code Logic / Audit Log | **PASS (Static Verified)** | Lưu `PaymentTransaction`, giữ `Cancelled`, không trừ kho hay điểm. |
| **BE-NEW-13: Concurrent Member Points & Positive Points** | Code Logic / Atomic DB Update | **PASS (Static Verified)** | Hold điểm lúc checkout, hoàn điểm lúc cancel, Points > 0. |
| **BE-NEW-14: Absolute Unique OrderCode** | Code Logic / DB Identity | **PASS (Static Verified)** | Sinh mã theo `OrderId:D4` (`ORD-yyyyMMdd-XXXX`), 100% không trùng. |
| **ENDPOINT F3: Kiosk Dev Simulation** | Controller & Service | **PASS (Static Verified)** | Dev-only, `X-Api-Key`, hoàn tất giao dịch an toàn, trả `200 Paid`. |
| **Concurrency Stress Test trên SQL Server thật** | SQL Server Integration DB | **NOT RUNTIME TESTED** | Chưa chạy trên môi trường test SQL Server riêng biệt theo `docs/11` (chờ Leader kích hoạt DB test riêng). |
| **SePay Sandbox Live Webhook** | SePay Sandbox / Tunnel Ngrok | **NOT RUNTIME TESTED** | Cần tunnel Ngrok và webhook dashboard SePay sandbox để kích hoạt từ bên ngoài. |

> [!NOTE]
> Tuân thủ nghiêm ngặt quy định: **Không tự nhận Task 5.5 hay E2E PASS khi chưa có bằng chứng runtime log trên SQL Server test và SePay sandbox thật.** Các kịch bản concurrency phức tạp và sandbox webhook được ghi rõ ràng là `NOT RUNTIME TESTED`.
