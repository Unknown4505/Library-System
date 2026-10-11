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
