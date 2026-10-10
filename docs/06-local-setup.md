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
- **.NET 10 SDK:** các project đang target `net10.0`; WPF cần Windows.
- **SQL Server (Developer hoặc Express):** Local database.
- **SSMS (SQL Server Management Studio)** hoặc **Azure Data Studio**: Để quản lý DB.
- **Visual Studio 2022** (Khuyên dùng, cần thiết để code WPF Kiosk) hoặc **VS Code**.
- **Ngrok:** Công cụ tạo đường hầm (tunnel) để máy chủ Local nhận được Webhook thanh toán từ SePay.

---

## 2. Clone Code & Khôi phục (Restore)

Mở Terminal / Git Bash và chạy:

```bash
git clone https://github.com/Unknown4505/Library-System.git
cd Library-System
dotnet restore BookKiosk.slnx
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

## 4. Cấu hình Secret và CORS

API dùng các key canonical `ApiSettings:ApiKey`, `Jwt:Key` và `Cors:AllowedOrigins`. Cấu hình Development có API Key/JWT demo để chạy local; có thể ghi đè bằng user-secrets:

```bash
dotnet user-secrets init --project BookKiosk.API
dotnet user-secrets set "ApiSettings:ApiKey" "<development-kiosk-key>" --project BookKiosk.API
dotnet user-secrets set "Jwt:Key" "<development-jwt-signing-key-at-least-32-bytes>" --project BookKiosk.API
```

Hoặc dùng environment variables `ApiSettings__ApiKey` và `Jwt__Key`. Production bắt buộc phải cung cấp secret thật; fallback JWT chỉ hoạt động ở Development. Origin local của CMS là `https://localhost:7125`/`http://localhost:5131` và được khai báo trong `Cors:AllowedOrigins`. Kiosk WPF không chịu CORS.

Kiosk dùng `ApiSettings:BaseUrl` và `ApiSettings:ApiKey` trong config riêng. Giá trị API Key phải trùng với API. Chuỗi `kiosk-secret-key` trong file Development chỉ là dữ liệu local mẫu, không dùng khi deploy.

---

## 5. Khởi chạy Dự án (Run Project)

Hệ thống có 3 project chính. **BẮT BUỘC phải chạy Backend API lên trước.**

### Chạy Backend API
```bash
cd BookKiosk.API
dotnet run
```
Truy cập: `https://localhost:7111/swagger` để xem tài liệu API (Swagger UI) và test thử.

CMS là process riêng: chạy `dotnet run --project BookKiosk.CMS`, mặc định tại `https://localhost:7125` hoặc `http://localhost:5131`.

### Chạy Kiosk App (WPF)
- Mở Visual Studio 2022.
- Chọn project `BookKiosk.Kiosk` làm **Startup Project**.
- Bấm **F5** để chạy Kiosk App.
- Đảm bảo `ApiSettings:BaseUrl` trong `BookKiosk.Kiosk/appsettings.json` trỏ tới `https://localhost:7111/`.

---

## 6. Setup Ngrok để Test Thanh toán thực tế (Webhook)

Khi test thanh toán, SePay cần gọi webhook về API local tại cổng `7111`, nên cần tunnel công khai.

### Bước 1: Chạy Ngrok
Mở Terminal mới và gõ:
```bash
ngrok http https://localhost:7111
```
Ngrok sẽ sinh ra một đường link internet (Ví dụ: `https://abcd-123.ap.ngrok.io`). Link này trỏ thẳng vào localhost của bạn.

### Bước 2: Cập nhật Webhook URL
Vào màn hình quản trị SePay, paste đường link Ngrok vừa lấy được vào cấu hình Webhook URL.
Thêm route API xử lý webhook của bạn vào đuôi:
👉 `https://abcd-123.ap.ngrok.io/api/payments/sepay-webhook`

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
- **Cách fix:** Kiểm tra `BookKioskApiClient`/HttpClient gửi Header `X-API-KEY`, và `ApiSettings:ApiKey` của Kiosk trùng với `ApiSettings:ApiKey` phía API.
