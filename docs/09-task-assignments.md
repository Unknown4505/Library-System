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
| **9** | Tuần 9 | CMS Web: M9 (Quản lý Sách, Danh mục) | [ ] | [ ] | [ ] | CMS CRUD được sản phẩm |
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
| 0.1 | Khởi tạo Solution, 6 Projects theo kiến trúc 3 lớp | [x] | | | `[x]` |
| 0.2 | Tạo file Markdown Docs, CODEOWNERS, `.gitignore` | [x] | | | `[x]` |
| 0.3 | Code toàn bộ Domain Entities & Enums | | [ ] | | `[ ]` |
| 0.4 | Code Fluent API Configurations cho từng Entity | | [ ] | | `[ ]` |
| 0.5 | Chạy `Add-Migration` & Cập nhật Database | | [ ] | | `[ ]` |
| 0.6 | **[Leader]** Setup Dependency Injection (DI) & Serilog (Log lỗi) chung cho hệ thống | [x] | | | `[ ]` |

### MODULE 1: BACKEND API - SÁCH & DANH MỤC
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 1.1 | Thiết lập `GlobalExceptionHandlerMiddleware` | | [ ] | | `[ ]` |
| 1.2 | API: `GET /api/books` (Có phân trang, tìm kiếm) | | [ ] | | `[ ]` |
| 1.3 | API: `GET /api/books/barcode/{barcode}` | | [ ] | | `[ ]` |
| 1.4 | API: `GET /api/categories` và `GET /api/areas` | | [ ] | | `[ ]` |
| 1.5 | Cấu hình Swagger JWT & API Key | | [ ] | | `[ ]` |
| 1.6 | **[Leader]** Khai báo các Shared DTOs và Constants dùng chung cho BE và FE | [x] | | | `[ ]` |

### MODULE 2: BACKEND API - THÀNH VIÊN & ĐIỂM
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 2.1 | API: `GET /api/members/{phoneNumber}` (Tra SĐT) | | [ ] | | `[ ]` |
| 2.2 | API: Cập nhật, tạo mới Thành viên | | [ ] | | `[ ]` |
| 2.3 | API: `GET /api/members/{id}/point-history` | | [ ] | | `[ ]` |

### MODULE 3: KIOSK WPF - ĐIỀU HƯỚNG & TRANG CHỦ
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 3.1 | Thiết lập MVVM (BaseViewModel, RelayCommand) | | | [ ] | `[ ]` |
| 3.2 | Thiết lập `NavigationService` (Quản lý Frame) | | | [ ] | `[ ]` |
| 3.3 | Layout `IdlePage`: Màn hình chờ + Video QC | | | [ ] | `[ ]` |
| 3.4 | Cấu hình `IdleTimerService` (Quay về màn hình chờ) | | | [ ] | `[ ]` |
| 3.5 | Layout `HomePage`: Slider Sách bán chạy / Mới | | | [ ] | `[ ]` |
| 3.6 | **[Leader]** Setup HttpClientFactory / ApiClient thuần túy để FE kiểm soát hoàn toàn API | [x] | | | `[ ]` |

### MODULE 4: KIOSK WPF - TÌM KIẾM & CHI TIẾT
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 4.1 | Layout `SearchPage`: Lưới sách + Load More | | | [ ] | `[ ]` |
| 4.2 | Logic Filter theo Danh mục, Tác giả | | | [ ] | `[ ]` |
| 4.3 | Layout `BookDetailPage`: Chi tiết + Vị trí kệ | | | [ ] | `[ ]` |
| 4.4 | Kiểm tra Tồn kho trước khi cho thêm vào giỏ | | | [ ] | `[ ]` |
| 4.5 | **[Leader]** Ghép nối: Đổi từ Mock Data Data giả sang gọi API thật (Phần Search & Detail) | [x] | | | `[ ]` |

### MODULE 5: BACKEND API - CHECKOUT & GIỮ KHO
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 5.1 | API: `POST /api/orders/kiosk/checkout` | | [ ] | | `[ ]` |
| 5.2 | Logic: Tự động quét và áp dụng Promotion (KM) | | [ ] | | `[ ]` |
| 5.3 | Logic: Trừ tiền điểm (Points), tính TotalAmount | | [ ] | | `[ ]` |
| 5.4 | Logic: Cập nhật `ReservedQuantity` & Lưu Order | | [ ] | | `[ ]` |
| 5.5 | **[Leader]** Viết test kịch bản tính điểm và áp KM, fix bug Concurrency | [x] | | | `[ ]` |

### MODULE 6: KIOSK WPF - GIỎ HÀNG & THANH TOÁN
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 6.1 | Layout `CartPage`: Hiển thị sách, Tăng giảm SL | | | [ ] | `[ ]` |
| 6.2 | Bàn phím ảo (Numpad) nhập SĐT quy đổi điểm | | | [ ] | `[ ]` |
| 6.3 | Khang gọi API Checkout, nhận OrderCode sang Payment | | | [ ] | `[ ]` |
| 6.4 | `PaymentPage`: Hiện bảng tóm tắt tiền + Đếm ngược | | | [ ] | `[ ]` |

### MODULE 7: PAYMENT - SEPAY & WEBHOOK
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 7.1 | API: Sinh link/ảnh QR VietQR theo chuẩn SePay | | [ ] | | `[ ]` |
| 7.2 | API: `POST /api/payments/sepay-webhook` | | [ ] | | `[ ]` |
| 7.3 | Logic: Xác thực HMAC Signature bảo mật | | [ ] | | `[ ]` |
| 7.4 | Logic: Cập nhật Paid, trừ kho vật lý (StockQuantity)| | [ ] | | `[ ]` |
| 7.5 | Kiosk `PaymentPage`: Polling API Status mỗi 3s | | | [ ] | `[ ]` |
| 7.6 | **[Leader]** Ghép nối: Viết logic Polling cho FE để tự động chuyển trang khi BE nhận được tiền (SignalR hoặc Timer) | [x] | | | `[ ]` |

### MODULE 8: KIOSK WPF - THIẾT BỊ PHẦN CỨNG
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 8.1 | Kiosk: Gắn UI xử lý dữ liệu từ Camera/Máy quét | | | [ ] | `[ ]` |
| 8.2 | Kiosk: Sinh file PDF Hóa đơn với QuestPDF | | | [ ] | `[ ]` |
| 8.3 | Kiosk: `MaintenancePage` Báo lỗi thiết bị, khóa Kiosk | | | [ ] | `[ ]` |
| 8.4 | **[Leader]** Khởi tạo Interface phần cứng (`IBarcodeScanner`, `IPrinter`) và Class Mock để Khang test UI lúc dev | [x] | | | `[ ]` |
| 8.5 | **[Leader]** Triển khai gọi Driver ESC/POS in nhiệt thật | [x] | | | `[ ]` |
| 8.6 | HeartbeatService: `POST /api/kiosk/heartbeat` | | | [ ] | `[ ]` |

### MODULE 9: CMS WEB - QUẢN LÝ SẢN PHẨM
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 9.1 | Admin API: CRUD Sách, Danh mục, Khu vực | | [ ] | | `[ ]` |
| 9.2 | Layout Dashboard (SB Admin / Bootstrap) | | | [ ] | `[ ]` |
| 9.3 | Trang Quản lý Sách (DataTables / Grid) | | | [ ] | `[ ]` |
| 9.4 | Form Thêm/Sửa sách (Upload ảnh) | | | [ ] | `[ ]` |
| 9.5 | **[Leader]** Cấu hình CORS Policy để Web Admin có thể gọi được Backend API | [x] | | | `[ ]` |

### MODULE 10: CMS WEB - TỒN KHO, KM & POS
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 10.1 | Admin API: CRUD Khuyến mãi, Nhập kho | | [ ] | | `[ ]` |
| 10.2 | Trang Nhập kho (Inventory) | | | [ ] | `[ ]` |
| 10.3 | Trang Quản lý Khuyến mãi | | | [ ] | `[ ]` |
| 10.4 | Trang POS Bán tại quầy (Áp mã thủ công) | | | [ ] | `[ ]` |

### MODULE 11: THỐNG KÊ DOANH THU
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 11.1 | API: `GET /api/reports/revenue` (Group by Tháng) | | [ ] | | `[ ]` |
| 11.2 | API: `GET /api/reports/top-books` | | [ ] | | `[ ]` |
| 11.3 | CMS: Vẽ Biểu đồ Bar Chart Doanh thu | | | [ ] | `[ ]` |
| 11.4 | CMS: Biểu đồ Pie Chart Trạng thái Kiosk | | | [ ] | `[ ]` |
| 11.5 | **[Leader]** Ghép nối data thống kê giữa BE & FE | [x] | | | `[ ]` |

### MODULE 12: KIỂM THỬ, BÁO CÁO & ĐÓNG GÓI
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Trạng thái |
|---|---|:---:|:---:|:---:|:---:|
| 12.1 | **[Leader]** Kịch bản Test End-to-End toàn luồng | [x] | | | `[ ]` |
| 12.2 | **[Leader]** Vẽ Sơ đồ Context Diagram, BFD, DFD | [x] | | | `[ ]` |
| 12.3 | **[Leader]** Vẽ ERD Database | [x] | | | `[ ]` |
| 12.4 | Viết Báo cáo Word Đồ án cuối kỳ | [x] | [ ] | [ ] | `[ ]` |

---

## 3. Tổng hợp khối lượng công việc

| Thành viên | Số đầu việc chính | Chức năng (Modules) phụ trách |
|---|---|---|
| **@Unknown4505** | ~18 | Kiến trúc, **Ghép nối FE-BE**, Cấu hình hệ thống, Support phần cứng ảo, Báo cáo đồ án |
| **@thien33** | ~28 | Code toàn bộ API, Database, Nghiệp vụ thanh toán, Webhook |
| **@lehuukhang** | ~32 | Thiết kế 100% UI, Binding dữ liệu Kiosk và CMS Web |

---

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
