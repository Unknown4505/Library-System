# Kiến trúc & Tổng quan — BookKiosk

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

> **Đọc file này SAU `00-how-to-use-docs.md` và TRƯỚC khi đọc bất kỳ file nào khác.**
> File này cho bạn bức tranh toàn cảnh hệ thống trước khi đi vào chi tiết từng phần.

---

## 1. Mục tiêu hệ thống

| Mục tiêu | Mô tả |
|---|---|
| **Tự phục vụ** | Khách hàng tự tìm, tự quét, tự thanh toán tại Kiosk — không cần nhân viên |
| **Quản lý tập trung** | Thủ thư / Admin quản lý sách, kho, hóa đơn, báo cáo qua Web Admin |
| **Nhất quán dữ liệu** | Tồn kho không bao giờ bị âm dù nhiều điểm bán hoạt động đồng thời |
| **Chịu lỗi** | Mất mạng, mất camera, webhook trễ đều có kịch bản xử lý rõ ràng |

---

## 2. Phạm vi MVP `[MVP]`

### Trong phạm vi — phải làm
- Tra cứu sách theo tên, tác giả, thể loại; xem vị trí kệ trên bản đồ
- **Hiển thị tồn kho thực tế** (còn hàng / hết hàng) trên kết quả tìm kiếm và trang chi tiết sách
- **Gợi ý sách trên Kiosk** — 3 dạng: tìm kiếm nhiều nhất, bán chạy nhất, mới nhập kho
- Quét mã vạch / QR bằng webcam laptop, thêm vào giỏ hàng
- **`[DEV-ONLY]`** Ô nhập mã thủ công trên **cả Kiosk và CMS** — chỉ hiển khi `ASPNETCORE_ENVIRONMENT=Development`
- **Thẻ thành viên & tích điểm** — đăng ký/tra cứu bằng số điện thoại, tích điểm khi mua, dùng điểm khi thanh toán tại Kiosk
  - Tích: mỗi **10,000đ** chi tiêu = **1 điểm** (làm tròn xuống, tính trên tiền thực trả sau KM)
  - Dùng: **1 điểm = 1,000đ** giảm giá — tối đa **100 điểm (= 100,000đ)** / đơn, không có tối thiểu
  - Vẫn tích điểm trên phần tiền thực trả khi dùng điểm trong cùng đơn
- **Chương trình khuyến mãi** — Admin tạo trên CMS; tự động apply khi checkout Kiosk; nhân viên bấm nút áp KM trên POS quầy
- Thanh toán QR (SePay + ngrok) tại Kiosk
- Hiển thị hóa đơn điện tử sau thanh toán; xuất PDF bằng **QuestPDF**
- Bán tại quầy (POS) trên Web Admin — tiền mặt hoặc QR
- Nhập kho, quản lý tồn, cảnh báo sách sắp hết
- Giám sát Kiosk: heartbeat, trạng thái online/offline, báo sự cố
- Dashboard: doanh thu, top sách, so sánh Kiosk vs quầy

### Ngoài phạm vi — để giai đoạn sau `[LATER]`
- Mượn / trả sách
- In hóa đơn nhiệt (cần máy in vật lý)
- Recommendation engine nâng cao (AI/ML-based)
- Mã giảm giá (voucher code) — không có kênh phân phối mã cho khách trong MVP

---

## 3. Actors (Tác nhân)

| Tác nhân | Mô tả | Giao diện | Xác thực |
|---|---|---|---|
| **Khách hàng** | Mua sách tại Kiosk, không đăng nhập. Có thể nhập SĐT để dùng thẻ thành viên (tùy chọn) | Kiosk WPF | Không |
| **Thẻ thành viên (Member)** | Đăng ký tại quầy bằng SĐT, tra cứu bằng SĐT khi thanh toán Kiosk / POS | Kiosk + CMS | Không (tra bằng SĐT) |
| **Thủ thư** | Bán tại quầy, tra cứu, đăng ký Member, xử lý sự cố Kiosk | Web Admin | JWT — Role `Staff` |
| **Admin** | Quản lý toàn bộ: sách, kho, nhân viên, báo cáo, KM | Web Admin | JWT — Role `Admin` |
| **Kiosk (thiết bị)** | Gửi heartbeat, báo lỗi phần cứng | Backend API | API Key riêng từng máy |
| **SePay** | Gửi webhook xác nhận tiền đã về | Backend API | HMAC signature |

---

## 4. Kiến trúc tổng thể

```
┌──────────────────────────────────┬──────────────────────────────────────┐
│  Kiosk App (WPF)                 │  Web Admin (ASP.NET Core MVC)        │
│  - Tìm sách (tên/thể loại)        │  - Dashboard doanh thu               │
│  - Xem tồn kho (còn/hết)        │  - Quản lý sách, kho, nhân viên      │
│  - Gợi ý: bán chạy/mới/được TK     │  - POS bán tại quầy                  │
│  - Xem vị trí kệ sách            │  - Thẻ thành viên & giảm giá        │
│  - Quét mã vạch (webcam)         │  - Giám sát Kiosk                    │
│  - [DEV] Nhập mã thủ công        │  - [DEV] Nhập mã thủ công (POS)     │
│  - Thẻ TV / điểm / mã giảm giá   │  - Xuất báo cáo Excel/PDF            │
│  - Thanh toán QR                 │                                      │
│  - Xuất hóa đơn PDF (QuestPDF)   │                                      │
└──────────────┬───────────────────┴────────────────┬─────────────────────┘
               │              HTTPS / REST           │
               │            Bearer JWT / API Key     │
               └──────────────────┬──────────────────┘
                                  ▼
                  ┌────────────────────────────────────┐
                  │  ASP.NET Core Web API (.NET 8)     │
                  │                                    │
                  │  Controllers                       │
                  │      ↓ validate, authorize         │
                  │  Services  (business logic)        │
                  │      ↓ transaction, EF Core        │
                  │  Repositories                      │
                  │      ↓                             │
                  │  SQL Server (EF Core Code-First)   │
                  └──────────────┬─────────────────────┘
                                 │  webhook (POST)
                                 ▼
                  ┌──────────────────────────────┐
                  │  SePay                       │
                  │  (cổng thanh toán QR)        │
                  │  ngrok tunnel → localhost    │  ← chỉ khi dev/demo local
                  └──────────────────────────────┘
```

### Nguyên tắc kiến trúc cốt lõi

> 5 nguyên tắc này phải được giữ xuyên suốt project. Không ngoại lệ.

**1. Một nguồn sự thật — Backend là trung tâm**
- Mọi logic: tính tiền, trừ kho, kiểm tra quyền → chỉ nằm ở Backend.
- Frontend (Kiosk, CMS) chỉ hiển thị dữ liệu Backend trả về. Không tự tính.

**2. CMS + API là 1 process** ✅ chốt
- `BookKiosk.CMS` và `BookKiosk.API` chạy chung trong 1 `dotnet run`.
- CMS Controller inject `IService` trực tiếp — không gọi HTTP nội bộ.
- Port chuẩn: `http://localhost:5000` | `https://localhost:5001`
- Swagger: `https://localhost:5001/swagger` (chỉ hiển khi `env=Development`)

**3. Kiosk là client “mỏng”**
- Không kết nối trực tiếp vào Database.
- Không giữ giá, tồn kho ở local (trừ cache tra cứu ngắn hạn).
- Mọi quyết định thanh toán đều đến từ Backend.
- Kiosk gọi API tại: `https://localhost:5001/api/`

**4. Idempotent — gọi lặp không gây hại**
- Webhook SePay gửi 2 lần → kho chỉ trừ 1 lần.
- `checkout` gọi 2 lần với cùng giỏ → chỉ tạo 1 đơn.
- Cơ chế: `unique index` trên `ReferenceCode` trong bảng `PaymentTransactions`.

**5. Chịu lỗi có kịch bản**
- Mất mạng → Kiosk hiển thị “Tạm thời không thể thanh toán”, vẫn cho tra cứu từ cache.
- Mất camera → báo bảo trì, gửi incident lên Backend, tự thử kết nối lại.
- Webhook trễ → đơn vào trạng thái `NeedsReview`, xử lý thủ công.

---

## 5. Công nghệ đã chốt ✅

| Thành phần | Công nghệ | Lý do chọn |
|---|---|---|
| **Backend API** | ASP.NET Core Web API (.NET 8) | Cùng stack C# với toàn nhóm |
| **ORM / DB access** | Entity Framework Core (Code-First) | Migrations tự động, không viết SQL thủ công |
| **CSDL** | SQL Server Express (LocalDB khi dev) | Hỗ trợ transaction, RowVersion, index tốt |
| **Xác thực** | JWT (access 30 phút + refresh token) | Chuẩn ngành, stateless |
| **Web Admin** | ASP.NET Core MVC + Razor Pages | Cùng stack .NET, không cần học JS framework |
| **Kiosk App** | WPF (.NET 8) + MVVM | Hỗ trợ cảm ứng, DPI scale, XAML quen thuộc |
| **Quét mã vạch** | ZXing.Net.Bindings.Windows.Compatibility | Chạy được trên webcam laptop, không cần USB scanner |
| **Hóa đơn PDF** | QuestPDF | Thuần .NET, không cần Office/Acrobat, miễn phí cho đồ án |
| **Thanh toán QR** | SePay | Sandbox dễ setup, webhook rõ ràng |
| **Tunnel webhook local** | ngrok | Miễn phí, 1 lệnh là có HTTPS public URL |
| **Log** | Serilog (file + Console) | Structured logging, dễ trace lỗi |
| **Cache** | `IMemoryCache` | Giảm tải tra cứu danh mục, bản đồ kệ |
| **Xuất báo cáo** | ClosedXML (Excel) + QuestPDF (PDF) | Thuần .NET, không cần Office |
| **Dependency Injection** | Built-in .NET DI | Không cần Autofac, đủ dùng cho đồ án |

---

## 6. Cấu trúc Solution

```
BookKiosk.sln
│
├── BookKiosk.API/                  ← [MVP] Web API entry point
│   ├── Controllers/                   AuthController, BookController, OrderController,
│   │                                  PaymentController, KioskController, ReportController
│   ├── Middleware/                    GlobalExceptionHandler, RequestLogging
│   └── Program.cs                     Cấu hình DI, Swagger, CORS, Auth, Serilog
│
├── BookKiosk.Application/          ← [MVP] Business logic layer
│   ├── Services/                      CheckoutService, PaymentService, BookService,
│   │                                  InventoryService, ReportService, KioskService,
│   │                                  MemberService,                     ← đăng ký, tra điểm, tích điểm
│   │                                  DiscountService,                   ← validate mã, tính giảm
│   │                                  RecommendationService              ← top-search, top-sell, newest
│   ├── DTOs/                          Request & Response DTO cho từng client
│   ├── Interfaces/                    IBookRepository, ICheckoutService, IMemberService, ...
│   └── Validators/                    FluentValidation cho từng request DTO
│
├── BookKiosk.Domain/               ← [MVP] Entities & Enums (không phụ thuộc gì)
│   ├── Entities/                      Book, Category, Area, Order, OrderDetail,
│   │                                  PaymentTransaction, StockReceipt, StockHistory,
│   │                                  User, RefreshToken, Kiosk, KioskIncident,
│   │                                  Member, PointTransaction,          ← Thẻ thành viên
│   │                                  Discount, DiscountUsage             ← Giảm giá
│   └── Enums/                         OrderStatus, SaleChannel, PaymentMethod,
│                                      StockChangeType, UserRole, KioskStatus,
│                                      DiscountType                        ← Percent | FixedAmount
│
├── BookKiosk.Infrastructure/       ← [MVP] Data access & external services
│   ├── Data/                          BookKioskDbContext, EF Configurations
│   ├── Repositories/                  Implementations của IRepository
│   ├── Payment/                       SePayClient, WebhookHandler
│   └── Pdf/                           ReceiptPdfService (QuestPDF)
│
├── BookKiosk.Kiosk/                ← [MVP] WPF Desktop App
│   ├── Pages/                         IdlePage, HomePage, SearchPage, BookDetailPage,
│   │                                  CartPage, PaymentPage, ReceiptPage, MaintenancePage,
│   │                                  MemberLoginPage,                   ← nhập SĐT tra thẻ
│   │                                  RecommendationPage                 ← gợi ý sách (home)
│   ├── ViewModels/                    ViewModel cho từng Page
│   ├── Services/                      ApiClient, CameraService, IdleTimerService,
│   │                                  KioskHealthService, PdfViewerService
│   └── App.xaml / App.xaml.cs
│
└── BookKiosk.CMS/                  ← [MVP] ASP.NET Core MVC Web Admin
    ├── Controllers/                   DashboardController, BookController, InventoryController,
    │                                  PosController, OrderController, KioskController,
    │                                  ReportController, UserController,
    │                                  MemberController,                  ← danh sách TV, điểm, lịch sử
    │                                  DiscountController                 ← tạo/vô hiệu mã giảm giá
    ├── Views/                         .cshtml tương ứng từng Controller
    ├── ViewModels/                    ViewModel cho từng View
    └── wwwroot/                       CSS, JS tĩnh
```

---

## 7. Luồng chính (tóm tắt)

### 7.1 Khách mua sách tại Kiosk
```
Chạm màn hình → Home (hiển gợi ý: bán chạy / mới / được tìm nhiều)
  ├─► Tra cứu: tìm theo tên/thể loại → xem chi tiết → xem vị trí kệ trên bản đồ
  └─► Self-Checkout:
        Quét mã vạch (webcam) ─────────────────────────────────────────┐
        HOẶC nhập mã thủ công [DEV-ONLY] ─── chỉ hiện khi env=Dev ──────┤
                                                                         ↓
        → thêm giỏ hàng (Backend validate tồn kho, hiển thị còn/hết)
        → Xác nhận thanh toán
        → Màn hình ưu đãi (tùy chọn, có thể bỏ qua):
              ┌─ Nhập SĐT thẻ thành viên (không bắt buộc)
              │   Nếu tìm thấy: hiển tên + điểm hiện có
              │   Chọn số điểm muốn dùng (tối đa 100 điểm = 100,000đ)
              └─ Chương trình KM đang active → Backend tự apply, không cần nhập mã
        → Tổng kết:
              Giá gốc:        X
              Dùng điểm TV:  -Y  (nếu có)
              KM đang active: -Z  (nếu có)
              THỰC THANH TOÁN: X - Y - Z
        → Backend giữ chỗ tồn kho (transaction)
        → Hiển thị QR SePay + đếm ngược 3 phút
        → Khách chuyển tiền bằng app ngân hàng
        → SePay gọi webhook → Backend xác thực → chốt kho + tích điểm cho member
        → Kiosk poll thấy "Đã thanh toán"
        → Màn hình hóa đơn: mã đơn, danh sách, tổng tiền, điểm vừa tích
        → Nút “Lưu PDF” → mở file PDF
        → 10 giây → về Idle
```

### 7.2 Bán tại quầy (POS)
```
Thủ thư đăng nhập Web Admin → POS
  → Quét mã vạch (USB scanner) hoặc nhập thủ công [DEV]
  → Giỏ hàng
  → Hỏi SĐT thành viên (tùy chọn)
        Nếu có: hiển điểm, chọn dùng điểm (giống Kiosk)
  → KM đang active → nút "Áp KM" (bấm thủ công, không tự apply)
  → Tổng kết → Thu tiền mặt hoặc QR
  → POST /api/orders/counter → Backend trừ kho ngay trong transaction
  → Tích điểm cho member (nếu có) → hiển hóa đơn
```

### 7.3 Timeout & nhả kho
```
Kiosk không thanh toán trong 3 phút (HanThanhToan)
  → Background Service quét mỗi 30 giây
  → Đổi đơn sang Cancelled
  → Nhả SoLuongGiuCho về kho
  → Kiosk nhận trạng thái Cancelled → hiển thị "Hết hạn" → về Idle
```

---

## 8. Demo trên 1 Laptop — Điểm cần lưu ý

> Kiến trúc giữ nguyên. Chỉ khác ở cách triển khai.

| Hạng mục | Môi trường thật | Khi demo trên laptop |
|---|---|---|
| Nhập mã thủ công | Không có | `[DEV-ONLY]` — ô text trên Kiosk và POS/CMS, chỉ hiện khi `env=Development` |
| Camera | USB scanner riêng | Webcam tích hợp (chọn camera index đúng) |
| Chất lượng quét | Tốt | In mã to, giữ cách 15–25 cm, đủ sáng |
| Webhook SePay | Server public | ngrok tunnel → `localhost:5001` |
| Thanh toán QR thật | Điện thoại quét màn hình laptop | Không xung đột webcam vì là thiết bị khác |
| HTTPS | Chứng chỉ thật | `dotnet dev-certs https --trust` |
| CSDL | SQL Server | SQL Server Express / LocalDB |
| Kiosk mode Windows | Assigned Access | Chạy WPF fullscreen (F11-style), bỏ qua lockdown |
| Giả lập thanh toán | — | Nút `[DEV-ONLY]` "Giả lập tiền về" gọi đúng endpoint webhook |

---

## 9. Đọc tiếp theo

| Bạn muốn hiểu | Đọc file |
|---|---|
| Cấu trúc thư mục chi tiết, naming convention | `02-project-structure.md` |
| Thiết kế bảng CSDL, cột, index | `03-database.md` |
| API endpoint đầy đủ, request/response | `04-api-reference.md` |
| Luồng checkout, giữ chỗ kho, webhook | `05-business-pipeline.md` |
| Cài đặt chạy local, cấu hình ngrok + SePay | `06-local-setup.md` |
