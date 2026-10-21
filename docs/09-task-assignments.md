# Tiến độ Dự án & Phân công công việc — BookKiosk

Tài liệu này quy định tiến độ thực hiện dự án (15 tuần) và phân công công việc chi tiết cho từng thành viên theo từng Module.

## 1. Bảng tiến độ 15 Tuần

| Tuần | Thời gian | Nội dung công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Kết quả mong đợi |
|:---:|---|---|:---:|:---:|:---:|---|
| **1** | Tuần 1 | Phân tích yêu cầu, chốt kiến trúc & Database | [x] | [ ] | [ ] | Hoàn thành Docs, Setup GitHub, Db Schema |
| **2** | Tuần 2 | Khởi tạo Solution, cấu hình EF Core, DI | [x] | [ ] | [ ] | Project chạy được, kết nối SQL Server |
| **3** | Tuần 3 | API: M1 (Sách/Danh mục) & M2 (Thành viên) | [x] | [ ] | [ ] | Hoàn thành API Core, Leader setup DTO |
| **4** | Tuần 4 | Kiosk WPF: M3 (Điều hướng) & M4 (Tra cứu) | [x] | [ ] | [ ] | Leader cấu hình HTTP Client gọi API |
| **5** | Tuần 5 | Kiosk WPF: M5 (Checkout API) & M6 (Cart UI) | [x] | [ ] | [ ] | Logic giữ kho, hiển thị Giỏ hàng Kiosk |
| **6** | Tuần 6 | Kiosk WPF: M6 (Cart UI - tiếp tục) | [ ] | [ ] | [ ] | Kiosk tạo đơn, binding data |
| **7** | Tuần 7 | Payment: M7 (SePay Webhook) | [x] | [ ] | [ ] | Leader làm cầu nối Polling giữa FE và BE |
| **8** | Tuần 8 | Hardware: M8 (Máy in, Quét mã, Camera) | [x] | [ ] | [ ] | Leader code Mock Hardware, Khang ráp UI |
| **9** | Tuần 9 | CMS Web: M9 (Quản lý Sách, Danh mục) | [x] | [ ] | [ ] | CMS CRUD được sản phẩm |
| **10** | Tuần 10 | CMS Web: M10 (Kho bãi, POS) & M11 (Thống kê) | [x] | [ ] | [ ] | Leader setup CORS & Tích hợp CMS với BE |
| **11** | Tuần 11 | **Kiểm thử tích hợp & Sửa lỗi (Freeze Code)** | [x] | [ ] | [ ] | Leader ghép nối toàn bộ luồng Kiosk và CMS |
| **12** | Tuần 12 | M12 (Kiểm thử, Cải thiện UX báo lỗi Kiosk) | [x] | [ ] | [ ] | Kiosk không bao giờ bị Crash |
| **13** | Tuần 13 | M12 (Viết Báo cáo, Vẽ Sơ đồ) | [x] | [ ] | [ ] | Draft báo cáo quyển Word |
| **14** | Tuần 14 | Chuẩn bị Data Demo, Setup IIS, Kịch bản Demo | [x] | [ ] | [ ] | Dữ liệu đầy đủ để thuyết trình hội đồng |
| **15** | Tuần 15 | **Review & Nộp đồ án** | [x] | [ ] | [ ] | Nộp mã nguồn và báo cáo |

---

## 2. Phân công công việc chi tiết theo 12 Module

> **Quy ước:** 
> - **Leader (@Unknown4505):** Phụ trách Kiến trúc, **Tích hợp/Ghép nối (Integration)** giữa Frontend và Backend, Setup thư viện và Mock Hardware.
> - **BE (@thien33):** Phụ trách WebAPI, xử lý logic Application, Entity Framework.
> - **FE (@lehuukhang):** Phụ trách vẽ Kiosk WPF và giao diện CMS MVC.

### MODULE 0: SETUP & DATABASE
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 0.1 | Khởi tạo Solution, 6 Projects theo kiến trúc 3 lớp | @Unknown4505 | | | `[x]` |
| 0.2 | Tạo file Markdown Docs, CODEOWNERS, `.gitignore` | @Unknown4505 | | | `[x]` |
| 0.3 | **[Leader]** Code toàn bộ Domain Entities & Enums | @Unknown4505 | | | `[x]` |
| 0.4 | **[Leader]** Setup DbContext & Fluent API Configurations | @Unknown4505 | | | `[x]` |
| 0.5 | **[Leader]** Viết DbInitializer (Seed Data) & Chạy Migration | @Unknown4505 | | | `[x]` |
| 0.6 | **[Leader]** Setup Dependency Injection (DI) & Serilog (Log lỗi) chung cho hệ thống | @Unknown4505 | | | `[x]` |
| 0.7 | **[Leader]** Scaffold additive Employees/SalaryPayments, Manager=3; sinh/verify migration trên DB QA disposable | @Unknown4505 | | | `[ ]` — model + migration scaffold/static review xong; SQL QA BLOCKED — ENV |

### MODULE 1: BACKEND API - SÁCH & DANH MỤC
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 1.1 | **[Leader]** Thiết lập `GlobalExceptionHandlerMiddleware` | @Unknown4505 | | | `[x]` |
| 1.2 | API: `GET /api/books` (Có phân trang, tìm kiếm) | | @thien33 | | `[x]` |
| 1.3 | API: `GET /api/books/barcode/{barcode}` | | @thien33 | | `[x]` |
| 1.4 | API: `GET /api/categories` và `GET /api/areas` | | @thien33 | | `[x]` |
| 1.5 | **[Leader]** Cấu hình Swagger JWT & API Key (Bảo mật) | @Unknown4505 | | | `[x]` |
| 1.6 | **[Leader]** Khai báo các Shared DTOs và Constants dùng chung cho BE và FE | @Unknown4505 | | | `[x]` |

### MODULE 2: BACKEND API - THÀNH VIÊN & ĐIỂM
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 2.1 | API: `GET /api/members/{phoneNumber}` (Tra SĐT) | | @thien33 | | `[x]` |
| 2.2 | API: Cập nhật, tạo mới Thành viên | | @thien33 | | `[x]` |
| 2.3 | API: `GET /api/members/{id}/point-history` | | @thien33 | | `[x]` |

### MODULE 3: KIOSK WPF - ĐIỀU HƯỚNG & TRANG CHỦ
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 3.1 | Thiết lập MVVM (BaseViewModel, RelayCommand) | | | @lehuukhang | `[x]` |
| 3.2 | Thiết lập `NavigationService` (Quản lý Frame) | | | @lehuukhang | `[x]` |
| 3.3 | Layout `IdlePage`: Màn hình chờ + Video QC | | | @lehuukhang | `[x]` |
| 3.4 | Cấu hình `IdleTimerService` (Quay về màn hình chờ) | | | @lehuukhang | `[x]` |
| 3.5 | Layout `HomePage`: (Đã gộp chung vào SearchPage) | | | @lehuukhang | `[x]` |
| 3.6 | **[Leader]** Setup HttpClientFactory / ApiClient thuần túy để FE kiểm soát hoàn toàn API | @Unknown4505 | | | `[x]` |

### MODULE 4: KIOSK WPF - TÌM KIẾM & CHI TIẾT
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 4.1 | Layout `SearchPage`: Lưới sách + Load More | | | @lehuukhang | `[x]` |
| 4.2 | Logic Filter theo Danh mục, Tác giả | | | @lehuukhang | `[x]` |
| 4.3 | Layout `BookDetailPage`: Chi tiết + Vị trí kệ | | | @lehuukhang | `[x]` |
| 4.4 | Kiểm tra Tồn kho trước khi cho thêm vào giỏ | | | @lehuukhang | `[x]` |
| 4.5 | **[Leader]** Ghép nối: Đổi từ Mock Data Data giả sang gọi API thật (Phần Search & Detail) | @Unknown4505 | | | `[ ]` |

### MODULE 5: BACKEND API - CHECKOUT & GIỮ KHO
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 5.1 | API: `POST /api/orders/kiosk/checkout` | | @thien33 | | `[x]` |
| 5.2 | Logic: Tự động quét và áp dụng Promotion (KM) | | @thien33 | | `[x]` |
| 5.3 | Logic: Trừ tiền điểm (Points), tính TotalAmount | | @thien33 | | `[x]` |
| 5.4 | Logic: Cập nhật `ReservedQuantity` & Lưu Order | | @thien33 | | `[x]` |
| 5.5 | **[Leader]** Viết test kịch bản tính điểm và áp KM, fix bug Concurrency | @Unknown4505 | | | `[ ]` |
| 5.6 | API: `GET /api/orders/kiosk/{orderId}/receipt` — dữ liệu hóa đơn chính thức từ Orders/OrderDetails ([Issue #28](https://github.com/Unknown4505/Library-System/issues/28)) | <br> | @thien33 | <br> | `[ ]` |

### MODULE 6: KIOSK WPF - GIỎ HÀNG & THANH TOÁN
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 6.1 | Layout `CartPage`: Hiển thị sách, Tăng giảm SL | | | @lehuukhang | `[x]` |
| 6.2 | Bàn phím ảo (Numpad) nhập SĐT quy đổi điểm | | | @lehuukhang | `[x]` |
| 6.3 | Khang gọi API Checkout, nhận OrderCode sang Payment | | | @lehuukhang | `[x]` |
| 6.4 | `PaymentPage`: Hiện bảng tóm tắt tiền + Đếm ngược | | | @lehuukhang | `[x]` |

### MODULE 7: PAYMENT - SEPAY & WEBHOOK
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 7.1 | API: Sinh link/ảnh QR VietQR theo chuẩn SePay | | @thien33 | | `[x]` |
| 7.2 | API: `POST /api/payments/sepay-webhook` | | @thien33 | | `[x]` |
| 7.3 | Logic: Xác thực HMAC Signature bảo mật | | @thien33 | | `[ ]` |
| 7.4 | Logic: Cập nhật Paid, trừ kho vật lý (StockQuantity)| | @thien33 | | `[x]` |
| 7.5 | Kiosk `PaymentPage`: Polling API Status mỗi 3s | | | @lehuukhang | `[x]` |
| 7.6 | **[Leader]** Ghép nối: Viết logic Polling cho FE để tự động chuyển trang khi BE nhận được tiền (SignalR hoặc Timer) | @Unknown4505 | | | `[ ]` |

### MODULE 8: KIOSK WPF - THIẾT BỊ PHẦN CỨNG
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 8.1 | Kiosk: Gắn UI xử lý dữ liệu từ Camera/Máy quét | | | @lehuukhang | `[x]` |
| 8.2 | Kiosk: Sinh file PDF Hóa đơn với QuestPDF | | | @lehuukhang | `[ ]` |
| 8.2.1 | Tích hợp Receipt API vào màn hình hóa đơn, Mock Printer và PDF; dùng ReceiptDto từ Backend ([Issue #29](https://github.com/Unknown4505/Library-System/issues/29), phụ thuộc #28) | <br> | <br> | @lehuukhang | `[ ]` |
| 8.3 | Kiosk: `MaintenancePage` Báo lỗi thiết bị, khóa Kiosk | | | @lehuukhang | `[ ]` |
| 8.4 | **[Leader]** Khởi tạo Interface phần cứng (`IBarcodeScanner`, `IReceiptPrinter`) và Class Mock | @Unknown4505 | | | `[x]` |
| 8.4.1 | **[Ghi chú Kiosk]** FE bắt buộc dùng interface `BookKiosk.Kiosk.Services.Hardware.IBarcodeScanner` đã được Leader mock sẵn, KHÔNG tự tạo service quét mã mới. | | | @lehuukhang | `[x]` |
| 8.4.2 | Tự định nghĩa dữ liệu Mock (Mock Data) cho Bill in ra màn hình để nắm rõ cấu trúc dữ liệu | | | @lehuukhang | `[ ]` |
| 8.5 | **[Leader]** Triển khai gọi Driver ESC/POS in nhiệt thật | @Unknown4505 | | | `[ ]` |
| 8.6 | HeartbeatService: `POST /api/kiosk/heartbeat` | | | @lehuukhang | `[ ]` |

### MODULE 9: CMS WEB - QUẢN LÝ SẢN PHẨM
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 9.1 | **[Leader]** Admin API: CRUD Sách, Danh mục, Khu vực | @Unknown4505 | | | `[ ]` |
| 9.2 | **[Leader]** Layout Dashboard (SB Admin / Bootstrap) | @Unknown4505 | | | `[x]` |
| 9.3 | **[Leader]** Trang Quản lý Sách (DataTables / Grid) | @Unknown4505 | | | `[x]` |
| 9.4 | **[Leader]** Form Thêm/Sửa sách (Upload ảnh) | @Unknown4505 | | | `[x]` |
| 9.5 | **[Leader]** Cấu hình CORS Policy để Web Admin có thể gọi được Backend API | @Unknown4505 | | | `[x]` |

### MODULE 10: CMS WEB - TỒN KHO, KM & POS
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 10.1 | Admin API: CRUD Khuyến mãi, Nhập kho | | @thien33 | | `[x]` |
| 10.2 | Trang Nhập kho (Inventory) | | | @lehuukhang | `[ ]` |
| 10.3 | Trang Quản lý Khuyến mãi | | | @lehuukhang | `[ ]` |
| 10.4 | Trang POS Bán tại quầy (Áp mã thủ công) | | | @lehuukhang | `[ ]` |

### MODULE 11: THỐNG KÊ DOANH THU
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 11.1 | API: `GET /api/reports/revenue` (Group by Tháng) | | @thien33 | | `[x]` |
| 11.2 | API: `GET /api/reports/top-books` | | @thien33 | | `[x]` |
| 11.3 | CMS: Vẽ Biểu đồ Bar Chart Doanh thu | | | @lehuukhang | `[ ]` |
| 11.4 | CMS: Biểu đồ Pie Chart Trạng thái Kiosk | | | @lehuukhang | `[ ]` |
| 11.5 | **[Leader]** Ghép nối data thống kê giữa BE & FE | @Unknown4505 | | | `[ ]` |

### MODULE 12: KIỂM THỬ, BÁO CÁO & ĐÓNG GÓI
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 12.1 | **[Leader]** Kịch bản Test End-to-End toàn luồng | @Unknown4505 | | | `[ ]` |
| 12.1.1 | Kiểm thử E2E Checkout → Paid → Receipt API → Kiosk hiển thị và xuất PDF (Issues #28, #29) | @Unknown4505 | <br> | <br> | `[ ]` |
| 12.2 | **[Leader]** Vẽ Sơ đồ Context Diagram, BFD, DFD | @Unknown4505 | | | `[ ]` |
| 12.2.1 | **[Leader]** Đồng bộ BFD/DFD/ERD/Class Diagram current/target và sửa DG-01…DG-08; PNG sẽ được Leader vẽ/xuất lại sau | @Unknown4505 | | | `[ ]` |
| 12.3 | **[Leader]** Vẽ ERD Database | @Unknown4505 | | | `[x]` |
| 12.4 | Viết Báo cáo Word Đồ án cuối kỳ | @Unknown4505 | @thien33 | @lehuukhang | `[ ]` |

---

## 3. Tổng hợp khối lượng công việc

| Thành viên | Số đầu việc chính | Chức năng (Modules) phụ trách |
|---|---|---|
| **@Unknown4505** | ~27 | Base Config (Entities, DbContext, Migrations, Middleware, Swagger), CMS Sách |
| **@thien33** | ~22 | Code Controller/Service (Logic), Điểm thưởng, Thanh toán SePay, Webhook |
| **@lehuukhang** | ~29 | Thiết kế UI Kiosk, Xử lý giao diện CMS Quản lý tồn kho & Doanh thu |

### Ghi chú nghiệm thu cập nhật 2026-10-11

### Handoff Receipt cập nhật 2026-10-16

- **Task 5.6 / Issue #28 — @thien33:** Receipt API chỉ đọc `Orders`/`OrderDetails` đã Paid; nộp test 200/404/409, privacy và idempotent GET. Trạng thái `PLANNED`, không coi docs/diagram là code hoàn thành.
- **Task 8.2.1 / Issue #29 — @lehuukhang:** dùng `ReceiptDto` thật cho Receipt UI, Mock Printer, QuestPDF; xử lý network/404/409; phụ thuộc #28 PASS contract.
- **Task 12.1.1 — @Unknown4505:** E2E trên SQL QA disposable và SePay test mode. Nếu thiếu SQL/SePay/endpoint thì ghi `BLOCKED — ENV`, không mô phỏng rồi tự báo PASS.
- #19/#20 đã closed/completed và PR #26 đã merge; không mở lại. Chi tiết file ownership, acceptance và draft comment ở `12-workflow-handoff-and-scope.md`.
- **Handoff PLANNED:** Auth/RBAC và Employee/Salary giao Backend `@thien33` triển khai sau contract; CMS Auth/Employee/Salary/NCC giao FE `@lehuukhang`; Leader giữ Domain/EF schema, shared contract và integration QA. Chưa tạo Issue mới; Owner/Dependencies/API-Data contract/Acceptance nằm ở docs/12.
- **Salary APPROVED DESIGN (HR-DEC-01/02/03):** PayYear 2000..2100; Admin xem all và pay; Manager/Staff chỉ xem own Paid qua JWT→Employee; Backend enforce công thức NetSalary, Draft/Paid actor-time, transaction/concurrency và immutable Paid. API/UI vẫn `[ ]` PLANNED; migration giữ nguyên và SQL QA vẫn `BLOCKED — ENV`.

### Handoff Kiosk Advertising & Navigation — flow và Contract V1 đã duyệt (2026-10-21)

GitHub đã có Issue #37 (Backend) và #38 (Frontend); #29 tiếp tục giữ riêng Receipt UI/PDF. Cập nhật docs là `DOCS COMPLETED`, không đồng nghĩa feature đã implement hoặc test PASS.

| Nhãn dự kiến | Phân loại | Owner | Dependency | Acceptance chính | Trạng thái |
|---|---|---|---|---|---|
| FE — Kiosk Idle Advertising ([#38](https://github.com/Unknown4505/Library-System/issues/38)) | NEW FEATURE | `@lehuukhang` | #37 bàn giao contract/API thật; UI/loading/empty/fallback có thể chuẩn bị trước | Startup vào Idle Ads; DTO/message/null/empty/partial xử lý theo docs 04; touch sang `SearchPage`; inactivity 60s; slideshow 8s configurable; cache/retry/timer cleanup; không gián đoạn giao dịch | **PLANNED / NOT RUN** |
| FE — Receipt Navigation UX Change ([#38](https://github.com/Unknown4505/Library-System/issues/38)) | UX CHANGE, không phải regression so với thiết kế cũ | `@lehuukhang` | Receipt UI/PDF [#29](https://github.com/Unknown4505/Library-System/issues/29); tránh sửa chồng `ReceiptViewModel` | Chỉ sau `Paid` và hoàn tất Receipt; giữ mốc 5s hiện hành; giữ dữ liệu in/PDF an toàn; clear session/cart; về `SearchPage`; không cancel Paid; sau 60s mới về Idle Ads | **PLANNED / NOT RUN** |
| BE Advertising API ([#37](https://github.com/Unknown4505/Library-System/issues/37)) | API GAP | `@thien33` | Contract V1 đã được Leader duyệt; phối hợp #38 và không mở rộng sang Checkout ProductDiscount | Endpoint/wrapper/auth/DTO đúng contract; new arrivals/best sellers đúng rule; message/error/empty/partial/503; lọc ưu đãi chưa được Checkout hỗ trợ; không thêm schema `Advertisements` | **APPROVED CONTRACT / IMPLEMENTATION PLANNED / NOT RUN** |

Rule QA: chỉ đánh dấu `[x]` khi toàn bộ scope tương ứng có evidence PASS, acceptance hoàn thành và dependencies đã thỏa mãn. Nếu chỉ một subtask PASS thì cập nhật subtask, không đánh dấu toàn feature. #37 chỉ sửa Backend; #38 chỉ sửa Kiosk FE; #29 giữ riêng Receipt UI/PDF và phải phối hợp file ownership với #38.

- **4.5 giữ chưa hoàn thành:** HTTP smoke trên merge không commit `main@fbc15a2` + `Khang@d9de651` đã PASS pagination, keyword, category, detail, barcode hợp lệ/404 và dữ liệu out-of-stock. WPF chưa chạy tương tác; FE-13 còn race pagination và Backend vẫn trả sách inactive qua detail/barcode.
- **5.5 giữ chưa hoàn thành:** Đã chạy bộ test trên SQL Server Express local bằng database cô lập. Constraints, rollback, concurrent last-copy, cancel và promotion+100 điểm PASS; duplicate webhook, 4 validation điểm và OrderCode uniqueness FAIL. Checkout idempotency/cleanup race vẫn BLOCKED bởi Issue #19/#20.
- **7.3 giữ chưa hoàn thành:** Contract xác thực SePay phải được đối chiếu chính xác với cấu hình/tài liệu gateway và regression theo Issue #20; static code review không phải runtime evidence.
- **7.6 giữ chưa hoàn thành:** HTTP smoke đã PASS Checkout→Pending→QR→polling→Cancel và đối chiếu reserve 0→1→0; F3/Receipt đều trả 404, chưa có SePay Test Mode/Paid/Receipt E2E.
- **9.1 giữ chưa hoàn thành:** Leader đã bổ sung Category/Area CRUD dùng shared DTO, wrapper và role Admin; SQL contract tests PASS. Books write endpoints hiện chưa có authorization và validation wrapper toàn cục chưa được nghiệm thu, nên chưa thể đánh dấu hoàn thành.
- **11.5 giữ chưa hoàn thành:** Shared Reports DTO/contract đã chốt và serialization PASS; implementation vẫn trả `object`, `ReportsController` hiện giới hạn role Admin nhưng chưa có runtime authorization evidence và PR #11 chưa có CMS chart/status integration.
- **12.1 giữ chưa hoàn thành:** Kế hoạch và kết quả component SQL đã cập nhật tại docs/11-end-to-end-test-plan.md; chưa chạy E2E đầy đủ với Kiosk WPF và SePay Test Mode.
- **12.2 giữ chưa hoàn thành:** Artifact BFD/DFD 0/1/2 tồn tại trong `diagram/`; BFD và DFD Level 1 cùng bảy nhóm chức năng, DFD Level 2 khớp luồng checkout/payment. DFD Level 1 còn thiếu data store/luồng xác thực `Users` và bộ sơ đồ chưa phân biệt rõ chức năng đã triển khai với phần Planned/BLOCKED.
- **12.3 giữ trạng thái hoàn thành:** ERD đã được đối chiếu có đúng 17 entity, không thêm bảng ngoài model và có quan hệ nullable `Kiosks.AreaId -> Areas.AreaId` đúng cấu hình `SET NULL`.

---

### Handoff ProductDiscount V7.1 — LEADER FOUNDATION / IMPLEMENTATION PLANNED

| Workstream | Owner | Scope | Acceptance | Trạng thái |
|---|---|---|---|---|
| Leader domain/schema/shared DTO/docs | Leader | Snapshot properties, EF mapping, CRUD/Checkout/Receipt DTO contract, stacking rules, migration gate và QA contract | Build + serialization/schema consistency; không sửa runtime calculation | **READY FOR REVIEW / MIGRATION READY** |
| BE-01 Promotion CRUD | `@thien33` | Persist OrderDiscount/ProductDiscount, validation, PUT replace-all, auth/message | Transactional replace, no duplicate BookId, type/detail consistency | **PLANNED / NOT RUN** |
| BE-02 Promotion calculation & Checkout | `@thien33` | Kiosk best benefit/tie-break, POS manual, points, zero-payable rollback, snapshot persistence | SQL/contract tests cho totals, stock, points và QR amount | **PLANNED / NOT RUN** |
| BE-03 Receipt API | `@thien33` | Snapshot-first mapping và legacy fallback | Paid-only receipt, không recalculate, regression #28 | **PLANNED / NOT RUN** |
| FE-01 CMS Promotion Management | `@lehuukhang` | Chọn type, OrderDiscount, nhiều ProductDiscount book rows | Backend-authoritative validation/message; không hardcode | **PLANNED / NOT RUN** |
| FE-02 Kiosk/POS Promotion Display | `@lehuukhang` | Hiển thị promotion/totals/item breakdown từ Backend | Không tự tính final price; xử lý loading/empty/error | **PLANNED / NOT RUN** |
| FE-03 Receipt/PDF | `@lehuukhang` | Promotion metadata và line discount từ Receipt API | Không dùng cart local; regression #29 | **PLANNED / NOT RUN** |
| Integration QA | Leader | Contract, SQL, E2E, legacy, cancel/stock/points/payment regression | Evidence thật trước khi mở quảng cáo ProductDiscount | **PLANNED / NOT RUN** |

Dependencies: BE-01 → BE-02 → BE-03; FE-01 phụ thuộc BE-01; FE-02 phụ thuộc BE-02; FE-03 phụ thuộc BE-03/#28/#29. Advertising #37/#38 chỉ được mở ProductDiscount sau Checkout E2E PASS.

## 4. Lưu ý Phụ thuộc & Trình tự thực hiện (Dependencies)

Để đảm bảo dự án diễn ra đúng tiến độ, tránh việc thành viên này "ngồi chơi" chờ thành viên khác, toàn bộ team phải tuân thủ nghiêm ngặt **Luồng tuần tự (Sequential)** và **Luồng song song (Parallel)** dưới đây:

### 4.1 Luồng tuần tự (Bắt buộc phải đợi nhau)

Luồng nghiệp vụ lõi phải được xây dựng theo đúng thứ tự (Module trước làm nền tảng cho Module sau):

```text
[M0] Setup & Database (Leader)
      │
      ├──> [M1, M2] Core API Sách & User (BE) 
      │           │
      │           └──> [M5] API Checkout & Giữ kho (BE)
      │                       │
      │                       └──> [M7] Webhook Thanh toán (BE)
      │
      └──> [M3, M4] Kiosk UI Điều hướng & Tra cứu (FE)
                  │
                  └──> [M6] Kiosk Checkout UI & Polling (FE)
                              │
                              └──> [M8] Tích hợp phần cứng Kiosk (FE)
```
*Ghi chú: [M9, M10, M11] CMS Web có thể bắt đầu làm bất cứ lúc nào sau khi [M0] hoàn thành.*

### 4.2 Luồng song song (Quy tắc Vàng - KHÔNG ĐỢI NHAU)

- **Frontend (@lehuukhang)** KHÔNG CẦN CHỜ **Backend (@thien33)** viết xong API. 
- Ngay khi `M0` chốt xong Database và API Contract (ở file `04-api-reference.md`), FE phải lập tức thiết kế WPF và CMS MVC bằng **Dữ liệu giả (Mock Data/Fake JSON)**. 
- BE (@thien33) cứ code API và tự test độc lập bằng Postman.
- **[QUAN TRỌNG] Tích hợp:** Sau khi BE xong API và FE xong UI, **Leader (@Unknown4505)** sẽ là người hỗ trợ "Ráp" hai mảnh này lại với nhau (gọi HttpClient, map DTO, test URL, fix CORS).

---

### 5. QUY TẮC LƯU TRỮ VÀ XỬ LÝ ĐƯỜNG DẪN ẢNH (Image URL)
Để tránh tình trạng lỗi đường dẫn (Conflict 404) khi các thành viên (nhất là FE Khang và BE Thiện) pull code của nhau về máy tính cá nhân:
1. **Lưu database (Entity `Book.ImageUrl`):**
   - **Tối kỵ:** KHÔNG ĐƯỢC lưu đường dẫn tuyệt đối (VD: `C:/images/demen.jpg` hay `https://localhost:7111/uploads/...`).
   - **Bắt buộc:** Chỉ lưu đường dẫn tương đối, luôn bắt đầu bằng `/uploads/`. VD: `/uploads/books/demen.jpg`.
2. **Thư mục chứa ảnh vật lý (wwwroot):**
   - Đã cấu hình `app.UseStaticFiles()` trong API.
   - Thư mục vật lý nằm tại: `BookKiosk.API/wwwroot/uploads/books/`. (Đã tạo sẵn file `.gitkeep` để ae clone về tự có folder này).
3. **Khi FE hiển thị ảnh:**
   - Kiosk UI hoặc CMS chỉ cần ghép Base URL của API với đường dẫn trong DB. 
   - VD: `<img src="https://localhost:7111" + book.ImageUrl />`

