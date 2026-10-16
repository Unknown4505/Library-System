# Workflow handoff và phạm vi current/target — BookKiosk

Ngày đối chiếu: 2026-10-16. Integration baseline: `origin/main@efcd388a2c5c6791eeecefe6e511d6b31ec8a91f`, branch working tree `codex/leader-workflow-integration`.

## 1. Quyết định đã chốt

| Chủ đề | Current source | Target | Verification |
|---|---|---|---|
| Loại sách | `Categories` | Giữ nguyên; không có `BookTypes` | PASS static |
| Hóa đơn | Paid `Orders` + `OrderDetails` | Receipt API/UI #28/#29; không có `Invoices` | PLANNED |
| Customer/account | `Members` tách khỏi `Users` | Giữ nguyên | PASS static |
| Role | `Admin=1`, `Staff=2` ở baseline | Thêm `Manager=3`, không đổi số cũ | PASS source; chưa runtime DB |
| Schema | Main baseline 17 bảng | 19 bảng sau migration: thêm Employees/SalaryPayments | Model + migration scaffold/static review PASS; runtime SQL chưa chạy |
| Lương | Chưa có | Sổ tháng Draft/Paid đơn giản | PLANNED API/UI |
| Device incidents | Kiosks/KioskIncidents là bảng thật | Không là process BFD/DFD chính | PASS design |

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
| Quản lý tài khoản | R/W | Không mặc định | Self only | PLANNED |
| Hồ sơ nhân viên | R/W | Theo policy | Self only | PLANNED |
| Sổ lương | Create/Pay/Read all | Own Paid only; không pay | Own Paid only | APPROVED DESIGN; API/UI PLANNED |
| Reports | Yes | No | No | Contract hiện tại Admin-only |
| Kiosk receipt | API key thiết bị | API key thiết bị | API key thiết bị | #28 PLANNED |

## 4. Traceability nghiệp vụ và sơ đồ

| Process | Datastore/entity | Source liên quan | Trạng thái |
|---|---|---|---|
| 2.0/2.4 Checkout | Books, Orders, OrderDetails | `OrderService`, checkout DTO | IMPLEMENTED; diagram phải thể hiện reserve |
| 2.6 Paid | Books, Orders, PaymentTransactions, Members, PointTransactions | `PaymentService`, PaymentsController | IMPLEMENTED core; E2E môi trường còn thiếu |
| 2.7 Receipt | Orders, OrderDetails, Books | Contract `ReceiptDto`, Issue #28/#29 | PLANNED |
| 2.8 Cancel/Timeout | Books, Orders | `OrderService`, cleanup service | IMPLEMENTED core; nhả reserved, không trừ stock |
| 3.0 Nhập kho | Suppliers, ImportReceipts, ImportReceiptDetails, Books | Import service/API | IMPLEMENTED source |
| 8.0 Reports | Orders/OrderDetails, Books | ReportsController/DTO | IMPLEMENTED endpoint; Admin authorization cần evidence riêng |
| HR target | Users, Employees, SalaryPayments | Domain + EF config + migration `20261016071312` | Model/migration SCAFFOLDED; API PLANNED; runtime SQL NOT TESTED |

Artefacts canonical: [`BFD.drawio`](../diagram/BFD.drawio), [`DFD_Level_0_Context.drawio`](../diagram/DFD_Level_0_Context.drawio), [`DFD_Level_1_Main.drawio`](../diagram/DFD_Level_1_Main.drawio), [`DFD_Level_2_P2_Invoice_Checkout.drawio`](../diagram/DFD_Level_2_P2_Invoice_Checkout.drawio), [`ERD_Current_and_Target_3_Pages.drawio`](../diagram/ERD_Current_and_Target_3_Pages.drawio) và [`BookKiosk_Class_Diagram_Current_Target.drawio`](../diagram/BookKiosk_Class_Diagram_Current_Target.drawio). Current phản ánh source; Target được phép chứa HR nhưng phải ghi `PLANNED`/runtime unknown. Bốn file legacy đã được loại khỏi worktree. Theo quyết định Leader ngày 2026-10-16, lượt này chỉ cập nhật `.drawio`; PNG hiện có không được ghi đè và sẽ được Leader vẽ/xuất lại sau.

## 5. Handoff Issue #28 — Thiện

Owner: `@thien33`. Task 5.6. Status: **PLANNED / OPEN**.

- File ownership: OrdersController, OrderService/interface/repository và test BE liên quan; không đổi HR scaffold.
- Contract: `GET /api/orders/kiosk/{orderId}/receipt`, `X-Api-Key`, chỉ Paid + valid CompletedAt trả 200 `ApiResponseDto<ReceiptDto>` từ persisted `Orders`/`OrderDetails`.
- Acceptance: missing 404; Pending/Cancelled 409; repeated GET không side effect; không lộ CostPrice/phone/member PII; BookTitle tạm đọc qua `OrderDetails.Book`.
- Evidence cần nộp: targeted unit/HTTP tests 200/404/409/privacy/idempotency, SHA và test log; SQL chỉ trên QA disposable.

**DRAFT comment — không post:** “Thiện triển khai Task 5.6 theo contract `docs/04-api-reference.md`: Receipt API chỉ đọc order đã Paid từ persisted data, có targeted tests 200/404/409/privacy/idempotency. Không thêm Invoices và không dùng cart client. Gửi SHA + test log; SQL nếu có chỉ dùng QA disposable.”

## 6. Handoff Issue #29 — Khang

Owner: `@lehuukhang`. Task 8.2.1. Status: **PLANNED / OPEN, DEPENDS ON #28**.

- File ownership: Kiosk ReceiptPage/ViewModel, API client integration, Mock Printer và QuestPDF.
- Dùng real `OrderId` sau polling Paid và cùng `ReceiptDto` Backend; không tính lại từ cart local, không viết lại UI ngoài phạm vi.
- Acceptance: hiển thị/in/PDF nhất quán; 404/409/timeout/network xử lý an toàn; targeted ViewModel test trước, WPF runtime/E2E khi #28 sẵn sàng.
- Evidence cần nộp: targeted tests, ảnh UI/PDF đã che PII, SHA và log; không tự mock Paid để tuyên bố E2E PASS.

**DRAFT comment — không post:** “Khang triển khai Task 8.2.1 sau khi #28 PASS contract. Khi polling Paid, gọi Receipt API bằng OrderId thật; ReceiptPage/Mock Printer/QuestPDF dùng cùng ReceiptDto và xử lý 404/409/network. Nộp targeted ViewModel test + UI/PDF evidence; không lấy total từ cart local.”

## 7. Draft handoff bổ sung — chờ Leader duyệt

Không tạo Issue hoặc giao việc chính thức từ bảng này. Owner dưới đây là **đề xuất** để Leader duyệt trước khi triển khai.

| Hạng mục | Owner đề xuất | Dependencies | API/Data contract | Outputs | Acceptance criteria | Trạng thái |
|---|---|---|---|---|---|---|
| Auth/RBAC CMS | BE `@thien33`; Leader chốt policy; FE `@lehuukhang` tích hợp login | `Users`, JWT config, role matrix Admin/Manager/Staff | `/api/auth/login`, `/api/users`, password/JWT/security rules | Auth/User API + CMS login/session | Không trả `PasswordHash`; 401/403 đúng; enum số không đổi; targeted auth/role tests PASS | **PLANNED / APPROVAL REQUIRED** |
| Employee | BE `@thien33`; FE `@lehuukhang`; Leader sở hữu schema | Auth/RBAC PASS; migration HR được apply trên QA disposable | `Employee` DTO/policy, filtered unique UserId/EmployeeCode, disable thay delete | Employee API + CMS CRUD/self profile | 400/404/409 đúng; row authorization; không lộ PII; targeted API tests PASS | **PLANNED** |
| Salary ledger | BE `@thien33`; FE `@lehuukhang`; Leader giữ schema/shared contract | Auth + Employee PASS; SQL QA migration PASS | `SalaryPayment` Draft/Paid; PayYear 2000..2100; unique kỳ lương; decimal(18,0); công thức NetSalary; Admin-only pay; Manager/Staff own-Paid | Salary API + CMS draft/pay/read | Draft không actor/time; Draft→Paid atomically; Admin actor + UTC; concurrent pay chỉ thắng một; Paid immutable; JWT row ownership; không chuyển tiền ngân hàng | **APPROVED DESIGN; IMPLEMENTATION PLANNED** |
| NCC/Supplier CMS | BE `@thien33`; FE `@lehuukhang` | Supplier/Import contract hiện có; authorization CMS | `Suppliers`, `ImportReceipts`, DTO/validation và delete policy | Supplier CRUD/list + CMS integration | Không xóa làm mất lịch sử nhập; duplicate/invalid trả wrapper chuẩn; import regression targeted PASS | **PLANNED** |

Mỗi handoff cần issue/task riêng sau khi Leader duyệt, kèm file ownership và test evidence; không gộp vào #28/#29.

### 7.1 Salary enforcement contract cho member — APPROVED DESIGN

- **DB ENFORCED:** unique `(EmployeeId, PayYear, PayMonth)`; `PayMonth` 1..12; các khoản tiền và NetSalary không âm; status chỉ Draft/Paid; FK và delete `RESTRICT`.
- **APPLICATION REQUIRED — HR-DEC-01:** tạo/cập nhật Draft chỉ nhận `PayYear` 2000..2100, inclusive; ngoài khoảng trả validation error. Migration không có PayYear CHECK.
- **APPLICATION REQUIRED — HR-DEC-03 Draft:** xác định `BaseSalarySnapshot` hợp lệ, không đổi null thành 0; tiền không âm; tính `NetSalary = BaseSalarySnapshot + AllowanceAmount - DeductionAmount` và kết quả không âm; để `PaidByUserId`/`PaidAt` null. `HireDate` tiếp tục nullable.
- **APPLICATION REQUIRED — HR-DEC-03 Paid:** chỉ Admin chuyển `Draft → Paid`; lấy actor từ JWT và `PaidAt` UTC phía server; status/actor/time trong một transaction; concurrency chỉ cho một request thắng; không pay hai lần; không sửa/xóa Paid qua API thường.
- **APPLICATION REQUIRED — HR-DEC-02 Read:** Admin xem all Draft/Paid; Manager/Staff chỉ xem Paid của chính Employee khi JWT UserId khớp `Employees.UserId`; deny Draft, người khác và identity không có Employee. Không dùng UserId từ client. Manager không pay.
- **APPROVED DESIGN:** giữ nguyên migration `20261016071312_AddEmployeeSalaryLedger`, không thêm SQL CHECK trong V6.3. Salary là ledger ghi nhận thanh toán, không thực hiện chuyển tiền ngân hàng. Service/API chưa có nên vẫn **PLANNED**, không phải IMPLEMENTED.

### 7.2 Handoff có thể gửi cho member

**Thiện — Backend (`@thien33`)**

- Chức năng: Issue #28 Receipt API; sau đó Auth, Employee và Salary API theo contract đã chốt.
- Dependencies: migration HR phải PASS trên SQL QA disposable; Auth/JWT identity mapping sẵn sàng trước Employee self-service/Salary; Receipt #28 độc lập với Salary.
- Ownership: Controller/Service/DTO/validation/RBAC/transaction/concurrency Backend. Không sửa Domain entity, EF configuration, migration/snapshot hoặc Kiosk/CMS UI nếu chưa phối hợp Leader.
- Acceptance: targeted API/service tests cho validation, JWT row ownership, Draft/Paid transition, concurrent pay và immutable Paid; không báo IMPLEMENTED trước khi có code + evidence.

**Khang — Frontend (`@lehuukhang`)**

- Chức năng: Issue #29 Receipt UI/PDF phụ thuộc #28; CMS Auth/Employee/Salary/NCC theo DTO/API contract.
- Dependencies: #28 PASS contract trước Receipt integration; Auth/Employee/Salary API thật trước integration/runtime acceptance. Có thể chuẩn bị UI bằng fake API nhưng phải giữ DTO/error states tương thích.
- Ownership: Kiosk Receipt View/ViewModel/API client/Mock Printer/PDF và CMS pages/state. Không sửa Backend Controller/Service, Domain/EF/migration hoặc tự định nghĩa contract khác.
- Acceptance: targeted ViewModel/UI tests, xử lý 400/401/403/404/409/network; UI ẩn nút không được coi là security enforcement; không mock rồi báo E2E PASS.

**Leader:** giữ Domain/EF schema, shared contract, quyết định security/business và integration QA.

## 8. Acceptance Leader và blocker

- Leader Task 12.1.1 chạy `Checkout → Pending → SePay Paid → Receipt API → UI/PDF` trên SQL QA/test mode. Thiếu SePay/SQL/endpoint thì `BLOCKED — ENV`.
- #19/#20 đã closed/completed và PR #26 đã merge; không mở lại hoặc sửa lại nếu không có regression evidence mới.
- Migration HR đã scaffold an toàn qua design-time factory và static-review chỉ có additive HR operations. Apply/rollback/runtime SQL vẫn `BLOCKED — ENV` vì chưa có connection QA disposable được xác minh; không chạy API hay `database update` trên DB hiện hữu.
- Merge readiness chỉ đạt khi build PASS, diagram XML PASS, PNG được Leader visual-review/xuất lại, migration review + SQL QA PASS và các test Receipt/E2E theo dependency hoàn thành. Docs/model riêng không đủ để claim feature complete.
