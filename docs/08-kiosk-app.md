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
- **Giao diện:** Chạy video quảng cáo sách hoặc hiển thị ảnh "Chạm vào màn hình để bắt đầu".
- **Logic:** Nếu khách chạm vào, chuyển sang Màn hình Tìm Sách.

### 2. Màn hình Tìm sách / Quét mã vạch (Home Screen)
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
  - Gọi API `POST /api/orders/checkout` để lấy mã QR.
  - Bật tính năng **SignalR Client** (hoặc Polling mỗi 2 giây gọi API kiểm tra trạng thái đơn hàng).
  - Khóa màn hình (chặn nút Back) để tránh tình trạng khách vừa chuyển tiền xong lại bấm Back gây lỗi giỏ hàng.
  - Nếu quá 3 phút không thanh toán -> Báo "Hết hạn" -> Trở về Màn hình Chờ.

### 5. Màn hình Hoàn tất & In Hóa Đơn (Success Screen)
- **Giao diện:** Báo "Thanh toán thành công. Đang in hóa đơn...".
- **Logic:**
  - Gửi lệnh in (ESC/POS) xuống máy in nhiệt.
  - Sau 5 giây tự động xóa sạch Giỏ hàng và chuyển về Màn hình Chờ đón khách tiếp theo.

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
- **Nút thoát ẩn [DEV-ONLY]:** Nên tạo một góc nhỏ trên màn hình (vô hình), bấm liên tục 5 lần sẽ hiện ra ô nhập Password để tắt Kiosk, phục vụ lúc nhân viên bảo trì cần thoát ra Windows.
