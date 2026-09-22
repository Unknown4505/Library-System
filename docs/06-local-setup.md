# Hướng Dẫn Cài Đặt Local (Local Setup) — BookKiosk

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

> **Mục tiêu:** Hướng dẫn từng bước (Step-by-step) để bất kỳ thành viên nào mới vào dự án cũng có thể tự setup môi trường, clone code về và chạy thử dự án thành công trên máy cá nhân mà không cần hỏi Leader.

---

## 1. Yêu cầu hệ thống (Prerequisites)

Trước khi bắt đầu, đảm bảo máy bạn đã cài đặt đủ các phần mềm sau:
- **.NET 8 SDK:** Tải từ trang chủ Microsoft.
- **SQL Server (Developer hoặc Express):** Local database.
- **SSMS (SQL Server Management Studio)** hoặc **Azure Data Studio**: Để quản lý DB.
- **Visual Studio 2022** (Khuyên dùng, cần thiết để code WPF Kiosk) hoặc **VS Code**.
- **Ngrok:** Công cụ tạo đường hầm (tunnel) để máy chủ Local nhận được Webhook thanh toán từ PayOS/SePay.

---

## 2. Clone Code & Khôi phục (Restore)

Mở Terminal / Git Bash và chạy:

```bash
git clone https://github.com/<your-org>/BookKiosk.git
cd BookKiosk
dotnet restore
```

---

## 3. Cấu hình Cơ sở dữ liệu (Database Setup)

Toàn bộ Backend sử dụng **Entity Framework Core (Code First)**.

### Bước 3.1: Đổi chuỗi kết nối (Connection String)
Mở file `BookKiosk.API/appsettings.Development.json` (nếu chưa có thì copy từ `appsettings.json`):
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=BookKiosk_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```
*(Đổi `Server=localhost` thành tên instance SQL Server của máy bạn nếu cần, ví dụ `Server=.\\SQLEXPRESS`).*

### Bước 3.2: Chạy Migration để tạo Database
Mở Terminal ở thư mục gốc của project (hoặc mở Package Manager Console trong Visual Studio, chọn Default project là `BookKiosk.Infrastructure`):

```bash
cd BookKiosk.API
dotnet ef database update --project ../BookKiosk.Infrastructure
```
Lệnh này sẽ tự động tạo Database tên là `BookKiosk_Dev` và đẩy toàn bộ các bảng vào SQL Server của bạn.

---

## 4. Cấu hình Cổng Thanh Toán (PayOS / SePay)

Để Kiosk sinh được mã QR thanh toán đúng chuẩn đồ án, bạn cần khai báo API Key.
Tiếp tục sửa file `BookKiosk.API/appsettings.Development.json`:

```json
"PaymentGateway": {
  "ClientId": "YOUR_PAYOS_CLIENT_ID",
  "ApiKey": "YOUR_PAYOS_API_KEY",
  "ChecksumKey": "YOUR_PAYOS_CHECKSUM_KEY"
}
```
*(Nếu chưa có, hãy xin Leader hoặc tự đăng ký 1 tài khoản dev miễn phí trên PayOS để test).*

---

## 5. Khởi chạy Dự án (Run Project)

Hệ thống có 3 project chính. **BẮT BUỘC phải chạy Backend API lên trước.**

### Chạy Backend API
```bash
cd BookKiosk.API
dotnet run
```
Truy cập: `https://localhost:5001/swagger` để xem tài liệu API (Swagger UI) và test thử.

### Chạy Kiosk App (WPF)
- Mở Visual Studio 2022.
- Chọn project `BookKiosk.Kiosk` làm **Startup Project**.
- Bấm **F5** để chạy Kiosk App.
- Đảm bảo trong `appsettings.json` của Kiosk, `ApiBaseUrl` đang trỏ đúng về `https://localhost:5001`.

---

## 6. Setup Ngrok để Test Thanh toán thực tế (Webhook)

Khi bạn test dùng điện thoại quét mã QR Kiosk để chuyển tiền thật, ngân hàng sẽ trả kết quả về cho PayOS. PayOS cần báo lại cho Backend API của bạn (Webhook). Nhưng vì API của bạn đang chạy ở `localhost:5001`, PayOS trên internet không thể gọi vào được. -> **Cần dùng Ngrok.**

### Bước 1: Chạy Ngrok
Mở Terminal mới và gõ:
```bash
ngrok http https://localhost:5001
```
Ngrok sẽ sinh ra một đường link internet (Ví dụ: `https://abcd-123.ap.ngrok.io`). Link này trỏ thẳng vào localhost của bạn.

### Bước 2: Cập nhật Webhook URL
Vào màn hình quản trị của PayOS/SePay, paste đường link Ngrok vừa lấy được vào cấu hình Webhook URL.
Thêm route API xử lý webhook của bạn vào đuôi:
👉 `https://abcd-123.ap.ngrok.io/api/payments/webhook`

**Lưu ý:** Vì dùng bản ngrok miễn phí, mỗi lần bạn tắt máy bật lại, ngrok sẽ đổi link mới. Bạn phải vào trang quản trị cổng thanh toán cập nhật lại URL.

---

## 7. Các lỗi thường gặp (Troubleshooting)

### Lỗi 1: `A network-related or instance-specific error occurred while establishing a connection to SQL Server.`
- **Nguyên nhân:** Chuỗi kết nối sai, hoặc Dịch vụ SQL Server chưa bật.
- **Cách fix:** Bấm `Win + R`, gõ `services.msc`, tìm `SQL Server (MSSQLSERVER)` hoặc `(SQLEXPRESS)` và ấn Start. Kiểm tra lại chuỗi `Server=` trong `appsettings.json`.

### Lỗi 2: Lỗi CORS khi gọi API từ trình duyệt (CMS Web Admin)
- **Nguyên nhân:** Trình duyệt chặn Kiosk Web gọi API khác port do vi phạm chính sách bảo mật CORS.
- **Cách fix:** Mở `Program.cs` ở thư mục `BookKiosk.API`, đảm bảo đã khai báo `app.UseCors()` trước `app.UseAuthorization()`.

### Lỗi 3: Kiosk gọi API toàn báo `401 Unauthorized`
- **Nguyên nhân:** Kiosk chưa truyền `X-Api-Key` trong Header.
- **Cách fix:** Kiểm tra lại class `HttpClient` trong project Kiosk, đảm bảo đã add Header `X-Api-Key` trùng khớp với khóa `KioskSecretKey` khai báo trong API.
