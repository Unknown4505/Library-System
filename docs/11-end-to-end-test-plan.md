# Kế hoạch kiểm thử End-to-End — BookKiosk

## 1. Phạm vi và nguyên tắc

Tài liệu này là kế hoạch thực thi Task 12.1. Việc có test plan không đồng nghĩa đã runtime-test. Chỉ chuyển một ca sang PASS khi có log API, đối chiếu SQL Server và bằng chứng UI tương ứng.

- Môi trường: Windows, API ASP.NET Core chạy Development, Kiosk WPF cấu hình RealApi, SQL Server test riêng và SePay Test Mode/sandbox.
- Không dùng Production DB, API key, webhook secret hoặc tài khoản ngân hàng thật.
- Mỗi lần chạy dùng tiền tố dữ liệu `E2E-<run-id>`; ghi lại OrderId, OrderCode và ReferenceCode.
- Trước khi chạy: apply toàn bộ migration, xác nhận constraints tồn kho, cấu hình KioskId tồn tại và giữ backup/test database có thể xóa.
- Trạng thái ban đầu của mọi ca dưới đây là **BLOCKED** cho tới khi đủ endpoint của Issue #19/#20, PR #11 được tích hợp và môi trường runtime sẵn sàng.

## 2. Ma trận kịch bản

| ID | Kịch bản | Preconditions và test data | Các bước | Expected DB | Expected UI/API | Cleanup | Trạng thái |
|---|---|---|---|---|---|---|---|
| E2E-01 | Happy path Kiosk | Book A active, stock 3/reserved 0; KioskId hợp lệ; member không bắt buộc | Search A → add cart → checkout → quét QR sandbox → polling → receipt | Một Order Pending rồi Paid; Reserved +1 rồi về 0; Stock còn 2; một PaymentTransaction | Search/checkout wrapper hợp lệ; polling Paid; receipt khớp Order/OrderDetails | Xóa dữ liệu theo run-id hoặc restore DB test | BLOCKED BE/FE/ENV |
| E2E-02 | Promotion + 100 điểm | Member có >=100 điểm; promotion fixed amount active; Book đủ kho | Lookup member → chọn 100 → checkout → thanh toán sandbox | PointsUsed=100; DiscountAmount đúng promotion; TotalAmount = SubTotal - DiscountAmount - 100000; điểm chỉ cập nhật một lần | UI không tính trùng hai loại giảm; receipt khớp DB | Xóa order/point/payment test và reset member | BLOCKED BE/FE/ENV |
| E2E-03 | Hết kho/concurrency | Book B stock 1/reserved 0; hai Kiosk/client | Hai checkout đồng thời cùng quantity 1 | Chỉ một Order được reserve; 0 <= Reserved <= Stock | Một client nhận success; client còn lại nhận 409, không fallback mock | Cancel order thắng và xác nhận reserve về 0 | BLOCKED ISSUE #19/ENV |
| E2E-04 | Barcode không tồn tại | Barcode ngẫu nhiên không có trong DB | Quét barcode | Không thay đổi DB | 404 wrapper; UI báo không tìm thấy, cart không đổi | Không cần | BLOCKED FE/ENV |
| E2E-05 | API offline | RealApi bật; dừng API sau khi vào Home | Search/lookup/checkout khi mất mạng | Không tạo order local/DB | UI báo lỗi mạng và cho retry; tuyệt đối không tự chuyển Mock/Cancelled/Paid | Khởi động lại API | BLOCKED FE/ENV |
| E2E-06 | SePay Test Mode | Pending order, sandbox credential và webhook contract đã xác nhận | Thanh toán đúng amount/content qua test mode | Một transaction unique; Order Paid; kho/điểm đổi đúng một lần | Polling Paid và receipt hiện ra | Xóa dữ liệu sandbox/test DB | BLOCKED ISSUE #20/ENV |
| E2E-07 | Sai số tiền | Pending order; webhook test có amount khác TotalAmount | Gửi qua SePay Test Mode theo contract thật | PaymentTransaction lưu vết; Order vẫn Pending; kho vẫn reserved | API 200 theo webhook policy; Kiosk báo chờ hỗ trợ, không in receipt | Cancel/cleanup order và nhả reserve | BLOCKED ISSUE #20/ENV |
| E2E-08 | Duplicate webhook | Pending order; cùng ReferenceCode | Phát lại webhook đồng thời hai lần | Chỉ một PaymentTransaction/tác dụng thanh toán; Stock/points đổi một lần | Cả retry xử lý idempotent; trạng thái cuối Paid | Xóa dữ liệu test | BLOCKED ISSUE #19/#20/ENV |
| E2E-09 | Timeout | Pending order không thanh toán; timeout config test ngắn | Chờ cleanup worker | Order Cancelled; Reserved giảm đúng quantity; Stock không đổi | Polling nhận Cancelled, không in receipt | Xóa order test | BLOCKED ISSUE #20/ENV |
| E2E-10 | Cancel đồng thời Paid | Pending order; chuẩn bị webhook đúng | Gửi cancel và webhook gần đồng thời | Chỉ một terminal state hợp lệ; không âm/double release; nếu Paid thì stock trừ một lần, nếu Cancelled thì reserve nhả một lần | API xung đột trả rõ; UI dựa vào trạng thái server | Đối chiếu rồi xóa dữ liệu test | BLOCKED ISSUE #19/ENV |
| E2E-11 | Receipt/PDF mock | Order Paid có nhiều dòng | Gọi receipt → mở mock printer/PDF | Không thay đổi DB; dữ liệu đọc từ Order/OrderDetails | OrderCode, line, giá snapshot, giảm giá, điểm, total đúng; không lộ CostPrice/PII | Xóa file PDF test và dữ liệu DB | BLOCKED BE/FE/ENV |
| E2E-12 | Heartbeat | KioskId tồn tại, worker bật | Chờ ít nhất hai chu kỳ; sau đó gửi Error hợp lệ | LastPingAt UTC tăng; incident được tạo đúng contract | 200 wrapper; CMS/status phản ánh thiết bị khi endpoint tổng hợp có sẵn | Resolve/xóa incident test | BLOCKED FE/ENV |
| E2E-13 | CMS Reports | Có các order Paid/Cancelled nhiều tháng bằng dữ liệu test | Đăng nhập Admin → gọi revenue/top-books | Chỉ Paid được aggregate; không mutate DB | Wrapper typed DTO khớp tổng SQL; non-Admin bị từ chối | Xóa dataset report | BLOCKED AUTH/FE/ENV |
| E2E-14 | Security regression | Build Development và Release; key/token đúng/sai | Thử thiếu/sai API key, JWT role sai, F3 ở hai môi trường, maintenance dialog | Không thay đổi DB cho request bị chặn | 401 thiếu key, 403 key sai; F3 Release 404; không có password Production mặc định | Xóa secret test khỏi session, không commit | BLOCKED BE/FE/ENV |

### Kết quả thực thi gần nhất (2026-10-11)

Các kết quả dưới đây bổ sung trường Input, Actual và Owner cho từng Test ID. Component tests chạy trên `c8@8da6d4d`; HTTP smoke test chạy trên merge không commit `origin/main@fbc15a2` + `origin/Khang@d9de651` với SQL Server Express database riêng `BookKiosk_PR11_Smoke_20261011`. Database và worktree tạm đã được xóa sau khi test. Ca nào chưa chạy đủ WPF + SePay vẫn giữ BLOCKED.

| ID | Input thực tế | Actual result | Status | Owner còn lại |
|---|---|---|---|---|
| E2E-01 | HTTP checkout BookId=1, quantity 1, không member | API tạo OrderId=1/`ORD20261011164229`, Pending, QR amount=50.000; reserve 0→1; cancel trả Cancelled và reserve 1→0. Chưa có Paid/receipt/WPF | BLOCKED | Thiện receipt/payment, Khang WPF, Leader E2E Paid |
| E2E-02 | Giá 300.000, promotion 50.000, member dùng 100 điểm | SQL test trả TotalAmount 150.000 đúng một lần; chưa chạy UI/payment | BLOCKED | Leader integration, Thiện #19/#20 |
| E2E-03 | Hai checkout đồng thời, stock 1 | SQL Server PASS: đúng một order và ReservedQuantity=1 | BLOCKED | Leader chạy API/WPF smoke |
| E2E-04 | Barcode `DOES-NOT-EXIST` | HTTP API trả 404 `NOT_FOUND`; chưa chạy thao tác scanner/UI | BLOCKED | Khang/Leader WPF runtime |
| E2E-05 | RealApi khi API offline | Static verified: ApiClient trả NETWORK/TIMEOUT, không fallback Mock; chưa runtime | BLOCKED | Khang/Leader runtime |
| E2E-06 | Pending order, correct amount | Chưa có SePay Test Mode được cấu hình | BLOCKED | Thiện #20, Environment |
| E2E-07 | amountIn lệch TotalAmount | SQL Server PASS: transaction được ghi và order vẫn Pending | BLOCKED | Thiện #20, Leader API/WPF |
| E2E-08 | Hai webhook cùng ReferenceCode đồng thời | FAIL: lần xử lý thứ hai tiếp tục trừ reserve và đụng CK_Books_ReservedQuantity | FAIL | Thiện #19 |
| E2E-09 | Pending order quá timeout | Cleanup/paid race test chưa thể chạy từ branch c8 hiện tại | BLOCKED | Thiện #19/#20 |
| E2E-10 | Cancel và webhook chạy đồng thời | SQL invariant test PASS một lần; chưa chạy qua HTTP/UI | BLOCKED | Leader integration, Thiện #19 |
| E2E-11 | Receipt DTO chứa order metadata và line snapshot | Serialization/security contract PASS; HTTP receipt endpoint trả 404 và WPF vẫn dựng receipt từ cart local | BLOCKED | Thiện receipt, Khang receipt |
| E2E-12 | `kioskId=1`, status Online, API key đúng | HTTP 200; SQL xác nhận `LastPingAt=2026-10-11T09:42:28`, Status=1. PR #11 vẫn fallback KioskId=1; chưa chạy worker đủ hai chu kỳ/incident | BLOCKED | Khang FE-18, Leader WPF/incident runtime |
| E2E-13 | Revenue/top-books typed DTO | DTO serialization PASS; authorization test FAIL vì ReportsController không giới hạn Admin | FAIL | Thiện Backend, Khang CMS |
| E2E-14 | API key thiếu/sai; F3/receipt; maintenance | HTTP thiếu key trả 401, key sai trả 403; F3 và receipt trả 404; PR #11 còn password hardcode; Books POST không JWT vẫn trả 201 | FAIL | Thiện auth/#20, Khang Maintenance |

## 3. Đối chiếu bắt buộc sau mỗi order

Ghi lại và so sánh trực tiếp các trường: `OrderId`, `OrderCode`, `SubTotal`, `DiscountAmount`, `PointsUsed`, `TotalAmount`, `OrderStatus`, `CompletedAt`, `StockQuantity`, `ReservedQuantity`, `PaymentTransactions.ReferenceCode` và số dư điểm thành viên. Với race test, chụp trạng thái trước/sau trong cùng database test và giữ log timestamp UTC.

## 4. Lệnh và bằng chứng

- Contract/unit suite: `dotnet test tests/BookKiosk.LeaderTests/BookKiosk.LeaderTests.csproj -p:NuGetAudit=false -m:1 -nr:false`.
- SQL suite chỉ chạy khi `BOOKKIOSK_TEST_SQLSERVER` trỏ tới SQL Server test không phải Production. Bộ test tự tạo database tên ngẫu nhiên và xóa sau khi hoàn tất.
- Lưu bằng chứng gồm test result, API log đã che secret, ảnh UI, truy vấn DB trước/sau và phiên bản commit của Backend/Frontend.
- Cleanup thất bại phải đánh FAIL/BLOCKED và ghi database còn sót; không chạy lệnh xóa rộng trên database dùng chung.

## 5. Điều kiện nghiệm thu Task 12.1

Task chỉ được chuyển sang hoàn thành khi toàn bộ kịch bản bắt buộc đã chạy hoặc blocker được Leader chấp nhận bằng văn bản, không có lỗi P0/P1 mở, và có ma trận kết quả Actual/PASS-FAIL gắn với một bộ SHA cụ thể. Tại thời điểm tạo tài liệu này, Task 12.1 vẫn chưa hoàn thành.

## 6. Delta verification cho HR và Receipt (2026-10-16)

Các dòng kết quả 2026-10-11 ở trên là evidence lịch sử. #19/#20 hiện closed/completed và PR #26 đã merge; không mở lại. Trạng thái mới phải gắn SHA mới, không suy diễn từ log cũ.

| Test | Mức | Expected | Trạng thái hiện tại |
|---|---|---|---|
| HR-MODEL-01 | Build/model | `Admin=1`, `Staff=2`, `Manager=3`; Employee/User filtered unique; salary unique/check/RESTRICT | PASS — 2/2 automated model-contract tests |
| HR-SQL-01 | SQL integration | Migration additive tạo 19 bảng, giữ dữ liệu/precision cũ, rollback sạch trên DB QA disposable | BLOCKED — ENV |
| RECEIPT-DTO | Contract/serialization | Shared `ReceiptDto` có đủ field server-owned và không serialize dữ liệu nhạy cảm | PASS — 2/2 contract tests |
| RECEIPT-200 | HTTP contract | Paid + CompletedAt → 200 `ApiResponseDto<ReceiptDto>` từ persisted rows, GET không side effect | BLOCKED — #28 |
| RECEIPT-404 | HTTP contract | order không tồn tại → 404 | BLOCKED — #28 |
| RECEIPT-409 | HTTP contract | Pending/Cancelled → 409, không trả receipt | BLOCKED — #28 |
| RECEIPT-PRIVACY | Contract/security | Không có CostPrice, phone/member PII | BLOCKED — #28 |
| RECEIPT-UI-PDF | WPF/E2E | Paid polling → API → UI/mock printer/PDF cùng DTO | BLOCKED — #29 depends #28 |
| SALARY-RBAC | HTTP/security | Admin/Manager/Staff đúng policy và row authorization, không lộ lương người khác | BLOCKED — API chưa implement |

Không chạy API để thử migration vì startup auto-migrate. EF CLI scaffold qua design-time factory không khởi chạy API. SQL chỉ được chạy khi `BOOKKIOSK_TEST_SQLSERVER` đã được xác minh trỏ tới database `BookKiosk_QA_<run-id>` disposable.

### Evidence lượt 2026-10-16 — `origin/main@efcd388a` + `codex/leader-workflow-integration`

| Kiểm tra | Kết quả | Evidence / giới hạn |
|---|---|---|
| `dotnet build BookKiosk.slnx -c Release -m:1` | PASS | Release build V6: PASS, 0 errors, 123 warnings. Warning delta so với baseline Release: NOT VERIFIED. `NU1902`/`NU1903` được theo dõi riêng như security follow-up; không nâng package trong V6.2. |
| `dotnet test ... --filter FullyQualifiedName~BookKiosk.Tests.Contracts.Hr` | PASS | 2/2: enum role/status và EF design-time model (nullability, decimal(18,0), filtered unique, unique kỳ lương, check, RESTRICT). Không kết nối DB. |
| `dotnet test ... --filter FullyQualifiedName~BookKiosk.Tests.Contracts.Receipts` | PASS | 2/2 DTO contract/serialization. Không chứng minh endpoint #28 vì endpoint chưa implement. |
| Test harness main compatibility | PASS sau sửa tối thiểu | Bổ sung 4 delegation `GetOrderByCodeNoTrackingAsync` vào fake repositories và 1 file HR test dưới 100 dòng trong test repo độc lập; production không đổi để chiều test. |
| Migration `20261016071312_AddEmployeeSalaryLedger` | PASS static / NOT RUNTIME TESTED | EF CLI log dùng design-time factory; `Up()` chỉ 2 CreateTable + 4 CreateIndex, không alter/drop bảng cũ. Không chạy `database update`. |
| SQL apply/rollback | BLOCKED — ENV | `BOOKKIOSK_TEST_SQLSERVER` absent; không có database `BookKiosk_QA_<run-id>` disposable được xác minh. |
| `.drawio` XML/ID/references | PASS structural | 6 file parse được, trang 1/3/1/1/1/3, 0 duplicate ID, 0 missing parent/source/target ref. Visual và PNG NOT RUNTIME TESTED theo quyết định Leader. |

Security follow-up riêng từ restore metadata của targeted tests: `NU1903` cho `AutoMapper 13.0.1`; `NU1902` và `NU1903` cho transitive `System.Security.Cryptography.Pkcs 9.0.11` qua `Microsoft.Data.SqlClient 6.1.6`. V6.2 không nâng package; cần dependency review/regression riêng. `NU1900` chỉ cho biết vulnerability feed không truy vấn được tại một số lượt restore, không chứng minh package an toàn hoặc có lỗ hổng mới.

### Salary contract tests — APPROVED DESIGN, IMPLEMENTATION PLANNED

Các ca dưới đây là acceptance plan cho HR-DEC-01/02/03. Salary Service/API chưa tồn tại nên tất cả đều **PLANNED / NOT RUN**, không phải PASS.

| ID | Test tối thiểu | Expected | Trạng thái |
|---|---|---|---|
| SAL-01 | Tạo/cập nhật Draft với PayYear `1999`, `2000`, `2100`, `2101` | 2000/2100 hợp lệ; 1999/2101 trả validation error chuẩn | PLANNED / NOT RUN |
| SAL-02 | PayMonth `0`, `1`, `12`, `13` | 1/12 hợp lệ; 0/13 bị từ chối | PLANNED / NOT RUN |
| SAL-03 | Gửi các khoản tiền hợp lệ và NetSalary đúng/sai công thức | Server enforce `Base + Allowance - Deduction`; từ chối sai công thức hoặc kết quả âm | PLANNED / NOT RUN |
| SAL-04 | Tạo Draft | `PaidByUserId=null`, `PaidAt=null` | PLANNED / NOT RUN |
| SAL-05 | Admin xác nhận Paid | Actor lấy từ JWT Admin; `PaidAt` là UTC server; status/actor/time commit cùng transaction | PLANNED / NOT RUN |
| SAL-06 | Manager xem phiếu Paid của chính Employee | ALLOW khi JWT UserId map đúng `Employees.UserId` | PLANNED / NOT RUN |
| SAL-07 | Staff xem phiếu Paid của chính Employee | ALLOW khi JWT UserId map đúng `Employees.UserId` | PLANNED / NOT RUN |
| SAL-08 | Manager/Staff đổi ID để xem phiếu người khác | DENY; không dùng UserId/EmployeeId client làm ownership | PLANNED / NOT RUN |
| SAL-09 | Manager/Staff xem Draft của chính mình hoặc người khác | DENY | PLANNED / NOT RUN |
| SAL-10 | JWT User không có Employee liên kết | DENY quyền xem lương cá nhân | PLANNED / NOT RUN |
| SAL-11 | Manager xác nhận Paid | DENY; chỉ Admin được pay | PLANNED / NOT RUN |
| SAL-12 | Hai request đồng thời xác nhận cùng Draft | Chỉ một request chuyển sang Paid; không ghi actor/time hai lần | PLANNED / NOT RUN |
| SAL-13 | Sửa hoặc xóa phiếu Paid qua API thường | DENY; ledger Paid immutable | PLANNED / NOT RUN |

## 7. Acceptance — Kiosk Idle Advertising & Navigation

Feature chưa được triển khai/runtime-test; tất cả ca dưới đây là **PLANNED / NOT RUN**.

| ID | Kịch bản | Expected | Trạng thái |
|---|---|---|---|
| ADS-01 | Khởi động Kiosk | Vào `IdlePage`, slideshow quảng cáo bắt đầu; không yêu cầu login | PLANNED / NOT RUN |
| ADS-02 | Không tương tác 60 giây tại `SearchPage` | Chuyển về Idle Advertising đúng một lần | PLANNED / NOT RUN |
| ADS-03 | Chạm màn hình quảng cáo | Dừng/dispose slideshow timer và vào `SearchPage` có search + danh sách sách | PLANNED / NOT RUN |
| ADS-04 | Backend trả đủ ba nhóm | Promotion/new arrivals/best sellers map đúng field và ảnh; không dùng dữ liệu local giả | PLANNED / NOT RUN |
| ADS-05 | Không có promotion active | Bỏ slide promotion; các nhóm hợp lệ khác vẫn chạy | PLANNED / NOT RUN |
| ADS-06 | Promotion inactive/chưa tới ngày/hết hạn | Không hiển thị | PLANNED / NOT RUN |
| ADS-07 | API advertising lỗi/timeout | Không crash; hiện fallback “Chạm để bắt đầu”; touch vẫn hoạt động khi Search API khả dụng | PLANNED / NOT RUN |
| ADS-08 | Rời/quay lại `IdlePage` nhiều lần | Không còn timer cũ, không nhân đôi timer/request; slide interval cấu hình mặc định 8s | PLANNED / NOT RUN |
| ADS-09 | Đang Member/chọn điểm/checkout/Pending/đang tải hoặc xem Receipt | Inactivity không đưa về Idle; payment polling/timeout/cancel hiện hữu không bị thay đổi | PLANNED / NOT RUN |
| NAV-01 | Polling trả `Paid` | Vào Receipt; không gọi cancel và không bỏ qua hiển thị/in | PLANNED / NOT RUN |
| NAV-02 | Receipt hoàn tất/hết 5s hợp lệ | Giữ dữ liệu UI/PDF/in an toàn, clear cart/session rồi về `SearchPage`, không về Idle ngay | PLANNED / NOT RUN |
| NAV-03 | Search sau Receipt | Không còn item, điểm dùng, OrderId hoặc state giao dịch cũ | PLANNED / NOT RUN |
| NAV-04 | Sau Receipt về Search rồi không tương tác 60s | Chuyển sang Idle Advertising | PLANNED / NOT RUN |
| NAV-05 | Receipt API/PDF lỗi hoặc in chưa hoàn tất | Retry/error state; không dựng receipt giả và timer không cắt ngang thao tác | PLANNED / NOT RUN |

### Advertising API Contract V1 — targeted acceptance

Các rule 30 ngày/limit 10, partial success, cache 5 phút và retry đã được Leader duyệt ở mức Contract V1. Endpoint/UI chưa implement nên toàn bộ case dưới đây vẫn `PLANNED / NOT RUN`, không phải runtime PASS.

| ID | Kịch bản | Expected | Trạng thái |
|---|---|---|---|
| ADS-API-01 | GET đúng route với API key hợp lệ | HTTP 200, wrapper/message không rỗng, `data` không null và bốn arrays luôn tồn tại | PLANNED / NOT RUN |
| ADS-API-02 | Thiếu key / key sai | 401 `UNAUTHORIZED` / 403 `FORBIDDEN`, `data=null`, message không lộ key/config/nội bộ | PLANNED / NOT RUN |
| ADS-API-03 | Promotion type 1 | `promotionType=1`, `orderDiscount` có decimal numeric, `productDiscounts=[]` | PLANNED / NOT RUN |
| ADS-API-04 | Promotion type 2 có nhiều sách | `promotionType=2`, `orderDiscount=null`, mỗi product có book/mức giảm riêng | PLANNED / NOT RUN |
| ADS-API-05 | Enum serialization | JSON trả number `1/2`, không trả string hoặc giá trị ngoài enum | PLANNED / NOT RUN |
| ADS-API-06 | `imageUrl=null` hoặc ảnh tải lỗi | Contract giữ field null; FE dùng placeholder, không crash | PLANNED / NOT RUN |
| ADS-API-07 | Không có dữ liệu phù hợp | HTTP 200 `SUCCESS`, message empty-state, ba content arrays và `unavailableSections` đều `[]` | PLANNED / NOT RUN |
| ADS-API-08 | Một hoặc nhiều data section lỗi | HTTP 200, `success=true`, `PARTIAL_SUCCESS`; nhóm lỗi `[]`, nhóm tốt giữ data, `unavailableSections` đúng allow-list | PLANNED / NOT RUN |
| ADS-API-09 | Cả ba data source lỗi hoặc lỗi hệ thống ngoài dự kiến | Toàn bộ nguồn lỗi → 503 `SERVICE_UNAVAILABLE`; lỗi ngoài dự kiến → 500 `INTERNAL_ERROR`; wrapper/message an toàn | PLANNED / NOT RUN |
| ADS-API-10 | New arrivals | Rolling 30 ngày UTC theo lần nhập gần nhất, distinct book, active/còn hàng, tối đa 10, sort time DESC rồi BookId ASC | PLANNED / NOT RUN |
| ADS-API-11 | Best sellers | Rolling 30 ngày UTC; chỉ Paid/CompletedAt; sum Quantity; loại Pending/Cancelled; active/còn hàng; tối đa 10; sort totalSold DESC rồi BookId ASC | PLANNED / NOT RUN |
| ADS-API-12 | Cache hết 5 phút hoặc promotion hết hạn sớm | Reload đúng lúc, không dùng promotion hết hạn và không request theo slide | PLANNED / NOT RUN |
| ADS-API-13 | Message mọi nhánh | Success/empty/partial/error đều message tiếng Việt không rỗng; FE không hiển thị lỗi kỹ thuật thô | PLANNED / NOT RUN |
| ADS-API-14 | Background refresh/timer | Không có toast trùng khi slide đổi, inactivity chạy, refresh hoặc retry nền | PLANNED / NOT RUN |
| ADS-NAV-REG-01 | Paid → Receipt → Search → 60s Idle Ads | Không regression receipt/PDF/cart cleanup/payment; chỉ Idle sau 60s Search inactivity | PLANNED / NOT RUN |
| ADS-PROMO-01 | Promotion enum/branch | JSON number `1/2`; order/product branch đúng nullability; collections không null | PLANNED / NOT RUN |
| ADS-PROMO-02 | ProductDiscount chưa được Checkout hỗ trợ | Không đưa type 2 vào nội dung Kiosk, không hứa/tự tính mức giảm; type 1 vẫn hoạt động | PLANNED / NOT RUN |
| ADS-RETRY-01 | Advertising API lỗi trong một lượt Idle | Tối đa một retry sau ít nhất 30 giây; retry lỗi thì fallback/cooldown; không request storm | PLANNED / NOT RUN |

Acceptance cần evidence UI/timer lifecycle, request log, trạng thái cart/session và regression payment/Receipt API/PDF. Không đánh dấu PASS chỉ từ docs hoặc mock.

## 9. ProductDiscount V7.1 — targeted matrix

| ID | Kịch bản | Expected | Trạng thái |
|---|---|---|---|
| PD-E2E-01 | OrderDiscount only | Chọn đúng promotion đủ `MinOrderValue`; line discount đều 0; order discount đúng snapshot | PLANNED / NOT RUN |
| PD-E2E-02 | ProductDiscount only | Giảm đúng từng đơn vị, tổng line discount bằng `Orders.DiscountAmount` | PLANNED / NOT RUN |
| PD-E2E-03 | Promotion + points | Promotion áp trước, points áp sau; tổng dương và snapshot đúng | PLANNED / NOT RUN |
| PD-E2E-04 | Nhiều promotion | Chọn benefit lớn nhất, không stacking | PLANNED / NOT RUN |
| PD-E2E-05 | Benefit đồng hạng | Chọn `PromotionId ASC` | PLANNED / NOT RUN |
| PD-E2E-06 | ProductDiscount nhiều sách | Chỉ sách trong cùng Promotion được giảm, mỗi sách giữ mức riêng | PLANNED / NOT RUN |
| PD-E2E-07 | Quantity > 1 | `lineDiscountAmount = discountPerUnit × quantity` | PLANNED / NOT RUN |
| PD-E2E-08 | Promotion invalid/expired | Kiosk loại khỏi candidates; POS trả lỗi và không tự thay promotion | PLANNED / NOT RUN |
| PD-E2E-09 | POS manual/no selection | Chọn đúng requested promotion; không chọn thì discount 0 | PLANNED / NOT RUN |
| PD-E2E-10 | Zero/negative payable | HTTP 400 `BAD_REQUEST`, không Pending/QR, kho và điểm rollback | PLANNED / NOT RUN |
| PD-E2E-11 | Pending → Paid | Promotion/totals không bị tính lại | PLANNED / NOT RUN |
| PD-E2E-12 | Receipt snapshot | Receipt/PDF khớp snapshot promotion, points và từng line | PLANNED / NOT RUN |
| PD-E2E-13 | Legacy order | Fallback title/points an toàn; không sửa Paid cũ, không suy đoán ProductDiscount | PLANNED / NOT RUN |
| PD-E2E-14 | Cancel/stock/points regression | Hủy nhả reserve; lỗi rollback điểm/kho đúng một lần | PLANNED / NOT RUN |
| PD-E2E-15 | QR amount | QR amount bằng persisted `TotalAmount`, là số nguyên VNĐ và > 0 | PLANNED / NOT RUN |

Targeted contract QA phải kiểm tra JSON camelCase, enum numeric 1/2, collection không null, nullable legacy fields và item breakdown. Runtime chỉ PASS khi BE/FE implementation cùng SQL test evidence hoàn tất.
