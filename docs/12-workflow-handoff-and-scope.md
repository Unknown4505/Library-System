# Workflow handoff và phạm vi current/target — BookKiosk

Ngày đối chiếu integration/schema: 2026-10-16, baseline `origin/main@efcd388a2c5c6791eeecefe6e511d6b31ec8a91f`, branch `codex/leader-workflow-integration`. Trạng thái merge của Kiosk Receipt/Advertising/Navigation được đồng bộ ngày 2026-10-24 theo `origin/main@9a55b09760ad59aa2e7712e8ffbbc9016d235664`; baseline schema lịch sử ở trên không bị thay thế. Dependency audit ngày 2026-11-04 đối chiếu GitHub và source tại `main@e81693861893ce03a1c14a1a78a78e7cdca1ddd6` cho Issues #60/#61/#66 và PR #64; Issue #65 được Leader đóng `not_planned` vì CMS Kiosk Monitoring ngoài phạm vi bắt buộc. QA acceptance được đồng bộ ngày 2026-11-05: PR #68 exact HEAD `829a46b27327d3dceb98c93b521cd4779bb65fbd`, Release build 0 errors và targeted HTTP/JWT/SQL **19/19 PASS**; trạng thái vẫn **BACKEND QA PASS / PR OPEN / PENDING LEADER MERGE GATE**.

## 1. Quyết định đã chốt

| Chủ đề | Current source | Target | Verification |
|---|---|---|---|
| Loại sách | `Categories` | Giữ nguyên; không có `BookTypes` | PASS static |
| Hóa đơn | Paid `Orders` + `OrderDetails` | Receipt API/UI #28/#29; không có `Invoices` | IMPLEMENTED / MERGED / TARGETED QA VERIFIED; UI/E2E NOT RUN |
| Customer/account | `Members` tách khỏi `Users` | Giữ nguyên | PASS static |
| Role | `Admin=1`, `Staff=2` ở baseline | Thêm `Manager=3`, không đổi số cũ | PASS source; chưa runtime DB |
| Schema | Main baseline 17 bảng | 19 bảng sau migration: thêm Employees/SalaryPayments | Model + migration scaffold/static review PASS; runtime SQL chưa chạy |
| Lương | Chưa có | Sổ tháng Draft/Paid đơn giản | PLANNED API/UI |
| Device incidents | Kiosks/KioskIncidents là bảng thật | Không là process BFD/DFD chính | PASS design |
| Idle Advertising | Backend #37 đã merge; FE #38 ở PR #54 HEAD `9431b33` | Slideshow 3 nhóm, touch-to-start, 60s Search inactivity và fallback | IMPLEMENTED / TARGETED QA VERIFIED; FINAL WPF/E2E BLOCKED |
| Post-payment navigation | Receipt UI/PDF #29 đã merge; điều hướng mới thuộc FE #38 | `ReceiptPage → SearchPage`; giữ Receipt 5s và clear completed session | IMPLEMENTED / TARGETED QA VERIFIED; REAL PAID E2E BLOCKED |
| Revenue monthly correctness | Revenue API hiện hữu | `REVENUE-MONTHLY-01`: unique/range/order từ BE; CMS validate/normalize/reject atomically | APPROVED / FE INDEPENDENT TARGETED QA PASS @ `ea010e34` / REAL E2E PENDING |
| Revenue financial invariant | Checkout reject payable `<= 0`; report cộng Paid `TotalAmount` | `REVENUE-NEGATIVE-01`: Paid amount `> 0`, monthly Revenue/TotalOrders không âm | **APPROVED / FE INDEPENDENT TARGETED QA PASS @ `ea010e34` / BE HARDENING #71 PENDING** |

Nguồn ưu tiên: Entity/EF config/migration snapshot → `docs/init.sql` và drift → Service/Controller/DTO/docs → quyết định Leader. BFD không phải nguồn schema chuẩn.

## 2. Schema target và DDL dự kiến

`docs/init.sql` hiện là fresh-bootstrap Development độc lập cho 19 bảng và không mirror chính xác EF migration chain. 17 bảng lịch sử trong EF vẫn có 11 cột `decimal(18,2)`, còn bootstrap giữ contract `decimal(18,0)` và 23 `DEFAULT`. Migration/snapshot HR thêm 4 `CHECK`; bootstrap hiện có tổng 10 `CHECK`. Không chạy bootstrap trước EF migrations trên cùng database, không dùng nó để upgrade/repair database hiện hữu và không xem các guard `IF NOT EXISTS` là bảo đảm an toàn chạy lặp trên mọi schema. Database runtime chưa được truy vấn.

DDL dưới đây khớp migration `20261016071312_AddEmployeeSalaryLedger` đã scaffold và static-review. DDL HR có guard tránh tạo lại table/index đã được đưa vào `docs/init.sql`, nhưng guard không xác minh/repair toàn bộ schema hoặc EF migration history. Chưa apply migration hoặc script lên bất kỳ database nào. Runtime SQL vẫn cần database `BookKiosk_QA_<run-id>` disposable đã xác minh.

```sql
CREATE TABLE [dbo].[Employees] (
    [EmployeeId] INT IDENTITY(1,1) NOT NULL,
    [EmployeeCode] VARCHAR(50) NOT NULL,
    [UserId] INT NULL,
    [FullName] NVARCHAR(255) NOT NULL,
    [PhoneNumber] VARCHAR(20) NULL,
    [Email] VARCHAR(255) NULL,
    [Position] NVARCHAR(100) NOT NULL,
    [HireDate] DATETIME2 NULL,
    [CurrentBaseSalary] DECIMAL(18,0) NULL,
    [IsActive] BIT NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([EmployeeId]),
    CONSTRAINT [FK_Employees_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
    CONSTRAINT [CK_Employees_CurrentBaseSalary] CHECK ([CurrentBaseSalary] IS NULL OR [CurrentBaseSalary] >= 0)
);
CREATE UNIQUE INDEX [IX_Employees_EmployeeCode] ON [dbo].[Employees] ([EmployeeCode]);
CREATE UNIQUE INDEX [IX_Employees_UserId] ON [dbo].[Employees] ([UserId]) WHERE [UserId] IS NOT NULL;

CREATE TABLE [dbo].[SalaryPayments] (
    [SalaryPaymentId] INT IDENTITY(1,1) NOT NULL,
    [EmployeeId] INT NOT NULL,
    [PayMonth] INT NOT NULL,
    [PayYear] INT NOT NULL,
    [BaseSalarySnapshot] DECIMAL(18,0) NOT NULL,
    [AllowanceAmount] DECIMAL(18,0) NOT NULL,
    [DeductionAmount] DECIMAL(18,0) NOT NULL,
    [NetSalary] DECIMAL(18,0) NOT NULL,
    [Status] INT NOT NULL,
    [PaidByUserId] INT NULL,
    [PaidAt] DATETIME2 NULL,
    [Note] NVARCHAR(1000) NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    [UpdatedAt] DATETIME2 NULL,
    CONSTRAINT [PK_SalaryPayments] PRIMARY KEY ([SalaryPaymentId]),
    CONSTRAINT [FK_SalaryPayments_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [dbo].[Employees] ([EmployeeId]) ON DELETE NO ACTION,
    CONSTRAINT [FK_SalaryPayments_Users_PaidByUserId] FOREIGN KEY ([PaidByUserId]) REFERENCES [dbo].[Users] ([UserId]) ON DELETE NO ACTION,
    CONSTRAINT [CK_SalaryPayments_PayMonth] CHECK ([PayMonth] BETWEEN 1 AND 12),
    CONSTRAINT [CK_SalaryPayments_Amounts] CHECK ([BaseSalarySnapshot] >= 0 AND [AllowanceAmount] >= 0 AND [DeductionAmount] >= 0 AND [NetSalary] >= 0),
    CONSTRAINT [CK_SalaryPayments_Status] CHECK ([Status] IN (1, 2))
);
CREATE UNIQUE INDEX [IX_SalaryPayments_EmployeeId_PayYear_PayMonth]
    ON [dbo].[SalaryPayments] ([EmployeeId], [PayYear], [PayMonth]);
CREATE INDEX [IX_SalaryPayments_PaidByUserId] ON [dbo].[SalaryPayments] ([PaidByUserId]);
```

Không thay precision 11 cột cũ, không seed lương, không sửa migration lịch sử, không chạy trên database hiện hữu.

## 3. Ma trận role target

| Chức năng | Admin | Manager | Staff | Trạng thái |
|---|:---:|:---:|:---:|---|
| Quản lý tài khoản | Read ở PR #68; write theo USER-MGMT-BE02-01 sau khi implement | Không | Không | Issue #66: BE-01/BE-03 targeted QA PASS ở open PR #68; BE-02 APPROVED DESIGN / NOT IMPLEMENTED |
| Hồ sơ nhân viên | R/W | Theo policy | Self only | PLANNED |
| Sổ lương | Create/Pay/Read all | Own Paid only; không pay | Own Paid only | APPROVED DESIGN; API/UI PLANNED |
| Reports | Yes | No | No | Contract hiện tại Admin-only |
| Kiosk receipt | API key thiết bị | API key thiết bị | API key thiết bị | #28/#29 MERGED / TARGETED QA VERIFIED; UI/E2E NOT RUN |

## 4. Traceability nghiệp vụ và sơ đồ

| Process | Datastore/entity | Source liên quan | Trạng thái |
|---|---|---|---|
| 2.0/2.4 Checkout | Books, Orders, OrderDetails | `OrderService`, checkout DTO | IMPLEMENTED; diagram phải thể hiện reserve |
| 2.6 Paid | Books, Orders, PaymentTransactions, Members, PointTransactions | `PaymentService`, PaymentsController | IMPLEMENTED core; E2E môi trường còn thiếu |
| 2.7 Receipt | Orders, OrderDetails, Books | Contract `ReceiptDto`, Issue #28/#29 | IMPLEMENTED / MERGED / TARGETED QA VERIFIED; UI/E2E NOT RUN |
| 2.8 Cancel/Timeout | Books, Orders | `OrderService`, cleanup service | IMPLEMENTED core; nhả reserved, không trừ stock |
| 3.0 Nhập kho | Suppliers, ImportReceipts, ImportReceiptDetails, Books | Import service/API | IMPLEMENTED source |
| 8.0 Reports | Orders/OrderDetails, Books | ReportsController/DTO | Revenue/Top Books IMPLEMENTED; FE monthly/negative/unavailable interaction independent targeted QA PASS tại `ea010e34`; BE hardening #71 và CMS runtime E2E pending |
| CMS Kiosk Monitoring | Kiosks, KioskIncidents | Heartbeat hiện hữu; Issue #65 | **OUT OF SCOPE / NOT REQUIRED**; #65 CLOSED — NOT PLANNED; POST heartbeat giữ nguyên |
| CMS Users | Users | AuthController hiện hữu; Issue #66, PR #68 | Login IMPLEMENTED; BE-01 read + BE-03 targeted auth/security QA PASS; PR OPEN / NOT MERGED; BE-02 APPROVED DESIGN / NOT IMPLEMENTED |
| HR target | Users, Employees, SalaryPayments | Domain + EF config + migration `20261016071312` | Model/migration SCAFFOLDED; API PLANNED; runtime SQL NOT TESTED |

Artefacts canonical: [`BFD.drawio`](../diagram/BFD.drawio), [`DFD_Level_0_Context.drawio`](../diagram/DFD_Level_0_Context.drawio), [`DFD_Level_1_Main.drawio`](../diagram/DFD_Level_1_Main.drawio), [`DFD_Level_2_P2_Invoice_Checkout.drawio`](../diagram/DFD_Level_2_P2_Invoice_Checkout.drawio), [`ERD_Current_and_Target_3_Pages.drawio`](../diagram/ERD_Current_and_Target_3_Pages.drawio) và [`BookKiosk_Class_Diagram_Current_Target.drawio`](../diagram/BookKiosk_Class_Diagram_Current_Target.drawio). Current phản ánh source; Target được phép chứa HR nhưng phải ghi `PLANNED`/runtime unknown. Bốn file legacy đã được loại khỏi worktree. Theo quyết định Leader ngày 2026-10-16, lượt này chỉ cập nhật `.drawio`; PNG hiện có không được ghi đè và sẽ được Leader vẽ/xuất lại sau.

## 5. Handoff Issue #28 — Thiện

Owner: `@thien33`. Task 5.6. Status: **[x] IMPLEMENTED / MERGED / TARGETED QA VERIFIED** — Receipt API ban đầu merge qua PR #35; snapshot/legacy mapping hoàn thiện trong PR #52.

- File ownership: OrdersController, OrderService/interface/repository và test BE liên quan; không đổi HR scaffold.
- Contract: `GET /api/orders/kiosk/{orderId}/receipt`, `X-Api-Key`, chỉ Paid + valid CompletedAt trả 200 `ApiResponseDto<ReceiptDto>` từ persisted `Orders`/`OrderDetails`.
- Acceptance: missing 404; Pending/Cancelled 409; repeated GET không side effect; không lộ CostPrice/phone/member PII; BookTitle tạm đọc qua `OrderDetails.Book`.
- Evidence đã xác nhận: targeted unit/HTTP tests 200/404/409/privacy/snapshot-idempotency và legacy compatibility PASS; SQL chỉ dùng QA disposable.

**Kết quả handoff:** Backend đã triển khai đúng contract `docs/04-api-reference.md`; không thêm `Invoices`, không dùng cart client để dựng receipt và targeted QA đã PASS trước khi merge.

## 6. Handoff Issue #29 — Khang

Owner: `@lehuukhang`. Task 8.2.1. Status: **IMPLEMENTED / MERGED / TARGETED QA VERIFIED — PR #40**. WPF visual/UI E2E vẫn **NOT RUN**.

- File ownership: Kiosk ReceiptPage/ViewModel, API client integration, Mock Printer và QuestPDF.
- Dùng real `OrderId` sau polling Paid và cùng `ReceiptDto` Backend; không tính lại từ cart local, không viết lại UI ngoài phạm vi.
- Acceptance: hiển thị/in/PDF nhất quán; 404/409/timeout/network xử lý an toàn; targeted ViewModel/PDF tests đã PASS. WPF runtime/UI E2E vẫn phải chạy riêng.
- Evidence hiện có chỉ chứng minh targeted behavior; ảnh UI/PDF runtime và full E2E chưa chạy, không được suy ra PASS từ mock.

## 7. Handoff bổ sung — assigned issues và approval gates

Issues [#60](https://github.com/Unknown4505/Library-System/issues/60), [#61](https://github.com/Unknown4505/Library-System/issues/61) và [#66](https://github.com/Unknown4505/Library-System/issues/66) đã được tạo và gán owner trên GitHub. #60 BE-01 và #66 BE-01/BE-03 đã **TARGETED BACKEND QA PASS** trong PR #68 nhưng PR còn OPEN. Ngày 08/11/2026 Leader duyệt #60 BE-02 theo quyết định mới `BOOK-BARCODE-BE02-01`; approval này mở direct-cutover implementation cho `@thien33` trong PR riêng và không xác nhận code/QA/merge. #66 BE-02 tiếp tục dùng policy riêng `USER-MGMT-BE02-01`; không được lấy approval Users làm bằng chứng cho Barcode. [Issue #65](https://github.com/Unknown4505/Library-System/issues/65) đã **CLOSED — NOT PLANNED** và không còn là handoff/dependency phải triển khai. Các hàng Employee/Salary/NCC chưa có issue riêng vẫn là proposal/approval-gated.

| Hạng mục | Owner / handoff | Dependencies | API/Data contract | Outputs | Acceptance criteria | Trạng thái |
|---|---|---|---|---|---|---|
| Extended Books Search + Controlled Barcode — [#60](https://github.com/Unknown4505/Library-System/issues/60) | BE `@thien33`; FE `@lehuukhang` tích hợp qua #61 | BE-01 ở PR #68; BE-02 phải PR riêng | BE-01 search 6 fields; BE-02 planned Admin-only direct cutover `POST /api/books/{id}/change-barcode`, giữ BookId, không alias/schema | BE-01 evidence; BE-02 implementation + contract/audit/test evidence | BE-01 targeted QA PASS; BE-02 validation/RBAC/unique/reservation/transaction/concurrency/rollback + Books/Inventory/Order/Payment regression | **BE-01 QA PASS / PR #68 OPEN; BE-02 APPROVED DESIGN / NOT STARTED / QA NOT TESTED** |
| Kiosk WPF — [#61](https://github.com/Unknown4505/Library-System/issues/61) | FE `@lehuukhang` | Camera/runtime isolation; FEAT-02 phụ thuộc #60 BE-01 | Dùng API/DTO Backend hiện hữu, không tự sửa BE/SQL | Camera/search/mock isolation phía Kiosk | Targeted + physical camera/WPF visual + real BE integration theo từng dependency | **OPEN / PARTIAL; CMS KHÔNG THUỘC SCOPE ISSUE** |
| CMS Kiosk Monitoring — [#65](https://github.com/Unknown4505/Library-System/issues/65) | Không giao triển khai | POST heartbeat và entity Kiosk có sẵn, giữ nguyên | Không yêu cầu GET list/summary hoặc monitoring contract mới | Không có output bắt buộc | Không thuộc acceptance/E2E bắt buộc của đồ án | **OUT OF SCOPE / CLOSED — NOT PLANNED** |
| Auth/User Management CMS — [#66](https://github.com/Unknown4505/Library-System/issues/66) | BE `@thien33`; FE `@lehuukhang` tích hợp | Login/JWT có sẵn; GET source trong open PR #68 | BE-01 GET list/detail; BE-02 Admin-only Create, Update FullName/Role, Activate/Deactivate, Reset Password; không đổi username/schema | Merge BE-01; BE-02 trong PR riêng | BE-01/BE-03: 401/403, DTO allowlist, 404, paging/overflow, GET read-only PASS; BE-02 vẫn cần unique/concurrency/safety tests | **BE-01 + BE-03 TARGETED QA PASS / PR #68 OPEN; BE-02 APPROVED DESIGN / NOT IMPLEMENTED** |
| Employee | BE `@thien33`; FE `@lehuukhang`; Leader sở hữu schema | Auth/RBAC PASS; migration HR được apply trên QA disposable | `Employee` DTO/policy, filtered unique UserId/EmployeeCode, disable thay delete | Employee API + CMS CRUD/self profile | 400/404/409 đúng; row authorization; không lộ PII; targeted API tests PASS | **PLANNED** |
| Salary ledger | BE `@thien33`; FE `@lehuukhang`; Leader giữ schema/shared contract | Auth + Employee PASS; SQL QA migration PASS | `SalaryPayment` Draft/Paid; PayYear 2000..2100; unique kỳ lương; decimal(18,0); công thức NetSalary; Admin-only pay; Manager/Staff own-Paid | Salary API + CMS draft/pay/read | Draft không actor/time; Draft→Paid atomically; Admin actor + UTC; concurrent pay chỉ thắng một; Paid immutable; JWT row ownership; không chuyển tiền ngân hàng | **APPROVED DESIGN; IMPLEMENTATION PLANNED** |
| NCC/Supplier CMS | BE `@thien33`; FE `@lehuukhang` | Supplier/Import contract hiện có; authorization CMS | `Suppliers`, `ImportReceipts`, DTO/validation và delete policy | Supplier CRUD/list + CMS integration | Không xóa làm mất lịch sử nhập; duplicate/invalid trả wrapper chuẩn; import regression targeted PASS | **PLANNED** |

#60/#61/#66 phải giữ đúng boundary và handoff evidence trong từng issue. `USER-MGMT-BE02-01` chỉ phê duyệt User mutation subset được ghi dưới đây; không mở rộng sang hard-delete, đổi username, schema mới hoặc tự xác nhận hoàn thành. #65 chỉ được giữ như dấu vết quyết định **OUT OF SCOPE / CLOSED — NOT PLANNED**, không phải task chờ triển khai. Các handoff còn lại cần issue/task riêng sau khi Leader duyệt, kèm file ownership và test evidence; không gộp vào #28/#29.

### 7.1 Salary enforcement contract cho member — APPROVED DESIGN

- **DB ENFORCED:** unique `(EmployeeId, PayYear, PayMonth)`; `PayMonth` 1..12; các khoản tiền và NetSalary không âm; status chỉ Draft/Paid; FK và delete `RESTRICT`.
- **APPLICATION REQUIRED — HR-DEC-01:** tạo/cập nhật Draft chỉ nhận `PayYear` 2000..2100, inclusive; ngoài khoảng trả validation error. Migration không có PayYear CHECK.
- **APPLICATION REQUIRED — HR-DEC-03 Draft:** xác định `BaseSalarySnapshot` hợp lệ, không đổi null thành 0; tiền không âm; tính `NetSalary = BaseSalarySnapshot + AllowanceAmount - DeductionAmount` và kết quả không âm; để `PaidByUserId`/`PaidAt` null. `HireDate` tiếp tục nullable.
- **APPLICATION REQUIRED — HR-DEC-03 Paid:** chỉ Admin chuyển `Draft → Paid`; lấy actor từ JWT và `PaidAt` UTC phía server; status/actor/time trong một transaction; concurrency chỉ cho một request thắng; không pay hai lần; không sửa/xóa Paid qua API thường.
- **APPLICATION REQUIRED — HR-DEC-02 Read:** Admin xem all Draft/Paid; Manager/Staff chỉ xem Paid của chính Employee khi JWT UserId khớp `Employees.UserId`; deny Draft, người khác và identity không có Employee. Không dùng UserId từ client. Manager không pay.
- **APPROVED DESIGN:** giữ nguyên migration `20261016071312_AddEmployeeSalaryLedger`, không thêm SQL CHECK trong V6.3. Salary là ledger ghi nhận thanh toán, không thực hiện chuyển tiền ngân hàng. Service/API chưa có nên vẫn **PLANNED**, không phải IMPLEMENTED.

### 7.2 User Management BE-02 — APPROVED DESIGN / READY TO IMPLEMENT

- **Authorization:** toàn bộ `/api/users` Admin-only; anonymous `401`, Manager/Staff `403`. Admin được tạo tài khoản role `Admin`, `Manager`, `Staff` theo enum hiện hữu.
- **Approved endpoints:** `POST /api/users`; `PUT /api/users/{id}` chỉ sửa `fullName`/`role`; `PATCH /api/users/{id}/status`; `POST /api/users/{id}/reset-password` nhận `newPassword`.
- **Account safety:** không đổi username, không hard-delete, không self-deactivate và không vô hiệu hóa Admin active cuối cùng. User inactive không đăng nhập được và có thể được kích hoạt lại.
- **Credentials:** username unique case-insensitive và concurrency-safe; password tối thiểu 8 ký tự, có chữ hoa/chữ thường/số, hash bằng cơ chế hiện hữu; không trả/log password hoặc `PasswordHash`.
- **Schema boundary:** không thêm `Email`, `CreatedAt`, `MustChangePassword`, audit table hoặc migration trong BE-02. FE phải bỏ/ẩn mock field không có trong entity.
- **Acceptance:** 400/401/403/404/409 đúng; tests cho duplicate/concurrency, self/last-admin protection, inactive login, reset password cũ thất bại và password mới thành công. Thiết kế đã duyệt nhưng implementation/E2E vẫn **NOT DONE**.

### 7.3 Controlled Barcode Modification — APPROVED DESIGN / READY TO IMPLEMENT

**Approval audit:** Issue #60 body do Leader tạo ghi BE-02 `HOLD / DO NOT IMPLEMENT YET`; Issue chưa có comment. PR #68 body của `@thien33` chỉ là đề xuất direct cutover so với Barcode Alias và tự giữ trạng thái HOLD. Comment Leader tại PR #68 ngày 05/11/2026 vẫn xác nhận Barcode HOLD. Git history commit `619a95b` cũng chỉ đồng bộ HOLD. Vì vậy không có approval cũ hợp lệ; `USER-MGMT-BE02-01` là quyết định cho Issue #66 Users và không áp dụng ở đây.

**Leader decision `BOOK-BARCODE-BE02-01` — effective 08/11/2026:** **APPROVED FOR IMPLEMENTATION** cho `@thien33`. Chọn direct cutover có kiểm soát vì `Books.Barcode` đã có unique index `varchar(50)`, còn OrderDetail, ImportReceiptDetail và ProductDiscount tham chiếu `BookId`; đổi Barcode không phá FK/lịch sử. Barcode Alias bị loại khỏi iteration vì cần schema/migration, làm phức tạp uniqueness giữa mã chính/mã cũ và không có yêu cầu vận hành đủ mạnh. Source hiện không có Borrowing/Loan module; module tương lai phải tham chiếu BookId và qua review riêng.

- **Planned contract:** `POST /api/books/{id:int}/change-barcode`, request `{ newBarcode, reason }`, shared `ApiResponseDto`; success trả BookId, old/new Barcode, `requiresLabelReprint=true`, UTC timestamp. Endpoint được ghi **PLANNED**, chưa tồn tại trong source.
- **Validation/RBAC:** Admin-only; barcode trim, `1..50`, varchar-compatible, không control character và không giả định EAN-13; reason trim `5..500`; mã mới phải khác mã hiện tại. `400/401/403/404/409` theo contract, không leak provider detail.
- **Integrity/concurrency:** giữ BookId; chặn khi `ReservedQuantity > 0`; kiểm tra và cập nhật trong transaction; conditional update theo BookId + old Barcode + reservation state; DB unique index là chốt cuối cho race cùng mã. Chỉ provider unique violation thật được map duplicate conflict.
- **Audit/rollback:** structured security audit sau commit gồm actor UserId, BookId, old/new, reason, UTC và correlation id, không log token. Rollback nghiệp vụ gọi lại endpoint với mã cũ và reason mới; nếu mã cũ đã được dùng hoặc Book đang reserved thì trả `409`.
- **Compatibility:** Order/Payment/Inventory/Promotion giữ nguyên vì dùng BookId. Mã cũ phải `404` ngay sau cutover; không alias. Nhân viên phải in/dán tem mới ngay theo response. Không sửa lịch sử giao dịch.
- **Out of scope:** `UpdateBookDto.Barcode`, Barcode Alias/PreviousBarcode, migration/audit table, Kiosk/CMS UI, auto-print, Borrowing module mới, sửa PR #68.
- **PR/merge gate:** PR riêng cho Issue #60 BE-02; Release build 0 errors; implementation evidence; developer tests và independent QA tách biệt. Independent QA phải bao phủ authorization, validation, sequential/concurrent duplicate, same-book race, reservation race, transaction rollback, audit, old/new lookup và regression Books/Inventory/Checkout/Order/Payment. Chưa có code/evidence nên **IMPLEMENTATION NOT STARTED / QA NOT TESTED / MERGE NOT READY**.

### 7.4 Handoff có thể gửi cho member

**Thiện — Backend (`@thien33`)**

- Chức năng hiện được giao: [#60](https://github.com/Unknown4505/Library-System/issues/60) BE-01 Extended Search đã targeted Backend QA PASS trong open PR #68; #60 BE-02 Controlled Barcode Modification nay được duyệt theo `BOOK-BARCODE-BE02-01`. #66 User mutation dùng quyết định riêng `USER-MGMT-BE02-01`. Mỗi BE-02 phải ở PR riêng đúng Issue; không nối thêm vào PR #68.
- Tasks #60 BE-02: bổ sung DTO action riêng; endpoint Admin-only; service/repository conditional atomic update; DB-specific unique conflict mapping; structured audit; response bắt buộc reprint; không sửa schema/UI/lịch sử.
- Dependencies: dùng entity/index/UoW hiện hữu; phối hợp QA disposable SQL. Không phụ thuộc Barcode Alias, migration, Borrowing module hay thay đổi FE. Khi `ReservedQuantity > 0`, phải trả conflict thay vì đổi mã.
- Ownership: Controller/Service/DTO/repository validation, RBAC, transaction và concurrency Backend. Không sửa Domain entity, EF configuration, migration/snapshot, Kiosk/CMS UI hoặc PR #68.
- Deliverables: PR riêng tham chiếu `#60 BE-02`, exact SHA, changed-file list, request/response examples, Release build result, mô tả transaction/concurrency/audit và developer test result. Báo blocker trong Issue #60 trước khi tự mở rộng design.
- Acceptance: unit/service tests và independent API/SQL/concurrency/regression evidence theo §7.3; không báo IMPLEMENTED/QA PASS/MERGE READY trước khi đủ từng gate.

**Khang — Frontend (`@lehuukhang`)**

- Chức năng: [Issue #61](https://github.com/Unknown4505/Library-System/issues/61) chỉ bao gồm Kiosk WPF camera/search/mock isolation. Issue #29 Receipt UI/PDF đã hoàn thành targeted scope. CMS Reports/Users trong PR [#64](https://github.com/Unknown4505/Library-System/pull/64) không tự thuộc #61 và cần tách hoặc Leader approve mở rộng scope; CMS Kiosk Monitoring cần loại/tách vì đã OUT OF SCOPE.
- Dependencies: FEAT-02 #61 không còn chờ implementation/targeted QA của #60 BE-01 nhưng vẫn chờ merge, physical camera/WPF acceptance và real Backend integration evidence; CMS Users chờ #66 Read merge và BE-02 implementation theo contract đã duyệt. Revenue FE monthly/negative/unavailable interaction đã independent targeted QA PASS tại `ea010e34`, nhưng CMS↔Backend↔SQL E2E vẫn pending. CMS Kiosk Monitoring không chờ Backend và không phải tiêu chí merge/E2E. Có thể chuẩn bị UI bằng fake API nhưng phải giữ mock/unavailable state rõ ràng và không báo Production E2E PASS.
- Ownership: Kiosk Receipt View/ViewModel/API client/Mock Printer/PDF và CMS pages/state. Không sửa Backend Controller/Service, Domain/EF/migration hoặc tự định nghĩa contract khác.
- Acceptance: targeted ViewModel/UI tests, xử lý 400/401/403/404/409/network; UI ẩn nút không được coi là security enforcement; không mock rồi báo E2E PASS.

**PR #64 boundary — updated 07/11/2026:** exact independently tested HEAD `ea010e34cbed79c938bb6c530f78e68f2306cc9c` chỉ thay đổi Kiosk/CMS/test phía FE, không triển khai Backend. Trạng thái: **OPEN / INDEPENDENT CMS REVENUE & INTERACTION TARGETED QA PASS / TEST CLEANUP PENDING / LEADER E2E & FINAL MERGE GATES PENDING**. Task 11.3 giữ `[ ]` cho tới Leader final acceptance; Tasks 8.1/11.5 không hoàn thành. Không chuyển dependency #60/#66/#71 sang Khang và không khôi phục Monitoring #65 đã đóng.

**PR #64 test ownership cleanup — PENDING:** theo [Leader independent QA acceptance](https://github.com/Unknown4505/Library-System/pull/64#issuecomment-6703615150), Khang phải gỡ đúng phần test tự bổ sung trong PR: ba file `MonthlyRevenueNormalizerTests.cs`, `ReportsControllerTests.cs`, `UsersControllerTests.cs`; hai method append trong `DashboardControllerTests.cs`; năm method append và import chỉ dùng cho chúng trong `DashboardIntegrationTests.cs`. Phải giữ toàn bộ baseline Leader-owned (21 DashboardController + 11 DashboardIntegration methods), `CustomWebApplicationFactory` và production implementation. Independent acceptance evidence được Leader quản lý ngoài PR tại reusable harness nội bộ; cleanup chưa được ghi DONE.

**Leader:** giữ Domain/EF schema, shared contract, quyết định security/business, phê duyệt scope PR #64 và integration QA.

### 7.5 REVENUE-MONTHLY-01 — APPROVED / FE INDEPENDENT TARGETED QA PASS / REAL E2E PENDING

- **Backend `@thien33`:** giữ contract aggregation hiện hữu của `GET /api/reports/revenue?year={year}` — Paid-only, mỗi tháng tối đa một row trong `1..12`, `month ASC`; điều tra nếu API thực tế trả duplicate/out-of-range. Không tạo endpoint/DTO mới cho quyết định này.
- **Frontend `@lehuukhang`:** validate range/uniqueness, zero-fill sparse valid data thành 12 row và dùng cùng normalized dataset cho Dashboard/Reports chart, table, KPI/totals. Duplicate/out-of-range/null/malformed phải bị reject atomically; không silent `GroupBy + Sum`, không partial UI và phải log metadata năm/tháng/count không chứa order-level details.
- **AJAX/initial render:** lỗi authorization/API/network/JSON/contract giữ last-good year và rollback toàn UI; initial invalid response hiển thị Revenue unavailable/invalid, không giả doanh thu 0. Empty successful list vẫn là dữ liệu hợp lệ để zero-fill.
- **Leader `@Unknown4505`:** đã approve contract ngày 2026-11-06; giữ ownership, thực hiện final CMS↔BE↔DB E2E và quyết định merge gate.
- **Trạng thái:** Revenue Backend **IMPLEMENTED**; `REPORT-AJAX-STALE-01` **QA PASS 19/19** tại `10aad080`. Lịch sử tại `b161554`: Dashboard unavailable KPI **QA FAIL P2**. Sau fix, FE strict monthly normalization, valid empty/sparse, duplicate/range rejection, initial unavailable interaction, recovery, Top Books independence và AJAX rollback/stale đã **INDEPENDENT TARGETED QA PASS** tại `ea010e34` (44/44 baseline, 28 browser assertions, 8/8 normalizer cases). PR #64 vẫn **OPEN / HOLD — DO NOT MERGE** do test cleanup và final gates; Leader cross-layer E2E **NOT DONE** và Issue #61 không bị mở rộng scope bởi CMS Reports.

### 7.6 REVENUE-NEGATIVE-01 — APPROVED / FE INDEPENDENT TARGETED QA PASS / BE HARDENING PENDING

- **Approved business contract:** mọi order `Paid` hợp lệ có `TotalAmount > 0`; monthly Revenue và TotalOrders không âm; tháng hợp lệ không có Paid orders là 0/0. Negative Revenue/TotalOrders là `InvalidContract`: CMS reject nguyên tử, không clamp, không giả unavailable thành zero. Refund/reversal/negative adjustment chưa thuộc contract và cần approval riêng nếu bổ sung sau này.
- **Existing Backend implementation:** Kiosk/POS checkout đã reject và rollback payable `<= 0`; Revenue repository cộng `Paid Order.TotalAmount`; TotalOrders lấy từ `Count()`. Đây không phải bằng chứng Domain/DB/payment transition đã được harden hoặc runtime database có dữ liệu âm.
- **Leader `@Unknown4505`:** đã approve financial contract ngày 2026-11-06; giữ authority với schema change và final cross-layer E2E. Database constraint chỉ được triển khai sau review legacy/QA database và approval riêng; `docs/init.sql` không chứng minh schema Production.
- **Backend `@thien33` — [Issue #71](https://github.com/Unknown4505/Library-System/issues/71):** giữ positive-payable checkout; chặn order amount không hợp lệ chuyển `Paid`; ngăn dữ liệu nguồn âm bị aggregate hoặc bị monthly sum dương che khuất; điều tra invariant violation. Hardening và migration đều **NOT DONE**; không tạo/apply DB constraint khi chưa có approval riêng.
- **Frontend `@lehuukhang` — PR [#64](https://github.com/Unknown4505/Library-System/pull/64):** strict negative Revenue rejection, strict monthly validation, last-good-year rollback và validated dataset chung cho chart/table/KPI đã **INDEPENDENT TARGETED QA PASS** tại `ea010e34`. Dashboard unavailable KPI **FAIL P2** tại `b161554` là lịch sử trước fix, không còn là current fix requirement.
- **Leader E2E:** **NOT DONE / PENDING**. Phải phân biệt approved contract, FE independent targeted QA PASS, Backend implementation hiện hữu và hardening #71 chưa làm; targeted evidence không thay thế CMS↔Backend↔SQL E2E.

## 8. Acceptance Leader và blocker

- Leader Task 12.1.1 chạy `Checkout → Pending → SePay Paid → Receipt API → UI/PDF` trên SQL QA/test mode. Thiếu SePay/SQL/endpoint thì `BLOCKED — ENV`.
- #19/#20 đã closed/completed và PR #26 đã merge; không mở lại hoặc sửa lại nếu không có regression evidence mới.
- Migration HR đã scaffold an toàn qua design-time factory và static-review chỉ có additive HR operations. Apply/rollback/runtime SQL vẫn `BLOCKED — ENV` vì chưa có connection QA disposable được xác minh; không chạy API hay `database update` trên DB hiện hữu.
- Revenue Backend đã có `GET /api/reports/revenue` và `GET /api/reports/top-books`; `REVENUE-MONTHLY-01` và `REVENUE-NEGATIVE-01` đã APPROVED. Dashboard unavailable KPI P2 fix/retest đã independent FE QA PASS tại `ea010e34`; Backend hardening #71 và CMS Reports real E2E vẫn phải chạy riêng. FE PASS/contract approval không phải bằng chứng hardening đã hoàn thành.
- #65 đã **CLOSED — NOT PLANNED**; không yêu cầu heartbeat monitoring policy, GET APIs hoặc Monitoring E2E mới. #66 BE-02 policy đã được Leader duyệt tại `USER-MGMT-BE02-01`; mọi endpoint/schema ngoài approved subset vẫn **NEEDS LEADER DECISION**.
- **PR #68 scope gate:** #60 BE-02 đã có quyết định riêng `BOOK-BARCODE-BE02-01` nhưng phải triển khai trong PR mới; không đưa Barcode modification vào PR #68 và không suy ra PR #68 `MERGED`, `FULL E2E PASS` hay Issue #60 `CLOSED`. Approval BE-02 không thay đổi trạng thái QA/merge của PR #68.
- Merge readiness chỉ đạt khi build PASS, diagram XML PASS, PNG được Leader visual-review/xuất lại, migration review + SQL QA PASS và các test Receipt/E2E theo dependency hoàn thành. Docs/model riêng không đủ để claim feature complete.

## 9. Handoff Kiosk Advertising & Navigation — IMPLEMENTED / TARGETED QA VERIFIED; FINAL INTEGRATION BLOCKED

**Khang — Frontend (`@lehuukhang`)**

- Issue [#38](https://github.com/Unknown4505/Library-System/issues/38). Ownership: `IdlePage` slideshow/loading/empty/error, API client/cache/message handling, touch → `SearchPage`, Receipt → `SearchPage`, inactivity/slideshow/Receipt timer lifecycle, interaction reset và offline fallback.
- Acceptance: deserialize đúng Contract V1; xử lý enum 1/2, null/empty/partial/error, placeholder ảnh và message không spam; slideshow 8s configurable; Search inactivity 60s; không duplicate timer/request; sau Receipt giữ dữ liệu in/PDF an toàn, clear completed session/cart và không cancel Paid.
- Trạng thái: PR #54 HEAD `9431b33` đã **IMPLEMENTED / TARGETED QA VERIFIED**; final double-advance QA đạt 7/7, Debug/Release PASS. Backend #37 và Receipt #29 đã merge. Final WPF visual/real-API integration bị dừng trước khi có UI evidence; Search inactivity chỉ eligible tại `SearchPage`, không áp dụng tại `BookDetailPage` hoặc màn giao dịch active.

**Thiện — Backend (`@thien33`)**

- Issue [#37](https://github.com/Unknown4505/Library-System/issues/37). Backend đã merge qua PR #52 (`e537971`) và targeted runtime QA PASS tại HEAD `60e084d`; fix cuối ProductDiscount ở HEAD `f7634a0` không chạm Advertising.
- Ownership: action/service/query/DTO; wrapper/auth/message; promotion filtering; nhập kho gần nhất; Paid-only best sellers; projection/limit/partial/503 behavior theo contract đã duyệt.
- Không sửa UI #38, không mở rộng logic checkout ProductDiscount, không nới quyền CMS, không tạo bảng `Advertisements`, schema hay migration mới. Chỉ quảng cáo promotion mà luồng Checkout Kiosk thực sự áp dụng được.

**Leader (`@Unknown4505`)**

- Đã chốt endpoint/DTO, new arrivals 30 ngày/10, best sellers 30 ngày/10, partial success/503, cache/retry, ProductDiscount safety và response messages; tiếp tục review `X-Api-Key`, contract consistency, E2E/timer lifecycle/fallback và regression payment/Receipt.

- Backend #37 và Receipt #29 đã đủ source + targeted evidence và đã merge; #38 đã đủ **implementation + targeted QA evidence** ở PR #54 HEAD `9431b33`, nhưng chưa đủ WPF visual/real Paid E2E evidence để đánh dấu toàn feature `[x]` hoặc gọi là E2E PASS.

Issue bodies trên GitHub xác nhận phạm vi: #37 Backend `@thien33`, #38 Frontend `@lehuukhang`, #29 Receipt UI/PDF `@lehuukhang`. Lượt docs này không tạo/comment/update issue. GitHub connector không trả metadata assignee chính thức, nên ownership ở đây dựa trên nội dung issue và vẫn cần Leader quản trị assignment nếu UI GitHub khác.

**Response responsibility:** BE bảo đảm mọi nhánh có wrapper/code/message/data đúng contract và không lộ chi tiết nội bộ; FE kiểm tra toàn bộ wrapper, dùng fallback/message thân thiện và không spam toast do timer/background refresh. Backend `PARTIAL_SUCCESS`, `SERVICE_UNAVAILABLE`, auth message và 503 đã có runtime evidence PASS; phần FE xử lý/timer/fallback vẫn là scope #38 riêng.

## 10. Handoff ProductDiscount V7.1 — BACKEND MERGED / QA PASS

### 10.1 Leader-owned foundation

- Domain snapshot: `PromotionNameAtTime`, `PromotionTypeAtTime`, `PointsUsedAmountAtTime`, `BookTitleAtTime`, `DiscountPerUnitAtTime`.
- Shared Promotion CRUD, Kiosk/POS Checkout và Receipt DTO contract; JSON enum numeric 1/2, decimal money và non-null collections.
- Docs/SRS/E2E contract cho one-promotion selection, tie-break, non-stacking, points, zero payable và legacy receipt.
- EF mapping và migration additive `20261021084154_AddProductDiscountV71Snapshots` đã được scaffold từ worktree `origin/main`, kiểm tra model snapshot và áp dụng thành công trên SQL Server local của Leader. Không dùng kết quả local này để giả định các môi trường khác đã migrate.

### 10.2 Backend handoff — `@thien33`

1. [x] **BE-01 Promotion CRUD:** mở rộng route hiện hữu, persist type-specific detail, PUT replace-all transaction, validation/auth/message.
2. [x] **BE-02 Calculation & Checkout:** Kiosk highest benefit + `PromotionId ASC`, POS manual-only, ProductDiscount per unit, points, zero-payable rollback và snapshot tại Pending.
3. [x] **BE-03 Receipt:** persisted snapshot mapping, legacy fallback, không tính lại promotion; regression Receipt #28.

Backend đã merge qua PR #52 (`e53797104b6a6e8732071eeb2aa64e0bb4968d85`). Targeted QA cuối tại HEAD `f7634a0a1002e5835559d2af9cadfc0e51612a50` PASS 8/8 cho UTC validation, POS manual PromotionId và SQL cleanup; vòng trước PASS 13/15 ProductDiscount và 10/10 direct transaction regression, hai lỗi còn lại đã được retest PASS trước merge.

Backend không đổi Advertising #37 trong các task này. ProductDiscount chỉ được đưa vào Advertising sau Checkout implementation và E2E PASS.

### 10.3 Frontend handoff — `@lehuukhang`

1. [x] **FE-01 CMS Promotion:** type/detail editor, nhiều sách/mức giảm, PUT full state và hiển thị validation Backend — implemented và targeted QA PASS tại PR #58 HEAD `054d5f1`; merge pending, browser UI chưa chạy.
2. [x] **FE-02 Kiosk/POS:** catalog giữ giá niêm yết; totals và item breakdown lấy từ Backend; POS không tự thay promotion — implemented và targeted QA PASS tại PR #58 HEAD `054d5f1`; merge pending, cross-layer E2E #48 chưa chạy.
3. [x] **FE-03 Receipt/PDF:** dùng Receipt API snapshot, hiển thị promotion/line discount, không dùng cart local; regression #29 — đã merge qua PR #40 và targeted QA PASS; WPF visual/UI E2E chưa chạy.

### 10.4 Acceptance/dependencies

- Dependency bắt buộc: foundation/migration → BE-01 → BE-02 → BE-03; FE work đi sau contract/runtime tương ứng; Leader integration QA cuối.
- Evidence: DTO serialization, migration static review, SQL transactional tests, Kiosk/POS response, zero-payable rollback, legacy receipt, QR amount và cancel/stock/points regression.
- Backend BE-01/02/03 đã đủ runtime evidence và merge. FE-01/02 đã có targeted evidence riêng tại PR #58 HEAD `054d5f1` (9/9 final retest PASS) nhưng PR còn chờ merge; FE-03 đã merge qua PR #40. Browser/WPF visual và cross-layer E2E #48 vẫn chưa chạy, không được suy ra từ targeted PASS.
