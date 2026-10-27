# Kiến Trúc & Giao Diện Kiosk App (WPF) — BookKiosk

<!-- AI_REVIEW_INSTRUCTION
Nếu bạn là AI agent đọc file này:
- Phân tích toàn bộ nội dung docs trong folder này
- Nếu phát hiện bất kỳ vấn đề nào sau đây, HÃY BÁO LẠI NGAY cho user:
  • Thiếu thông tin cần thiết để code (endpoint, schema, field type)
  • Mâu thuẫn giữa các file (tên khác nhau, kiến trúc khác nhau)
  • Nguy cơ deadlock (2 member đợi nhau do thiếu contract)
  • Nguy cơ conflict (2 member cùng sửa file dùng chung mà không biết)
  • Business rule chưa chốt rõ (có thể hiểu nhiều cách)
-->

> **Mục tiêu:** Tài liệu này dành riêng cho team phụ trách Frontend Kiosk (sử dụng công nghệ C# WPF). Định nghĩa rõ mô hình MVVM, luồng chuyển màn hình (Navigation) và cách giao tiếp với các thiết bị phần cứng (Máy quét mã vạch, Máy in hóa đơn nhiệt).

---

## 1. Kiến trúc tổng quan (WPF MVVM)

Dự án Kiosk bắt buộc tuân theo mô hình **MVVM (Model - View - ViewModel)** để dễ bảo trì và test. Không viết code logic trực tiếp trong `MainWindow.xaml.cs` (Code-behind).

- **Views:** Các file `.xaml` chứa giao diện (nút bấm, danh sách, text).
- **ViewModels:** Chứa logic xử lý, gọi API, và các thuộc tính dữ liệu (`INotifyPropertyChanged`) để Binding lên View.
- **Services:** Chứa logic kết nối phần cứng (Máy in, Máy quét) hoặc gọi HTTP Client tới Backend API.
- **DI (Dependency Injection):** Sử dụng `Microsoft.Extensions.DependencyInjection` để tiêm các ViewModels và Services vào ứng dụng khi khởi động.

---

## 2. Luồng màn hình (Screen Flow)

Ứng dụng Kiosk chạy theo cơ chế "Navigation" (chuyển trang) trong một `Frame` chính duy nhất của `MainWindow`.

### 1. Màn hình Chờ (Idle / Splash Screen)
- **Giao diện `[IMPLEMENTED — PR #54 HEAD 9431b33 / TARGETED QA PASS]`:** `IdlePage` tự động trình chiếu khuyến mãi đang hiệu lực, sách mới về và sách bán chạy; nếu không có dữ liệu hợp lệ thì hiển thị “Chạm để bắt đầu”. Final WPF visual/real-API E2E vẫn **BLOCKED — INTEGRATION EVIDENCE INCOMPLETE**.
- **Logic:** App khởi động tại `IdlePage`; nếu khách chạm vào thì dừng slideshow và chuyển sang `SearchPage`.

### 2. Màn hình Tìm sách / Quét mã vạch (`SearchPage`)
- **Giao diện:** 
  - Bên trái: Danh sách các sách đang bán (có phân trang/scroll), nút lọc theo Category.
  - Bên phải: Cột hiển thị Giỏ hàng mini.
- **Logic:**
  - Lắng nghe máy quét mã vạch (Barcode Scanner). Nếu quét trúng mã sách -> Tự động thêm 1 cuốn vào giỏ hàng.
  - Có thể bấm trực tiếp vào ảnh sách trên màn hình cảm ứng để thêm vào giỏ.
  - Nút "Thanh toán" (Chỉ sáng lên khi giỏ hàng > 0).

### 3. Màn hình Thành viên & Tích điểm (Member Screen)
- **Giao diện:** Yêu cầu khách quét mã vạch Thẻ Thành Viên, hoặc nhập số điện thoại qua bàn phím ảo (On-screen keyboard).
- **Logic:**
  - Gọi API kiểm tra thành viên.
  - Hiển thị số điểm hiện có và hỏi khách muốn dùng bao nhiêu điểm để giảm giá.
  - Có nút "Bỏ qua" nếu khách không phải thành viên.

### 4. Màn hình Thanh toán (Checkout / QR Screen)
- **Giao diện:** Hiển thị tổng tiền và 1 mã QR code lớn ở giữa màn hình.
- **Logic:**
  - Gọi API `POST /api/orders/kiosk/checkout` để lấy mã QR.
  - Giá catalog/cart trước checkout là giá niêm yết. Promotion, điểm và tổng cuối cùng phải lấy từ response Backend; FE không tự chọn Promotion hoặc tự tính final price.
  - Hiển thị `promotionName`, `discountAmount`, `pointsUsedAmount` và item breakdown `discountPerUnit`/`netLineTotal` do Backend trả về. Empty/null promotion phải có trạng thái không giảm giá rõ ràng.
  - HTTP 400 `BAD_REQUEST` khi tổng sau giảm `<= 0` hoặc payload không hợp lệ phải hiển thị message tiếng Việt, giữ người dùng ở bước có thể chỉnh điểm/giỏ và không dựng QR cục bộ.
  - Polling mỗi **3 giây** qua `GET /api/orders/kiosk/{orderId}/payment-status` (contract hiện tại; SignalR chưa triển khai).
  - Khóa màn hình (chặn nút Back) để tránh tình trạng khách vừa chuyển tiền xong lại bấm Back gây lỗi giỏ hàng.
  - UI hiện đếm ngược 3 phút; Backend dọn đơn `Pending` sau 4 phút. Khoảng đệm cho phép request hủy/poll cuối hoàn tất. Hai timeout có chủ đích khác nhau.

### Nhịp gọi và thời gian chuẩn

- `HttpClient` của Kiosk timeout sau 30 giây cho một request.
- Backend heartbeat đã có trên `main` qua PR #17. `HeartbeatWorker` trong PR #11 gửi mỗi 60 giây nhưng chưa được runtime-test cùng Backend.
- `KioskSettings:KioskId` là bắt buộc, phải là số nguyên dương và phải tồn tại trong bảng `Kiosks`. Thiếu/sai config phải dừng worker với lỗi cấu hình rõ ràng; không fallback về `1`, không gửi chuỗi hiển thị như `KIOSK-01`.
- Timestamp Backend/database phải dùng UTC (`DateTime.UtcNow`); chỉ chuyển sang giờ địa phương khi hiển thị. Các chỗ còn dùng `DateTime.Now` là drift cần owner Backend xử lý, không phải contract mới.

### 5. Màn hình Hoàn tất & In Hóa Đơn (Success Screen)
- **Giao diện:** Báo "Thanh toán thành công. Đang in hóa đơn...".
- **Logic:**
  - Chỉ chuyển sang Success khi polling Backend trả `Paid`; lỗi mạng không được diễn giải thành `Cancelled` hay `Paid`.
  - Gọi `GET /api/orders/kiosk/{orderId}/receipt` và dựng bản in từ response Backend. Không dùng cart local làm nguồn hóa đơn.
  - Receipt/PDF dùng promotion metadata và line discount từ snapshot persisted. Với đơn legacy, chỉ dùng fallback đã được API chuẩn hóa; FE không tự suy đoán ProductDiscount.
  - Gửi lệnh in (ESC/POS) xuống máy in nhiệt.
  - Mốc hiển thị Receipt hiện hành là **5 giây**. Sau khi dữ liệu Receipt/PDF đã được giữ an toàn và thao tác hiển thị/in hoàn tất, xóa giỏ cùng trạng thái giao dịch cũ rồi chuyển về `SearchPage`.
  - Không tự hủy đơn đã `Paid`, không bỏ qua Receipt và không quay thẳng về `IdlePage`. Receipt/PDF an toàn đã merge qua PR #40 và targeted QA PASS; điều hướng Receipt → `SearchPage` thuộc PR #54 đã **IMPLEMENTED / TARGETED QA PASS**. Real Paid/Receipt/PDF E2E vẫn **BLOCKED** do chưa có payment sandbox evidence.

### Kiosk Idle Advertising Mode & Navigation Lifecycle — IMPLEMENTED / TARGETED QA VERIFIED; FINAL UI/E2E BLOCKED

Luồng chính thức:

```text
App Startup → Idle Advertising → Touch → SearchPage
→ Member/Checkout → Payment → Paid → Receipt → SearchPage
→ 60s Inactivity → Idle Advertising
```

`HomePage` đã được gộp vào `SearchPage`; không tạo `HomePage` mới. Các nhánh ngoại lệ gồm `API advertising unavailable → Idle fallback`, không có promotion active thì bỏ slide promotion, `Payment Pending → tiếp tục polling`, payment timeout/cancel theo safe cancellation flow hiện hữu, và lỗi Receipt API phải retry/báo lỗi thay vì dựng hóa đơn chính thức giả.

#### Nội dung và nguồn dữ liệu

| Nhóm | Quy tắc hiển thị | Tình trạng contract hiện tại |
|---|---|---|
| Khuyến mãi | Chỉ `IsActive=true` và thời điểm UTC hiện tại thuộc `StartDate..EndDate`; chỉ quảng cáo loại có căn cứ Checkout hỗ trợ theo Contract V1 | **BACKEND IMPLEMENTED / TARGETED QA PASS** — PR #52 |
| Sách mới về | Ảnh bìa, tên, giá; lần nhập kho gần nhất trong 30 ngày UTC; distinct book; active/còn hàng; sort lần nhập giảm dần rồi `BookId ASC`; tối đa 10 | **BACKEND IMPLEMENTED / TARGETED QA PASS** |
| Sách bán chạy | Chỉ order `Paid` trong 30 ngày UTC; sum quantity; active/còn hàng; sort tổng bán giảm dần rồi `BookId ASC`; tối đa 10; không dùng cart local/Pending/Cancelled | **BACKEND IMPLEMENTED / TARGETED QA PASS** |
| Ảnh bìa | Ghép URL tương đối với API base URL; ảnh lỗi dùng placeholder UI đã duyệt | **AVAILABLE** — `BookDto.ImageUrl`, top-books `imageUrl` và static files hiện hữu |

Contract V1, DTO/nullability, enum `promotionType=1/2`, JSON mẫu và error semantics nằm tại [`docs/04-api-reference.md` mục 3.9](04-api-reference.md). Backend #37 đã **IMPLEMENTED / MERGED / TARGETED QA VERIFIED**; FE #38 ở PR #54 HEAD `9431b33` đã **IMPLEMENTED / TARGETED QA VERIFIED** và không được hardcode dữ liệu giả thành quảng cáo thật. Final WPF visual, Search inactivity 60 giây và Paid E2E chưa đủ evidence để gọi là E2E PASS.

#### Timer, tương tác và cleanup

- **Slideshow timer:** mặc định đề xuất **8 giây/slide**, phải cấu hình được; chỉ một timer sống trên `IdlePage`, dừng/dispose khi rời trang và không reload API liên tục ở mỗi tick.
- **Inactivity timer:** **60 giây** không tương tác chỉ tại `SearchPage` thì dọn trạng thái UI an toàn và về `IdlePage`. `BookDetailPage` không phải trang eligible cho timeout này; khi quay lại `SearchPage`, timer áp dụng theo phiên Search. Mọi thao tác hợp lệ trên Search phải reset timer.
- **Receipt timer:** giữ **5 giây** hiện hành trước khi về `SearchPage`, nhưng không được đóng/cắt ngang thao tác hiển thị, tải PDF hoặc in chưa hoàn tất.
- Ba timer độc lập về trách nhiệm, không dùng chung và không được kích hoạt chồng chéo.
- Không kích hoạt Idle Advertising khi đang nhập thành viên/chọn điểm, tạo hoặc xác nhận checkout, chờ QR `Pending`, đã `Paid` nhưng đang tải Receipt, hoặc đang xem/in Receipt. Các màn hình giao dịch dùng timeout/cancel hiện hữu.
- Sau Receipt, giữ dữ liệu cần cho UI/PDF/in trước; sau đó clear cart, OrderId và state giao dịch cũ rồi điều hướng `SearchPage`. Không gọi cancel cho order `Paid`.

#### Tải dữ liệu, cache và response message

- Gọi `GET /api/kiosk/advertising` với `X-Api-Key` khi vào `IdlePage` và cache không hợp lệ; không dùng JWT CMS và không gọi lại theo mỗi slide.
- Cache tối đa 5 phút, nhưng phải hết hạn sớm tại `endDate` promotion gần nhất. Khi API lỗi, dùng fallback và tối đa một background retry trong mỗi lượt vào Idle sau ít nhất 30 giây; retry tiếp tục lỗi thì giữ fallback/cooldown, không request storm.
- FE phải kiểm tra cả HTTP status và `success`, `code`, `message`, `data`; không coi HTTP 200 là thành công nếu wrapper báo lỗi.
- `SUCCESS` với mảng rỗng là trạng thái không có nội dung; `PARTIAL_SUCCESS` giữ các nhóm tải được và dùng allow-list `unavailableSections`; HTTP 503 `SERVICE_UNAVAILABLE` khi cả ba nhóm không khả dụng.
- `imageUrl=null` hoặc tải ảnh lỗi phải dùng placeholder. Tiền là JSON number và được FE format VNĐ; timestamp là UTC ISO 8601.
- Lỗi tải nền dùng message/fallback thân thiện và log an toàn. Không bật toast/popup mỗi lần slide đổi, inactivity timer chạy, cache refresh hoặc retry nền.
- FE không hiển thị `ProductDiscount` như ưu đãi tự động áp dụng tại Kiosk cho đến khi Checkout thật hỗ trợ và có contract/evidence riêng; không tự tính giá giảm từ payload quảng cáo.

#### Offline/fallback

- API lỗi, timeout hoặc không có nhóm dữ liệu hợp lệ không được làm crash; bỏ nhóm rỗng và dùng màn hình chờ mặc định “Chạm để bắt đầu”.
- Có thể dùng asset tĩnh đã được duyệt trong UI, nhưng không hiển thị promotion hết hạn hoặc dữ liệu giả như dữ liệu thực.
- Touch-to-start vẫn phải hoạt động khi quảng cáo lỗi, miễn các API mua hàng cần thiết khả dụng.

---

## 3. Giao tiếp Phần cứng (Hardware Integration)

### A. Máy quét mã vạch (Barcode Scanner)
Hầu hết máy quét mã vạch trên thị trường hoạt động theo cơ chế **Keyboard Wedge** (Giả lập bàn phím). 
- Nghĩa là khi quét mã `8935244878342`, máy quét sẽ giả lập hành động gõ phím số đó, theo sau là phím `Enter`.
- **Cách xử lý trên WPF:** Không cần thư viện. Bắt sự kiện `Window.PreviewKeyDown`, lưu chuỗi ký tự gõ vào cho đến khi gặp phím `Enter` thì coi đó là mã vạch và gọi API tìm sách.
- **[📌 LƯU Ý DEMO ĐỒ ÁN]:** Lúc báo cáo trên lớp thường không có súng bắn mã vạch thật. Hãy thiết kế thêm một nút ẩn (hoặc phím tắt F2) mở ra 1 ô TextBox để gõ tay mã sách nhằm giả lập hành động quét.

### B. Máy in hóa đơn nhiệt (Thermal Receipt Printer)
Kiosk không in bằng máy in giấy A4, mà dùng cuộn giấy in nhiệt (khổ K80).
- **[📌 LƯU Ý DEMO ĐỒ ÁN]:** Vì lúc bảo vệ đồ án không có máy in thật, Dev bắt buộc phải code thêm tính năng **Giả lập in (Mock Printer)**. Thay vì đẩy lệnh ra thiết bị ngoại vi, hãy làm một `Window / Dialog` bật lên giữa màn hình Laptop, hiển thị nguyên tờ hóa đơn (như một file PDF thu nhỏ). Sau 5 giây popup đó tự tắt đi. Điều này giúp thầy cô dễ hình dung kết quả in.
- **Nếu dùng máy thật (Cách 1):** Tạo `PrintDocument` (`System.Drawing.Printing`), vẽ chuỗi text lên hình ảnh bitmap rồi đẩy xuống máy in mặc định.
- **Nếu dùng máy thật (Cách 2 - Chuyên nghiệp):** Cắm máy in qua cổng USB, dùng thư viện `ESC-POS-USB-NET` bắn mã Hex trực tiếp xuống máy in (tốc độ nhanh, tự động cắt giấy).

---

## 4. Chế độ Kiosk "Bất tử" (Kiosk Mode)

Máy Kiosk đặt nơi công cộng cần tránh việc khách hàng thoát ra ngoài màn hình Windows (Desktop) để nghịch ngợm hoặc cài virus.

**Cấu hình trong `MainWindow.xaml`:**
```xml
<Window ...
        WindowStyle="None" 
        WindowState="Maximized" 
        Topmost="True" 
        ResizeMode="NoResize">
```
- `WindowStyle="None" & WindowState="Maximized"`: Che toàn bộ thanh Taskbar của Windows.
- **Bắt phím tắt:** Cần override hàm xử lý phím (Hook) để chặn các tổ hợp phím như `Alt + F4`, `Ctrl + Alt + Del`, `Windows Key`.
- **Nút thoát ẩn [DEV-ONLY]:** Có thể dùng thao tác 5 lần chạm để mở entry point trong Development, nhưng không được xem đây là xác thực.

### Chính sách bảo trì theo môi trường

- Release/Production không dùng password hardcode hoặc default credential. Phải xác thực nhân viên qua Backend/JWT với quyền phù hợp; nếu dịch vụ này chưa sẵn sàng thì fail closed và chỉ bảo trì bằng quy trình vận hành ngoài ứng dụng.
- Development có thể bật dialog thử nghiệm qua config/secret cục bộ không commit. Build Release phải loại bỏ hoặc vô hiệu hóa đường tắt Development.
- PR #11 hiện còn password hardcode trong `PasswordDialog`; đây là blocker Production giao Khang.

### F3 thanh toán mô phỏng

- F3 chỉ hiển thị trong Development và gọi `POST /api/payments/dev-simulate-success/{orderCode}` không body; `ApiClient` tiếp tục gửi `X-Api-Key`.
- Không gọi `sepay-webhook` bằng payload giả. Khi API trả lỗi/404, UI báo Development simulation không khả dụng và giữ nguyên order; không tự chuyển Paid.
- Release không đăng ký phím F3 và Backend Production không map endpoint.

## 5. Receipt #29 — IMPLEMENTED / MERGED / TARGETED QA VERIFIED

- Sau khi polling trả `Paid`, Kiosk dùng `OrderId` thật gọi `GET /api/orders/kiosk/{orderId}/receipt` với `X-Api-Key`.
- `ReceiptPage`, `ReceiptViewModel`, Mock Printer và QuestPDF dùng cùng `ReceiptDto` từ Backend; không dựng tổng tiền từ cart local.
- `404`, `409`, timeout và network error phải hiển thị trạng thái có thể retry; không tự coi order Paid/Cancelled và không in payload chưa xác minh.
- Pending/Cancelled không có hóa đơn Paid. Chỉ xóa cart sau khi luồng thành công đã giữ đủ dữ liệu hiển thị/in.

Owner: `@lehuukhang`, Issue #29. Backend #28 đã merge qua PR #35; Kiosk Receipt đã merge qua PR #40 (`9a55b09`) và targeted tests PASS. WPF visual/UI E2E vẫn **NOT RUN**, không được suy ra từ targeted ViewModel/PDF tests.
