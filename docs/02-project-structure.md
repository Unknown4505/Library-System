# Cấu trúc Project & Naming Convention — BookKiosk

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

> **Đọc sau `01-overview.md`. Thuộc naming convention ở mục 3 trước khi tạo file đầu tiên.**

---

## 1. Cấu trúc Solution tổng thể

```
BookKiosk/                              ← root của repo (git)
│
├── .github/
│   └── CODEOWNERS                      ← phân quyền review PR
│
├── docs/                               ← toàn bộ tài liệu (bạn đang đọc)
│
├── BookKiosk.sln                       ← Visual Studio solution file
│
├── BookKiosk.API/                      ← Web API entry point
├── BookKiosk.Application/              ← Business logic
├── BookKiosk.Domain/                   ← Entities & Enums (core)
├── BookKiosk.Infrastructure/           ← Data access & external services
├── BookKiosk.Kiosk/                    ← WPF Desktop App
└── BookKiosk.CMS/                      ← ASP.NET Core MVC Web Admin
```

### Nguyên tắc phụ thuộc giữa các project

```
BookKiosk.API ──────────────────────────────────────────┐
BookKiosk.CMS ──────────────────────────────────────────┤
BookKiosk.Kiosk ────────────────────────────────────────┤
                                                        ↓
                              BookKiosk.Application ────┤
                                                        ↓
                              BookKiosk.Infrastructure ──┤
                                                        ↓
                              BookKiosk.Domain ──────────┘
```

> ⚠️ **Quy tắc bất biến:** `BookKiosk.Domain` không được `using` bất kỳ project nào khác.
> `BookKiosk.Application` không được `using` Infrastructure trực tiếp — chỉ qua Interface.

---

## 2. Chi tiết từng Project

### 2.1 BookKiosk.Domain — Tầng Domain (core, không phụ thuộc gì)

```
BookKiosk.Domain/
│
├── Entities/
│   ├── Book.cs
│   ├── Category.cs
│   ├── Area.cs                         ← khu vực kệ sách (để Kiosk chỉ đường)
│   ├── Order.cs
│   ├── OrderDetail.cs
│   ├── PaymentTransaction.cs
│   ├── StockReceipt.cs
│   ├── StockReceiptDetail.cs
│   ├── StockHistory.cs                 ← sổ cái kho, mọi biến động đều ghi
│   ├── User.cs
│   ├── RefreshToken.cs
│   ├── Kiosk.cs
│   ├── KioskIncident.cs
│   ├── Member.cs                       ← thẻ thành viên
│   ├── PointTransaction.cs             ← lịch sử tích/dùng điểm
│   ├── Promotion.cs                    ← chương trình khuyến mãi
│   └── PromotionUsage.cs               ← lịch sử apply KM theo đơn
│
└── Enums/
    ├── OrderStatus.cs                  ← Pending | Paid | Cancelled | NeedsReview
    ├── SaleChannel.cs                  ← Kiosk | Counter
    ├── PaymentMethod.cs                ← QR | Cash
    ├── StockChangeType.cs              ← Import | Sale | Adjustment | Cancellation
    ├── UserRole.cs                     ← Admin | Staff
    ├── KioskStatus.cs                  ← Online | Offline | Error
    ├── PromotionType.cs                ← Percent | FixedAmount
    └── PointTransactionType.cs         ← Earned | Redeemed
```

**Quy tắc Entity:** mỗi Entity phải có ít nhất:
```csharp
public class Book
{
    public int BookId { get; set; }          // PK: {ClassName}Id
    public DateTime CreatedAt { get; set; }  // audit
    public DateTime UpdatedAt { get; set; }  // audit
    // ... các property nghiệp vụ
}
```

---

### 2.2 BookKiosk.Application — Tầng Business Logic

```
BookKiosk.Application/
│
├── Services/
│   ├── BookService.cs
│   ├── CheckoutService.cs              ← nghiệp vụ trọng tâm: giữ chỗ kho, tính tiền
│   ├── PaymentService.cs               ← sinh QR, xử lý webhook SePay
│   ├── InventoryService.cs             ← nhập kho, điều chỉnh, lịch sử
│   ├── MemberService.cs                ← đăng ký, tra điểm, tích/dùng điểm
│   ├── PromotionService.cs             ← check KM active, tính giảm giá
│   ├── RecommendationService.cs        ← top-search, top-sell, newest
│   ├── ReportService.cs                ← doanh thu, top sách, KM vs quầy
│   └── KioskService.cs                 ← heartbeat, incident, config
│
├── DTOs/
│   ├── Book/
│   │   ├── BookKioskDto.cs             ← DTO cho Kiosk (không có giá vốn)
│   │   ├── BookAdminDto.cs             ← DTO cho CMS (đủ thông tin)
│   │   ├── BookCreateDto.cs
│   │   └── BookUpdateDto.cs
│   ├── Order/
│   │   ├── CheckoutRequestDto.cs
│   │   ├── CheckoutResponseDto.cs
│   │   ├── OrderDetailDto.cs
│   │   └── CounterSaleRequestDto.cs
│   ├── Payment/
│   │   ├── QrPaymentRequestDto.cs
│   │   ├── QrPaymentResponseDto.cs
│   │   └── SePayWebhookDto.cs
│   ├── Member/
│   │   ├── MemberLookupDto.cs          ← tra theo SĐT → trả điểm hiện có
│   │   ├── MemberRegisterDto.cs
│   │   └── PointRedeemRequestDto.cs
│   ├── Promotion/
│   │   ├── PromotionDto.cs
│   │   └── AppliedPromotionDto.cs      ← KM đã apply vào đơn
│   └── Common/
│       └── ApiResponseDto.cs           ← wrapper {success, code, message, data}
│
├── Interfaces/
│   ├── Repositories/
│   │   ├── IBookRepository.cs
│   │   ├── IOrderRepository.cs
│   │   ├── IMemberRepository.cs
│   │   └── IStockHistoryRepository.cs
│   └── Services/
│       ├── ICheckoutService.cs
│       ├── IPaymentService.cs
│       ├── IMemberService.cs
│       └── IPromotionService.cs
│
└── Validators/                         ← FluentValidation
    ├── CheckoutRequestValidator.cs
    ├── BookCreateValidator.cs
    └── MemberRegisterValidator.cs
```

---

### 2.3 BookKiosk.Infrastructure — Tầng Data & External

```
BookKiosk.Infrastructure/
│
├── Data/
│   ├── BookKioskDbContext.cs
│   └── Configurations/                 ← EF IEntityTypeConfiguration<T>
│       ├── BookConfiguration.cs
│       ├── OrderConfiguration.cs
│       ├── MemberConfiguration.cs
│       └── PromotionConfiguration.cs
│
├── Repositories/                       ← implements IRepository
│   ├── BookRepository.cs
│   ├── OrderRepository.cs
│   ├── MemberRepository.cs
│   └── StockHistoryRepository.cs
│
├── Payment/
│   ├── SePayClient.cs                  ← gọi SePay API sinh QR
│   └── SePayWebhookHandler.cs          ← xác thực chữ ký, xử lý webhook
│
├── Pdf/
│   └── ReceiptPdfService.cs            ← QuestPDF sinh hóa đơn PDF
│
└── Migrations/                         ← EF Core Migration files (tự sinh, không sửa tay)
```

---

### 2.4 BookKiosk.API — Web API Entry Point

```
BookKiosk.API/
│
├── Controllers/
│   ├── AuthController.cs               ← /api/auth
│   ├── BookController.cs               ← /api/books
│   ├── CategoryController.cs           ← /api/categories
│   ├── AreaController.cs               ← /api/areas
│   ├── OrderController.cs              ← /api/orders
│   ├── PaymentController.cs            ← /api/payments
│   ├── InventoryController.cs          ← /api/inventory
│   ├── MemberController.cs             ← /api/members
│   ├── PromotionController.cs          ← /api/promotions
│   ├── RecommendationController.cs     ← /api/recommendations
│   ├── KioskController.cs              ← /api/kiosks
│   └── ReportController.cs             ← /api/reports
│
├── Middleware/
│   ├── GlobalExceptionHandlerMiddleware.cs
│   └── RequestLoggingMiddleware.cs
│
├── appsettings.json
├── appsettings.Development.json        ← DEV config: SePay sandbox, ngrok URL
└── Program.cs                          ← DI, Swagger, CORS, Auth, Serilog
```

---

### 2.5 BookKiosk.Kiosk — WPF Desktop App

```
BookKiosk.Kiosk/
│
├── Pages/                              ← UserControl hoặc Page (XAML)
│   ├── IdlePage.xaml                   ← màn hình chờ, quảng cáo
│   ├── HomePage.xaml                   ← home + gợi ý sách
│   ├── SearchPage.xaml                 ← tìm sách
│   ├── BookDetailPage.xaml             ← chi tiết + vị trí kệ + tồn kho
│   ├── CartPage.xaml                   ← giỏ hàng
│   ├── MemberPage.xaml                 ← nhập SĐT, xem điểm, chọn dùng điểm
│   ├── PaymentPage.xaml                ← QR + đếm ngược
│   ├── ReceiptPage.xaml                ← hóa đơn + nút lưu PDF
│   └── MaintenancePage.xaml            ← màn hình bảo trì khi lỗi
│
├── ViewModels/
│   ├── IdleViewModel.cs
│   ├── HomeViewModel.cs
│   ├── SearchViewModel.cs
│   ├── BookDetailViewModel.cs
│   ├── CartViewModel.cs
│   ├── MemberViewModel.cs
│   ├── PaymentViewModel.cs
│   └── ReceiptViewModel.cs
│
├── Services/
│   ├── ApiClient.cs                    ← HttpClient + Polly (retry, timeout, circuit breaker)
│   ├── CameraService.cs                ← ZXing.Net, quét barcode từ webcam
│   ├── IdleTimerService.cs             ← 60 giây không thao tác → về Idle
│   ├── KioskHealthService.cs           ← heartbeat 30s, báo incident
│   └── PdfViewerService.cs             ← mở PDF sau khi QuestPDF sinh
│
├── Models/                             ← Model nội bộ Kiosk (không dùng DTO API trực tiếp)
│   ├── CartItem.cs
│   └── MemberSession.cs               ← lưu thông tin member trong phiên
│
├── App.xaml
└── App.xaml.cs                         ← DI setup, global exception handler
```

---

### 2.6 BookKiosk.CMS — ASP.NET Core MVC Web Admin

```
BookKiosk.CMS/
│
├── Controllers/
│   ├── DashboardController.cs
│   ├── BookController.cs
│   ├── CategoryController.cs
│   ├── InventoryController.cs
│   ├── PosController.cs                ← bán tại quầy
│   ├── OrderController.cs
│   ├── MemberController.cs             ← danh sách TV, điểm, lịch sử
│   ├── PromotionController.cs          ← tạo/tắt chương trình KM
│   ├── KioskController.cs              ← giám sát Kiosk
│   ├── ReportController.cs
│   └── UserController.cs
│
├── Views/
│   ├── Dashboard/Index.cshtml
│   ├── Book/Index.cshtml, Create.cshtml, Edit.cshtml
│   ├── Inventory/Index.cshtml, Create.cshtml
│   ├── Pos/Index.cshtml
│   ├── Member/Index.cshtml, Detail.cshtml
│   ├── Promotion/Index.cshtml, Create.cshtml
│   ├── Kiosk/Index.cshtml
│   └── Shared/_Layout.cshtml
│
├── ViewModels/
│   ├── BookListViewModel.cs
│   ├── PosViewModel.cs
│   ├── MemberDetailViewModel.cs
│   └── PromotionCreateViewModel.cs
│
└── wwwroot/
    ├── css/site.css
    └── js/site.js
```

---

## 3. Naming Convention đầy đủ

### 3.1 Entity (BookKiosk.Domain/Entities/)

```csharp
// ✅ Đúng
public class OrderDetail          // PascalCase, số ít
{
    public int OrderDetailId { get; set; }   // PK: {ClassName}Id
    public int OrderId { get; set; }         // FK: {ReferencedClass}Id
    public int BookId { get; set; }          // FK
    public decimal UnitPriceAtTime { get; set; }  // PascalCase, mô tả rõ
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }  // audit field
}

// ❌ Sai
public class order_detail { }           // snake_case
public class OrderDetails { }           // số nhiều (Entity dùng số ít)
public int Id { get; set; }             // PK không rõ là của bảng nào
public int order_id { get; set; }       // snake_case
public decimal Price { get; set; }      // không rõ là giá nào, lúc nào
```

### 3.2 DTO

```csharp
// ✅ Đúng — tên = {Resource}{Hướng/Mục đích}Dto
public class BookKioskDto { }          // DTO trả về Kiosk
public class BookAdminDto { }          // DTO trả về Admin
public class CheckoutRequestDto { }    // DTO nhận từ client
public class CheckoutResponseDto { }   // DTO trả về client
public class SePayWebhookDto { }       // DTO từ SePay webhook

// ❌ Sai
public class BookData { }              // không có Dto suffix
public class BookKioskResponse { }     // không nhất quán
public class BookDTO { }               // viết hoa cả DTO (dùng Dto)
```

### 3.3 Service & Interface

```csharp
// ✅ Đúng
public interface ICheckoutService
{
    Task<CheckoutResponseDto> CheckoutAsync(CheckoutRequestDto request);
    Task<bool> CancelOrderAsync(int orderId);
}

public class CheckoutService : ICheckoutService { }

// ❌ Sai
public interface CheckoutService { }   // Interface không có prefix I
public class CheckoutServiceImpl { }   // không dùng Impl suffix
public class CheckoutManager { }       // không nhất quán (dùng Service)
```

### 3.4 Repository

```csharp
// ✅ Đúng
public interface IBookRepository
{
    Task<Book?> GetByIdAsync(int bookId);
    Task<Book?> GetByBarcodeAsync(string barcode);
    Task<IEnumerable<Book>> SearchAsync(string keyword, int? categoryId, int page);
}

public class BookRepository : IBookRepository { }

// ❌ Sai
public class BookDAO { }              // dùng Repository, không dùng DAO
public class BookData { }             // không rõ vai trò
```

### 3.5 Controller (API)

```csharp
// ✅ Đúng
[ApiController]
[Route("api/[controller]")]           // → /api/books
public class BooksController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) { }

    [HttpPost("barcode/{code}")]
    public async Task<IActionResult> GetByBarcode(string code) { }
}

// ❌ Sai
public class BookController { }       // thiếu s — phải là BooksController
[Route("api/Book")]                   // phải lowercase: api/books
public IActionResult getBook() { }    // method phải PascalCase
```

### 3.6 ViewModel (WPF — BookKiosk.Kiosk)

```csharp
// ✅ Đúng
public class CartViewModel : BaseViewModel   // kế thừa BaseViewModel (có INotifyPropertyChanged)
{
    private List<CartItem> _cartItems = new();
    public List<CartItem> CartItems
    {
        get => _cartItems;
        set { _cartItems = value; OnPropertyChanged(); }
    }

    public ICommand AddToCartCommand { get; }     // Command: {Action}Command
    public ICommand RemoveItemCommand { get; }
}

// ❌ Sai
public class CartVM { }               // dùng ViewModel không viết tắt
public List<CartItem> cartItems;      // field public, phải là property
public void AddToCart() { }           // không binding được, phải là ICommand
```

### 3.7 Enum

```csharp
// ✅ Đúng
public enum OrderStatus
{
    Pending,       // PascalCase value
    Paid,
    Cancelled,
    NeedsReview    // ghép từ không cần gạch nối
}

public enum PromotionType
{
    Percent,       // giảm theo %
    FixedAmount    // giảm số tiền cố định
}

// ❌ Sai
public enum ORDER_STATUS { }          // UPPER_CASE không dùng cho enum name
public enum OrderStatus
{
    PENDING,                          // UPPER_CASE không dùng cho enum value
    paid,                             // lowercase
}
```

### 3.8 Endpoint URL

| Quy tắc | Đúng ✅ | Sai ❌ |
|---|---|---|
| Chữ thường | `/api/books` | `/api/Books` |
| Số nhiều | `/api/books` | `/api/book` |
| kebab-case | `/api/stock-receipts` | `/api/stockReceipts` |
| ID trong path | `/api/books/{id}` | `/api/getBook?id=` |
| Action rõ | `/api/orders/{id}/cancel` | `/api/cancelOrder/{id}` |
| Query filter | `/api/books?keyword=&page=1` | `/api/searchBooks/abc` |

### 3.9 Tên bảng & cột (SQL Server / EF)

| Hạng mục | Convention | Ví dụ đúng ✅ | Ví dụ sai ❌ |
|---|---|---|---|
| Tên bảng | `PascalCase`, số nhiều | `Books`, `OrderDetails`, `StockHistories` | `book`, `tbl_order` |
| Tên cột | `PascalCase` | `BookId`, `UnitPriceAtTime` | `book_id`, `unitPrice` |
| Khóa chính | `{Bảng số ít}Id` | `BookId`, `OrderDetailId` | `Id`, `ID` |
| Khóa ngoại | `{Entity}Id` | `CategoryId`, `MemberId` | `category`, `fk_member` |
| Index | `IX_{Bảng}_{Cột}` | `IX_Books_Barcode` | `idx_barcode` |
| Unique constraint | `UQ_{Bảng}_{Cột}` | `UQ_Books_Barcode` | `unique_barcode` |
| Check constraint | `CK_{Bảng}_{Cột}` | `CK_Books_StockQuantity` | `check_stock` |

---

## 4. Quy tắc tạo file mới

### Khi thêm Entity mới (ví dụ: `Promotion`)

```
1. Tạo: BookKiosk.Domain/Entities/Promotion.cs
2. Tạo: BookKiosk.Domain/Enums/PromotionType.cs  (nếu cần enum mới)
3. Tạo: BookKiosk.Infrastructure/Data/Configurations/PromotionConfiguration.cs
4. Thêm DbSet vào: BookKiosk.Infrastructure/Data/BookKioskDbContext.cs
5. Tạo: BookKiosk.Application/Interfaces/Repositories/IPromotionRepository.cs
6. Tạo: BookKiosk.Infrastructure/Repositories/PromotionRepository.cs
7. Chạy: dotnet ef migrations add Add_Promotion_Table --project BookKiosk.Infrastructure
8. Cập nhật: docs/03-database.md
```

### Khi thêm API endpoint mới (ví dụ: GET /api/promotions/active)

```
1. Thêm method vào: BookKiosk.Application/Interfaces/Services/IPromotionService.cs
2. Implement trong: BookKiosk.Application/Services/PromotionService.cs
3. Thêm DTO nếu cần: BookKiosk.Application/DTOs/Promotion/ActivePromotionDto.cs
4. Thêm action vào: BookKiosk.API/Controllers/PromotionsController.cs
5. Cập nhật: docs/04-api-reference.md
```

### Khi thêm màn hình Kiosk mới (ví dụ: `MemberPage`)

```
1. Tạo: BookKiosk.Kiosk/Pages/MemberPage.xaml + MemberPage.xaml.cs
2. Tạo: BookKiosk.Kiosk/ViewModels/MemberViewModel.cs
3. Đăng ký DI trong: App.xaml.cs
4. Thêm vào State Machine điều hướng màn hình
5. Cập nhật: docs/08-kiosk-app.md
```

---

## 5. Những điều KHÔNG làm

```
❌ Viết logic nghiệp vụ (tính tiền, check tồn kho) trong Controller
   → Controller chỉ gọi Service

❌ Gọi DbContext trực tiếp từ Service
   → Service chỉ gọi qua IRepository

❌ Dùng DTO của Kiosk trong CMS hoặc ngược lại
   → Mỗi client có DTO riêng

❌ BookKioskDto trả về giá vốn (CostPrice), ngày nhập, thông tin nhân viên
   → Kiosk DTO chỉ có: tên, ảnh, giá bán, tồn kho, vị trí kệ

❌ Tạo file .md mới để giải thích từng hàm
   → Dùng XML comment trong .cs

❌ Hardcode connection string, API key trong code
   → Luôn từ appsettings.json hoặc biến môi trường

❌ Code Entity hoặc viết HttpClient trước khi 03-database.md và 04-api-reference.md được Leader confirm
   → Thiếu contract → conflict schema, gọi sai API
```

---

## 6. Communication Map — File nào nói chuyện với file nào

> Đọc mục này để biết rõ luồng gọi và tránh gọi nhầm tầng.

### 6.1 Kiosk App gọi API

```
BookKiosk.Kiosk/Services/ApiClient.cs
    │  HttpClient + Polly (retry / timeout / circuit breaker)
    │  Header: Authorization: Bearer <jwt>  hoặc  X-Api-Key: <kiosk-key>
    │
    ├── GET  /api/recommendations          → RecommendationController
    ├── GET  /api/books?keyword=&page=     → BooksController
    ├── GET  /api/books/{id}               → BooksController
    ├── POST /api/orders/checkout          → OrdersController
    ├── GET  /api/orders/{id}/status       → OrdersController  (poll)
    ├── POST /api/members/lookup           → MembersController (tra SĐT)
    ├── GET  /api/promotions/active        → PromotionsController
    └── POST /api/kiosks/heartbeat         → KiosksController
```

### 6.2 CMS Web Admin gọi API

```
BookKiosk.CMS/Controllers/*.cs
    │  HttpClient (gọi API nội bộ) HOẶC inject Service trực tiếp*
    │  Header: Authorization: Bearer <jwt-staff/admin>
    │
    ├── GET/POST/PUT/DELETE /api/books          → BooksController
    ├── GET/POST            /api/inventory      → InventoryController
    ├── POST                /api/orders/counter → OrdersController  (POS quầy)
    ├── GET/POST/PATCH      /api/promotions     → PromotionsController
    ├── GET                 /api/members        → MembersController
    ├── GET                 /api/reports/*      → ReportController
    └── GET                 /api/kiosks         → KiosksController

* Nếu CMS và API deploy cùng 1 process → inject Service trực tiếp, không đi qua HTTP.
  Nếu deploy tách biệt → gọi qua HTTP như Kiosk.
  Leader confirm trước khi code CMS.
```

### 6.3 Luồng xử lý trong Backend (tầng xuống tầng)

```
[Request vào] ──► Controller (validate input, authorize)
                      │
                      ▼
                  Service (business logic: tính tiền, check rule)
                      │
               ┌──────┴──────┐
               ▼             ▼
          Repository    External Service
          (EF Core)     (SePayClient, PdfService)
               │
               ▼
          DbContext ──► SQL Server
```

**Quy tắc bất biến:**
- Controller **không** gọi Repository trực tiếp
- Service **không** gọi DbContext trực tiếp (chỉ qua IRepository)
- Controller **không** chứa `if/else` nghiệp vụ (tính tiền, check tồn kho, v.v.)

### 6.4 Webhook SePay

```
SePay Server ──► POST /api/payments/webhook (public, không cần JWT)
                          │
                    PaymentsController
                    └── xác thực HMAC signature (bắt buộc, reject nếu sai)
                          │
                    PaymentService.HandleWebhookAsync()
                    ├── tìm Order theo ReferenceCode
                    ├── idempotency check (đã xử lý rồi → return 200 luôn)
                    ├── đổi trạng thái → Paid
                    ├── trừ ReservedQuantity khỏi kho (transaction)
                    ├── tích điểm Member (nếu có)
                    └── ghi StockHistory + PointTransaction
```

---

## 7. Chuẩn Phân Trang (Pagination)

### 7.1 Khi nào BẮT BUỘC có phân trang?

| Màn hình / Endpoint | Có phân trang? |
|---|---|
| `GET /api/books` (tìm kiếm) | ✅ Bắt buộc |
| `GET /api/orders` (lịch sử đơn hàng) | ✅ Bắt buộc |
| `GET /api/members` (danh sách TV) | ✅ Bắt buộc |
| `GET /api/stock-receipts` | ✅ Bắt buộc |
| `GET /api/recommendations` (gợi ý) | ❌ Trả về top 10, không phân trang |
| `GET /api/categories` | ❌ Trả về all (< 50 records) |
| `GET /api/areas` | ❌ Trả về all |
| `GET /api/promotions/active` | ❌ Trả về all đang active |

### 7.2 Format request

```
GET /api/books?keyword=harry&categoryId=2&page=1&pageSize=20

Mặc định:  page=1, pageSize=20
Tối đa:    pageSize=100 (reject nếu vượt quá)
```

### 7.3 Format response

```csharp
// Wrapper chuẩn cho mọi response có phân trang
public class PagedResult<T>
{
    public List<T> Data { get; set; }
    public PaginationMeta Pagination { get; set; }
}

public class PaginationMeta
{
    public int Page { get; set; }        // trang hiện tại
    public int PageSize { get; set; }    // số item / trang
    public int Total { get; set; }       // tổng số item
    public int TotalPages { get; set; }  // tổng số trang
}
```

```json
// Ví dụ response thực tế
{
  "success": true,
  "code": "BOOKS_FOUND",
  "message": "Tìm thấy 42 sách",
  "data": {
    "data": [ { "bookId": 1, "title": "Harry Potter" } ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "total": 42,
      "totalPages": 3
    }
  }
}
```

### 7.4 CMS — Partial View phân trang (tái sử dụng)

```csharp
// Dùng ở mọi trang có danh sách trong CMS
@await Html.PartialAsync("_Pagination", new PaginationViewModel
{
    CurrentPage = Model.Pagination.Page,
    TotalPages  = Model.Pagination.TotalPages,
    RouteValues = new { keyword = Model.Keyword }  // giữ filter khi chuyển trang
})
```

### 7.5 Kiosk WPF — Load more (không dùng page number)

```csharp
// SearchPage: không hiện số trang, chỉ có nút "Xem thêm"
// Lý do: màn hình cảm ứng, trang số không thân thiện
// Cơ chế: giữ page index trong SearchViewModel, mỗi lần nhấn → page++, append vào list
private int _currentPage = 1;
public ICommand LoadMoreCommand { get; }  // append, không replace list
```

---

## 8. Chuẩn Response Message (API)

### 8.1 Wrapper bắt buộc cho MỌI response

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Code { get; set; }      // machine-readable, dùng để Kiosk xử lý
    public string Message { get; set; }   // human-readable, có thể hiển thị trực tiếp
    public T? Data { get; set; }
}
```

### 8.2 Bảng Code chuẩn — KHÔNG tự đặt code ngoài bảng này

| HTTP Status | Code | Khi nào dùng |
|---|---|---|
| 200 | `BOOKS_FOUND` | GET list thành công |
| 200 | `BOOK_FOUND` | GET single thành công |
| 201 | `ORDER_CREATED` | Tạo đơn thành công |
| 200 | `PAYMENT_CONFIRMED` | Webhook xác nhận thành công |
| 200 | `MEMBER_FOUND` | Tra SĐT tìm thấy member |
| 200 | `STOCK_UPDATED` | Cập nhật kho thành công |
| 400 | `INVALID_REQUEST` | Request body sai format/thiếu field |
| 400 | `OUT_OF_STOCK` | Sách hết hàng khi checkout |
| 400 | `POINTS_EXCEEDED` | Dùng quá 100 điểm |
| 401 | `UNAUTHORIZED` | Không có token hoặc token hết hạn |
| 403 | `FORBIDDEN` | Có token nhưng không đủ quyền |
| 404 | `BOOK_NOT_FOUND` | Không tìm thấy sách |
| 404 | `ORDER_NOT_FOUND` | Không tìm thấy đơn |
| 404 | `MEMBER_NOT_FOUND` | SĐT chưa đăng ký |
| 409 | `DUPLICATE_WEBHOOK` | Webhook trùng lặp (đã xử lý rồi) |
| 408 | `PAYMENT_EXPIRED` | Đơn hết hạn thanh toán (3 phút) |
| 500 | `INTERNAL_ERROR` | Lỗi không xác định |

### 8.3 Ví dụ thực tế

```json
// ✅ Thành công
{ "success": true,  "code": "ORDER_CREATED",  "message": "Đơn hàng đã được tạo", "data": { "orderId": 42, ... } }

// ✅ Lỗi nghiệp vụ (không phải lỗi server)
{ "success": false, "code": "OUT_OF_STOCK",   "message": "Sách 'Harry Potter' vừa hết hàng", "data": null }

// ✅ Lỗi validate
{ "success": false, "code": "INVALID_REQUEST","message": "Thiếu CartItems", "data": { "errors": ["CartItems is required"] } }
```

### 8.4 Kiosk xử lý response — dùng Code, không dùng Message

```csharp
// ✅ Đúng — check code, không check message text
var result = await _apiClient.CheckoutAsync(request);
switch (result.Code)
{
    case "ORDER_CREATED":   NavigateTo<PaymentPage>(); break;
    case "OUT_OF_STOCK":    ShowOutOfStockDialog(); break;
    case "UNAUTHORIZED":    RefreshTokenAndRetry(); break;
    default:                ShowGenericError(result.Message); break;
}

// ❌ Sai — so sánh message string dễ vỡ khi message thay đổi
if (result.Message == "Sách vừa hết hàng") { ... }
```

---

## 9. Component Tái Sử Dụng

### 9.1 CMS Web Admin — Razor Partial Views

> Đặt tất cả component tái sử dụng trong `Views/Shared/`

```
BookKiosk.CMS/Views/Shared/
├── _Layout.cshtml              ← layout chính: sidebar + header + main
├── _Sidebar.cshtml             ← menu điều hướng (include trong _Layout)
├── _Header.cshtml              ← top bar: tên user, logout, thông báo
├── _Pagination.cshtml          ← phân trang (dùng ở mọi trang danh sách)
├── _Alert.cshtml               ← thông báo success/error/warning
├── _ConfirmModal.cshtml        ← modal xác nhận xóa/vô hiệu
├── _BookCard.cshtml            ← card sách (dùng trong search POS)
└── _LoadingSpinner.cshtml      ← spinner khi đang load
```

**Cách dùng:**
```csharp
// Trong _Layout.cshtml — sidebar và header đã include sẵn, không cần gọi lại
@await Html.PartialAsync("_Sidebar")

// Trong từng View — chỉ cần gọi component cần thiết
@await Html.PartialAsync("_Alert", TempData["AlertMessage"])
@await Html.PartialAsync("_Pagination", Model.Pagination)
@await Html.PartialAsync("_ConfirmModal", new { Id = item.BookId, Name = item.Title })
```

**Quy tắc:**
- ❌ Không copy-paste HTML phân trang, alert, modal giữa các View
- ✅ Luôn dùng Partial View — sửa 1 chỗ là đồng bộ toàn bộ

### 9.2 Kiosk WPF — Shared UserControls & BaseViewModel

```
BookKiosk.Kiosk/
├── Controls/                           ← UserControl tái sử dụng
│   ├── BookCardControl.xaml            ← card sách (dùng ở Home, Search)
│   ├── LoadingOverlayControl.xaml      ← overlay loading (dùng ở mọi page)
│   ├── AlertDialogControl.xaml         ← popup thông báo lỗi/thành công
│   ├── CountdownTimerControl.xaml      ← đếm ngược (dùng ở PaymentPage)
│   └── NumericKeypadControl.xaml       ← bàn phím số (dùng ở MemberPage, DEV input)
│
└── ViewModels/
    └── BaseViewModel.cs                ← INotifyPropertyChanged + helper methods
```

**BaseViewModel — tất cả ViewModel đều kế thừa:**
```csharp
public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // Trạng thái loading — dùng để hiện/ẩn LoadingOverlay
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    // Thông báo lỗi nhanh — bind vào AlertDialog
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }
}
```

**Quy tắc:**
- ❌ Không copy-paste code `INotifyPropertyChanged` vào từng ViewModel
- ❌ Không tạo loading indicator riêng từng page — dùng `LoadingOverlayControl` + bind `IsLoading`
- ✅ Mọi ViewModel kế thừa `BaseViewModel`
- ✅ Mọi UserControl định nghĩa `DependencyProperty` cho data binding
