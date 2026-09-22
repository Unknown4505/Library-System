# HỆ THỐNG QUẢN LÝ & BÁN SÁCH TỰ PHỤC VỤ (KIOSK) — MÔ TẢ CHI TIẾT

> Phiên bản mở rộng từ ý tưởng ban đầu: kiến trúc **"Backend 1 cho 2"** (ASP.NET Core Web API + SQL Server, phục vụ đồng thời Web Admin và Kiosk Desktop C#).

---

## 1. TỔNG QUAN

### 1.1 Mục tiêu
- Giúp khách hàng **tự tra cứu, tự chọn, tự thanh toán** sách tại Kiosk mà không cần nhân viên.
- Giúp thủ thư/quản trị viên **quản lý danh mục, kho, doanh thu, bán tại quầy** trên Web.
- Đảm bảo **dữ liệu nhất quán** (đặc biệt là tồn kho) khi nhiều điểm bán hoạt động đồng thời.

### 1.2 Tác nhân (Actors)
| Tác nhân | Mô tả | Giao diện |
|---|---|---|
| Khách hàng | Người mua sách tại Kiosk, không cần đăng nhập | Kiosk App |
| Thủ thư / Nhân viên quầy | Bán tại quầy, tra cứu, xử lý sự cố Kiosk | Web Admin |
| Quản trị viên (Admin) | Quản lý sách, kho, nhân viên, xem báo cáo | Web Admin |
| Cổng thanh toán | Gửi webhook xác nhận tiền đã về | Backend |
| Hệ thống Kiosk (thiết bị) | Gửi heartbeat, báo lỗi phần cứng | Backend |

### 1.3 Phạm vi
- **Trong phạm vi (MVP):** tra cứu, bán qua Kiosk, bán tại quầy, nhập kho, thanh toán QR + tiền mặt, thống kê, giám sát Kiosk.
- **Mở rộng (giai đoạn sau):** mượn/trả sách (dashboard gốc có nhắc "bán/mượn"), thẻ thành viên, mã giảm giá nâng cao, gợi ý sách, in hóa đơn nhiệt.

---

## 2. KIẾN TRÚC TỔNG THỂ

```
        ┌──────────────────┐         ┌──────────────────────┐
        │   Web Admin      │         │   Kiosk App (WPF)    │
        │ (Thủ thư/Admin)  │         │   (Khách hàng)       │
        └────────┬─────────┘         └──────────┬───────────┘
                 │  HTTPS / REST / JSON          │
                 └───────────────┬───────────────┘
                                 ▼
                 ┌──────────────────────────────┐
                 │  ASP.NET Core Web API        │
                 │  Controllers → Services →    │      ┌───────────────┐
                 │  Repositories (EF Core)      │◄────►│ Cổng thanh toán│
                 └───────────────┬──────────────┘ webhook│ (VietQR/MoMo) │
                                 ▼                      └───────────────┘
                 ┌──────────────────────────────┐
                 │        SQL Server            │
                 └──────────────────────────────┘
```

### 2.1 Công nghệ đề xuất
| Thành phần | Công nghệ | Ghi chú |
|---|---|---|
| Backend | ASP.NET Core Web API (.NET 8), EF Core | Swagger/OpenAPI để test |
| CSDL | SQL Server | Index, transaction, RowVersion |
| Xác thực | JWT (access + refresh token) | Role: Admin, ThuThu |
| Web Admin | React/Vue **hoặc** Blazor / ASP.NET MVC | Chọn cái nhóm quen nhất |
| Kiosk | **WPF** (khuyên dùng hơn WinForms) | Giao diện cảm ứng, MVVM |
| Quét mã | ZXing.Net + AForge/OpenCvSharp/MediaCapture | Camera USB |
| Log | Serilog (file + bảng NhatKy) | Truy vết sự cố |
| Cache | IMemoryCache (danh mục, bản đồ kệ) | Giảm tải Kiosk |

> **Vì sao WPF thay vì WinForms?** WPF hỗ trợ tốt cảm ứng, animation, scale theo DPI, template nút lớn, MVVM. WinForms vẫn làm được nhưng khó làm giao diện "chạm" đẹp và co giãn.

### 2.2 Nguyên tắc thiết kế
1. **Một nguồn sự thật:** mọi logic tiền, tồn kho, quyền hạn nằm ở Backend. Frontend chỉ hiển thị.
2. **Kiosk là client "mỏng":** không giữ khóa DB, không tự tính tiền (chỉ hiển thị số Backend trả về).
3. **Idempotent:** các API thanh toán/webhook có thể gọi lặp mà không gây trừ kho hay tạo hóa đơn 2 lần.
4. **Chịu lỗi:** mất mạng, mất camera, webhook trễ đều có kịch bản xử lý.

---

## 3. KHỐI BACKEND (WEB API)

### 3.1 Cấu trúc solution đề xuất
```
BookKiosk.sln
├── BookKiosk.Api            # Controllers, Middleware, Program.cs, Auth
├── BookKiosk.Application    # Services, DTOs, Interfaces, Validators
├── BookKiosk.Domain         # Entities, Enums
└── BookKiosk.Infrastructure # DbContext, Repositories, Payment client, Logging
```

### 3.2 Các lớp (N-Tier)
| Lớp | Vai trò | Ví dụ |
|---|---|---|
| **Controllers** | Nhận request, kiểm tra quyền, validate đầu vào, trả JSON/HTTP status. Không chứa logic. | `BookController`, `OrderController`, `PaymentController`, `KioskController` |
| **Services** | Toàn bộ nghiệp vụ. | `CheckoutService`, `InventoryService`, `PaymentService`, `BookService`, `ReportService` |
| **Repositories** | Truy cập DB qua EF Core. | `BookRepository`, `OrderRepository`, `StockReceiptRepository` |
| **DTOs** | Dữ liệu "gọt" cho từng client. | `BookKioskDto` (Tên, Ảnh, Giá, Vị trí kệ), `BookAdminDto` (thêm giá vốn, ngày nhập) |
| **Middleware** | Xử lý xuyên suốt. | Global exception handler, request logging, rate limit |

### 3.3 Danh sách API chi tiết

**Auth API** (`/api/auth`)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/login` | Đăng nhập, trả access + refresh token |
| POST | `/refresh` | Cấp token mới |
| POST | `/logout` | Thu hồi refresh token |
| GET | `/me` | Thông tin người dùng hiện tại |

**Catalog API** (`/api/books`, `/api/categories`, `/api/areas`)
| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/books?keyword=&categoryId=&page=` | Kiosk/Admin | Tìm theo tên, tác giả, thể loại, phân trang |
| GET | `/api/books/{id}` | Kiosk/Admin | Chi tiết + vị trí kệ |
| GET | `/api/books/barcode/{code}` | Kiosk/Admin | Tra sách bằng mã vạch/QR |
| POST/PUT/DELETE | `/api/books` | Admin | Thêm/sửa/xóa (xóa mềm) |
| GET/POST/PUT/DELETE | `/api/categories`, `/api/areas` | Admin | Quản lý thể loại, khu vực kệ |
| GET | `/api/areas/{id}/map` | Kiosk | Dữ liệu bản đồ/vị trí kệ |

**Inventory API** (`/api/inventory`)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/receipts` | Tạo phiếu nhập kho (kèm chi tiết) |
| GET | `/receipts`, `/receipts/{id}` | Danh sách, chi tiết phiếu |
| GET | `/stock?lowOnly=true` | Tồn kho, lọc sách sắp hết |
| POST | `/adjust` | Điều chỉnh kiểm kê (có lý do, ghi log) |

**Order API** (`/api/orders`)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/api/orders/cart/validate` | Kiểm tra giỏ (giá hiện tại, còn hàng) — **không trừ kho** |
| POST | `/api/orders/checkout` | Tạo đơn trạng thái *ChoThanhToan* + giữ chỗ tồn kho |
| GET | `/api/orders/{id}` | Trạng thái đơn (Kiosk poll) |
| POST | `/api/orders/{id}/cancel` | Hủy, nhả giữ chỗ |
| POST | `/api/orders/counter` | Bán tại quầy (tiền mặt), Admin/ThuThu |
| GET | `/api/orders?from=&to=&status=` | Danh sách hóa đơn |

**Payment API** (`/api/payments`)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/qr` | Sinh mã QR cho đơn (số tiền, nội dung chuyển khoản có mã đơn) |
| POST | `/webhook` | Cổng thanh toán báo kết quả (xác thực chữ ký) |

**Kiosk API** (`/api/kiosks`)
| Method | Endpoint | Mô tả |
|---|---|---|
| POST | `/heartbeat` | Kiosk báo "còn sống" mỗi 30–60 giây |
| POST | `/incidents` | Báo sự cố (mất camera, lỗi in, lỗi mạng) |
| GET | `/config` | Cấu hình: timeout, nội dung màn hình chờ |
| GET | `/api/kiosks` | (Admin) danh sách + trạng thái Kiosk |

**Report API** (`/api/reports`)
`/revenue?from=&to=&groupBy=day|month`, `/top-books`, `/by-category`, `/kiosk-vs-counter`

### 3.4 Chuẩn phản hồi & lỗi
```json
{ "success": false, "code": "OUT_OF_STOCK", "message": "Sản phẩm vừa hết hàng", "data": null }
```
Các mã lỗi chính: `OUT_OF_STOCK`, `PRICE_CHANGED`, `ORDER_EXPIRED`, `PAYMENT_MISMATCH`, `UNAUTHORIZED`, `VALIDATION_ERROR`.

---

## 4. THIẾT KẾ CƠ SỞ DỮ LIỆU

### 4.1 Các bảng chính

**Nhóm Sản phẩm & Vị trí**
- `TheLoai` (MaTheLoai PK, TenTheLoai)
- `KhuVuc` (MaKhuVuc PK, TenKhuVuc, Tang, KeSach, ViTriX, ViTriY) — để Kiosk chỉ đường
- `Sach` (MaSach PK, TenSach, TacGia, NhaXuatBan, NamXuatBan, MaVach *unique*, GiaBan, GiaVon, SoLuongTon, SoLuongGiuCho, HinhAnh, MoTa, MaTheLoai FK, MaKhuVuc FK, TrangThai, **RowVersion**)

**Nhóm Giao dịch**
- `HoaDon` (MaHoaDon PK, ThoiGian, TongTien, GiamGia, TrangThai, KenhBan *[Kiosk|Quay]*, PhuongThucThanhToan, MaKiosk FK null, MaNhanVien FK null, HanThanhToan)
- `ChiTietHoaDon` (MaHoaDon FK, MaSach FK, SoLuong, **DonGiaTaiThoiDiem**, ThanhTien)
- `GiaoDichThanhToan` (MaGD PK, MaHoaDon FK, MaThamChieu *unique*, SoTien, TrangThai, ThoiGianNhan, DuLieuWebhook) — chống xử lý trùng
- `MaGiamGia` (Ma PK, LoaiGiam, GiaTri, NgayBatDau, NgayKetThuc, SoLanToiDa) *(tùy chọn)*

**Nhóm Kho**
- `PhieuNhapKho` (MaPhieu PK, NgayNhap, MaAdmin FK, NhaCungCap, GhiChu)
- `ChiTietNhapKho` (MaPhieu FK, MaSach FK, SoLuong, GiaNhap)
- `LichSuTonKho` (Id, MaSach, LoaiBienDong *[Nhap|Ban|DieuChinh|Huy]*, SoLuong, TonSau, MaThamChieu, ThoiGian) — **sổ cái kho**, phục vụ đối soát

**Nhóm Hệ thống**
- `NguoiDung` (MaND PK, TenDangNhap, MatKhauHash, HoTen, VaiTro, TrangThai)
- `RefreshToken` (Id, MaND FK, Token, HetHan, DaThuHoi)
- `Kiosk` (MaKiosk PK, TenKiosk, ViTri, ApiKeyHash, TrangThai, LanCuoiHeartbeat)
- `SuCoKiosk` (Id, MaKiosk FK, LoaiSuCo, MoTa, ThoiGian, DaXuLy)
- `NhatKyHeThong` (Id, ThoiGian, MucDo, NguoiDung, HanhDong, ChiTiet)

### 4.2 Quan hệ
```
TheLoai 1─n Sach n─1 KhuVuc
HoaDon 1─n ChiTietHoaDon n─1 Sach
HoaDon 1─n GiaoDichThanhToan
PhieuNhapKho 1─n ChiTietNhapKho n─1 Sach
Sach 1─n LichSuTonKho
Kiosk 1─n HoaDon ; Kiosk 1─n SuCoKiosk
```

### 4.3 Ràng buộc & chỉ mục
- `CHECK (SoLuongTon >= 0)` và `CHECK (SoLuongGiuCho >= 0)`.
- Index: `Sach(MaVach)` unique, `Sach(TenSach)`, `HoaDon(ThoiGian, TrangThai)`, `GiaoDichThanhToan(MaThamChieu)` unique.
- Xóa mềm cho `Sach` (giữ nguyên lịch sử hóa đơn).
- `DonGiaTaiThoiDiem` lưu giá lúc mua để đổi giá sau này không làm sai báo cáo.

### 4.4 Lưu ý về Trigger (bổ sung cho ý tưởng gốc)
Ý tưởng gốc: *"Trigger trừ SoLuongTon khi ChiTietHoaDon được tạo"*. Có hai điểm cần cân nhắc:
1. Nếu tạo `ChiTietHoaDon` ngay lúc bấm "Thanh toán" (khi mới hiện QR) thì trigger sẽ trừ kho **trước khi khách trả tiền**. Khách bỏ đi thì kho bị "treo".
2. Đặt logic trong Trigger khó test, khó debug, dễ lệch với code Service.

**Đề xuất:** đặt logic ở `CheckoutService` bên trong một transaction, và dùng Trigger/CHECK chỉ như **lớp bảo vệ cuối** (không cho tồn âm). Xem cơ chế "giữ chỗ" ở mục 6.

---

## 5. KHỐI FRONTEND 1 — WEB ADMIN

| Module | Chức năng chi tiết |
|---|---|
| **Đăng nhập & phân quyền** | JWT, Admin thấy tất cả; ThuThu chỉ thấy bán quầy, tra cứu, sự cố Kiosk |
| **Dashboard** | Doanh thu hôm nay/tuần/tháng, số sách bán, top sách, biểu đồ theo ngày, so sánh Kiosk vs Quầy, cảnh báo sách sắp hết, **trạng thái Kiosk (online/offline/lỗi)** |
| **Quản lý sách** | Bảng + tìm/lọc, thêm/sửa/xóa mềm, upload ảnh bìa, sinh/in tem mã vạch, **gắn vị trí kệ** (chọn khu vực/kệ) |
| **Quản lý thể loại & khu vực** | CRUD, sơ đồ kệ đơn giản để Kiosk hiển thị |
| **Quản lý kho** | Tạo phiếu nhập (quét mã vạch nhập nhanh), xem lịch sử, tồn kho, cảnh báo tồn thấp, kiểm kê/điều chỉnh có lý do |
| **Bán hàng tại quầy (POS)** | Quét mã vạch (máy quét USB = bàn phím ảo), giỏ hàng, thu tiền mặt/QR, in/hiển thị hóa đơn |
| **Hóa đơn** | Danh sách, lọc theo ngày/kênh/trạng thái, xem chi tiết, hủy (Admin) |
| **Giám sát Kiosk** | Danh sách Kiosk, heartbeat gần nhất, danh sách sự cố chưa xử lý, nút "Đã xử lý" |
| **Báo cáo** | Xuất Excel/PDF theo kỳ |
| **Người dùng** | (Admin) tạo tài khoản, khóa/mở, đặt lại mật khẩu |

---

## 6. NGHIỆP VỤ TRỌNG TÂM: CHỐNG TRANH CHẤP TỒN KHO

### 6.1 Vấn đề
Sách chỉ còn 1 cuốn; hai Kiosk cùng quét và cùng thanh toán.

### 6.2 Giải pháp 2 tầng

**Tầng 1 — Không trừ kho khi quét (Add to cart).**
Giỏ hàng nằm ở Kiosk. Backend chỉ `validate` để cảnh báo sớm ("Còn 1 cuốn"), không giữ gì.

**Tầng 2 — Giữ chỗ (reservation) lúc bấm "Xác nhận thanh toán", trong 1 transaction.**
```sql
BEGIN TRAN;
  -- với mỗi sách trong giỏ:
  UPDATE Sach
     SET SoLuongGiuCho = SoLuongGiuCho + @sl
   WHERE MaSach = @id
     AND (SoLuongTon - SoLuongGiuCho) >= @sl;   -- điều kiện nguyên tử
  -- nếu @@ROWCOUNT = 0 -> ROLLBACK, trả OUT_OF_STOCK
  INSERT HoaDon (TrangThai='ChoThanhToan', HanThanhToan = now + 3 phút) ...
  INSERT ChiTietHoaDon ...
COMMIT;
```
- Câu `UPDATE ... WHERE (Ton - GiuCho) >= @sl` là **nguyên tử**: khách thứ 2 sẽ nhận 0 dòng bị ảnh hưởng → báo *"Sản phẩm vừa hết hàng"*.
- Có thể thay bằng `RowVersion` (optimistic concurrency) của EF Core và bắt `DbUpdateConcurrencyException`. Cách UPDATE có điều kiện đơn giản và ít lỗi hơn.

**Tầng 3 — Chốt khi tiền về.**
Khi webhook báo thành công (hoặc thu ngân xác nhận tiền mặt): `SoLuongTon -= sl`, `SoLuongGiuCho -= sl`, ghi `LichSuTonKho`, đổi hóa đơn sang *DaThanhToan*.

**Tầng 4 — Nhả giữ chỗ.**
Background service (`IHostedService`) mỗi 30 giây quét đơn *ChoThanhToan* quá `HanThanhToan` → đổi *DaHuy/HetHan*, trừ `SoLuongGiuCho`. Kiosk hết 60 giây không thao tác cũng gọi `cancel`.

### 6.3 Máy trạng thái hóa đơn
```
ChoThanhToan ──tiền về──► DaThanhToan
     │ ├──hết hạn / khách hủy──► DaHuy
     │ └──tiền về sau khi đã hết hạn──► CanDoiSoat (xử lý thủ công/hoàn tiền)
```

---

## 7. THANH TOÁN QR

### 7.1 Luồng
1. Kiosk gọi `checkout` → nhận `MaHoaDon`, `TongTien`.
2. Kiosk gọi `POST /payments/qr` → Backend sinh chuỗi VietQR (số tiền + nội dung chuyển khoản chứa mã đơn, ví dụ `HD000123`) → Kiosk hiển thị QR + **đếm ngược**.
3. Khách quét và chuyển tiền.
4. Cổng thanh toán gọi `POST /payments/webhook`.
5. Backend: xác thực chữ ký → tra `MaThamChieu` → so khớp **đúng số tiền & đúng mã đơn** → chốt kho.
6. Kiosk **poll** `GET /orders/{id}` mỗi 2 giây (hoặc SignalR) → thấy *DaThanhToan* → màn hình "Cảm ơn".

### 7.2 Quy tắc an toàn
- **Không tin Kiosk** báo "đã thanh toán"; chỉ tin webhook/đối soát từ Backend.
- Webhook phải **idempotent**: cùng `MaThamChieu` gửi 2 lần chỉ xử lý 1 lần (unique index).
- Kiểm tra chữ ký/khóa bí mật của nhà cung cấp; chỉ nhận từ IP/endpoint đã cấu hình.
- Tiền về **sai số tiền** → đánh dấu `CanDoiSoat`, không tự chốt.
- Môi trường học tập: dùng sandbox của MoMo hoặc dịch vụ webhook ngân hàng (ví dụ payOS/SePay/Casso — cần kiểm tra điều kiện và tài liệu hiện hành của từng dịch vụ trước khi chọn).

---

## 8. KHỐI FRONTEND 2 — KIOSK APP (WPF)

### 8.1 Luồng màn hình
```
Idle (video/ảnh quảng cáo)
   │ chạm màn hình
   ▼
Home ─┬─► Tra cứu ─► Kết quả ─► Chi tiết + Bản đồ kệ
      └─► Self-Checkout (quét) ─► Giỏ hàng ─► Thanh toán QR ─► Cảm ơn ─► Idle
(mọi màn hình: 60 giây không tương tác → xóa giỏ → Idle)
```

### 8.2 Chi tiết module
| Module | Mô tả |
|---|---|
| **Idle Screen** | Phát video/ảnh sách mới (tải từ `/kiosks/config`), chạm để bắt đầu |
| **Tra cứu** | Bàn phím ảo to trên màn hình, tìm theo tên/tác giả, lọc thể loại, kết quả dạng thẻ có ảnh bìa lớn |
| **Chi tiết & vị trí** | Ảnh, giá, còn hàng hay không, **bản đồ**: tầng → khu vực → kệ (tô sáng vị trí) |
| **Self-Checkout** | Mở camera, quét QR/Barcode → gọi `/books/barcode/{code}` → thêm giỏ. Có phản hồi âm thanh/hiệu ứng khi quét thành công |
| **Giỏ hàng** | Thẻ lớn, nút +/−/Xóa lớn, tổng tiền chữ to |
| **Thanh toán** | Hiển thị QR lớn, đếm ngược (ví dụ 3 phút), nút Hủy, trạng thái chờ |
| **Kết quả** | "Thanh toán thành công" (kèm mã đơn/QR nhận hóa đơn) hoặc "Hết hạn/Thất bại" |
| **Bảo trì** | Màn hình thông báo khi lỗi thiết bị/mạng |

### 8.3 Quy tắc UX cảm ứng (kiểu McDonald's)
- Chữ tối thiểu ~24pt cho nội dung, nút cao tối thiểu ~60–80px, khoảng cách giữa nút đủ rộng.
- Tránh Table/DataGrid chữ nhỏ; dùng thẻ (card) có ảnh.
- Mỗi màn hình **một hành động chính**, màu tương phản cao, ít chữ, nhiều icon.
- Không có hover-only, không có right-click, không menu ẩn.
- Hỗ trợ đổi cỡ chữ/ngôn ngữ (VI/EN) nếu cần.

### 8.4 Timeout & bảo mật phiên
- Bộ đếm **60 giây không tương tác** (bắt sự kiện chạm/quét). Còn 10 giây hiển thị cảnh báo *"Bạn còn ở đó không?"* với nút "Tiếp tục".
- Hết giờ: `cancel` đơn đang chờ (nhả giữ chỗ) → xóa giỏ → về Idle. Không lưu gì của khách trên máy.

### 8.5 Kiến trúc nội bộ app
- **MVVM**: `View` (XAML) — `ViewModel` — `Services` (`ApiClient`, `CameraService`, `IdleTimerService`, `KioskHealthService`).
- `ApiClient` (HttpClient + Polly: retry, timeout, circuit breaker); chỉ dùng DTO dành cho Kiosk.
- **State machine** cho luồng màn hình để tránh trạng thái sai.
- Xác thực Kiosk: gửi **API key riêng của từng Kiosk** (lưu mã hóa bằng DPAPI/Windows Credential), không nhúng tài khoản người dùng.

### 8.6 Thiết bị ngoại vi & khả năng phục hồi
| Sự cố | Cách xử lý |
|---|---|
| Camera mất kết nối (lỏng USB) | `CameraService` kiểm tra định kỳ/bắt lỗi → tắt module quét, hiện thông báo bảo trì thân thiện, gọi `POST /kiosks/incidents`, **thử kết nối lại tự động** mỗi vài giây; khi có lại thì tự phục hồi |
| Mất mạng/API lỗi | Hiển thị "Tạm thời không thể thanh toán", vẫn cho tra cứu từ cache; không tạo đơn mới; tự thử lại |
| Lỗi chưa lường trước | `Application.DispatcherUnhandledException` + `AppDomain.UnhandledException` → log, quay về Idle, **không văng màn hình lỗi Windows** |
| Treo/Crash | Chạy dưới **watchdog** (app phụ hoặc Task Scheduler) tự khởi động lại; đặt app tự chạy cùng Windows |
| Kiosk chạy 24/7 | Heartbeat 30–60 giây; Backend quá 2–3 chu kỳ không thấy → đánh dấu *Offline* trên dashboard |

### 8.7 Cấu hình chế độ Kiosk trên Windows
Dùng *Assigned Access* / tài khoản Kiosk riêng, khóa phím tắt (Alt+Tab, Win), ẩn thanh taskbar, tự động đăng nhập, tắt chế độ ngủ màn hình, cập nhật Windows vào giờ đóng cửa.

---

## 9. LUỒNG NGHIỆP VỤ TỪ ĐẦU ĐẾN CUỐI

### 9.1 Khách mua sách tại Kiosk (Happy path)
```
Khách chạm → Home → Self-Checkout
→ Quét sách → Kiosk gọi /books/barcode → thêm giỏ
→ Bấm "Thanh toán" → /orders/cart/validate (kiểm tra giá, tồn)
→ /orders/checkout (giữ chỗ, tạo HD ChoThanhToan)
→ /payments/qr → hiển thị QR + đếm ngược
→ Khách chuyển tiền → webhook → chốt kho, HD = DaThanhToan
→ Kiosk poll thấy thành công → "Cảm ơn" → 10 giây → Idle
```
Nhánh lỗi: sách vừa hết → báo và cho phép bỏ món đó; hết giờ → hủy đơn, nhả kho; tiền về muộn → `CanDoiSoat`.

### 9.2 Bán tại quầy
Thủ thư đăng nhập Web → POS → quét mã vạch từng cuốn → nhận tiền mặt (hoặc QR) → `POST /orders/counter` → hóa đơn *DaThanhToan* ngay, trừ kho trong cùng transaction.

### 9.3 Nhập kho
Admin tạo `PhieuNhapKho` → quét/nhập từng sách + số lượng + giá nhập → lưu → `SoLuongTon` tăng, ghi `LichSuTonKho`. Sách mới chưa có trong danh mục thì tạo nhanh rồi nhập.

### 9.4 Xử lý sự cố Kiosk
Kiosk mất camera → gửi sự cố → Web Admin hiện cảnh báo đỏ/nhạc báo → thủ thư ra kiểm tra → bấm "Đã xử lý" → Kiosk tự báo trở lại bình thường.

---

## 10. BẢO MẬT
- **HTTPS bắt buộc**; mật khẩu băm (BCrypt/ASP.NET Identity PasswordHasher).
- JWT access token ngắn hạn (15–30 phút) + refresh token; policy theo Role.
- Kiosk dùng **API key** riêng, có thể thu hồi từng máy.
- Validate mọi input (FluentValidation), tham số hóa truy vấn (EF Core đã hỗ trợ) chống SQL Injection.
- CORS chỉ cho origin của Web Admin; Rate limit cho login và endpoint công khai của Kiosk.
- DTO cho Kiosk **không chứa** giá vốn, ngày nhập, thông tin nhân viên.
- Log hành động nhạy cảm (đổi giá, điều chỉnh kho, hủy hóa đơn) kèm người thực hiện.
- Không lưu dữ liệu cá nhân của khách; giỏ hàng chỉ ở bộ nhớ Kiosk và bị xóa khi hết phiên.

---

## 11. YÊU CẦU PHI CHỨC NĂNG
| Tiêu chí | Mục tiêu tham khảo |
|---|---|
| Hiệu năng | Tra cứu < 1 giây; quét mã → hiện sách < 1 giây |
| Sẵn sàng | Kiosk tự phục hồi lỗi; Backend có health-check `/health` |
| Nhất quán | Không bao giờ bán quá tồn kho |
| Truy vết | Mọi biến động kho đều có bản ghi `LichSuTonKho` |
| Sao lưu | Backup SQL Server hằng ngày, thử khôi phục định kỳ |
| Khả năng mở rộng | Thêm Kiosk mới chỉ cần đăng ký 1 dòng `Kiosk` + API key |
| Kiểm thử | Unit test Service (tính tiền, giữ chỗ); test tải cho tình huống 2 Kiosk cùng mua 1 cuốn |

---

## 12. KIỂM THỬ TRỌNG TÂM
1. **Race condition:** chạy 2–10 request song song mua cuốn cuối cùng → chỉ 1 thành công.
2. **Webhook trùng:** gửi cùng một webhook 2 lần → kho chỉ trừ 1 lần.
3. **Hết hạn:** tạo đơn, không thanh toán → sau hạn tồn giữ chỗ được nhả.
4. **Sai số tiền:** chuyển thiếu/thừa → đơn vào `CanDoiSoat`.
5. **Rút camera khi đang quét:** app không crash, có báo sự cố, cắm lại tự phục hồi.
6. **60 giây không thao tác:** giỏ bị xóa, đơn chờ bị hủy.
7. **Phân quyền:** ThuThu không gọi được API của Admin.

---

## 13. LỘ TRÌNH TRIỂN KHAI ĐỀ XUẤT
| Giai đoạn | Nội dung | Kết quả |
|---|---|---|
| 1. Nền tảng | Thiết kế CSDL, tạo solution, EF Core, Auth JWT | Đăng nhập được, DB chạy |
| 2. Catalog & Kho | API sách/thể loại/khu vực, nhập kho, lịch sử tồn | Web Admin quản lý sách và kho |
| 3. Bán tại quầy | Order API, transaction trừ kho, POS trên Web | Bán tiền mặt hoàn chỉnh |
| 4. Kiosk cơ bản | WPF: Idle, tra cứu, bản đồ kệ, quét camera, giỏ hàng | Kiosk chạy đến bước giỏ hàng |
| 5. Thanh toán QR | Giữ chỗ, sinh QR, webhook, đếm ngược, timeout | Self-checkout hoàn chỉnh |
| 6. Độ bền | Heartbeat, báo sự cố, watchdog, xử lý mất camera/mạng | Chạy ổn định 24/7 |
| 7. Báo cáo & hoàn thiện | Dashboard, xuất báo cáo, kiểm thử tải, tài liệu | Sẵn sàng demo/bảo vệ |

---

## 14. RỦI RO & PHƯƠNG ÁN
| Rủi ro | Phương án |
|---|---|
| Webhook thật khó tích hợp khi làm đồ án | Dùng sandbox/giả lập webhook bằng endpoint test, hoặc nút "Giả lập tiền về" chỉ bật ở môi trường Dev |
| Camera quét kém với mã mờ/phản chiếu | Chọn camera có lấy nét tự động, chiếu sáng đều, cho phép nhập mã thủ công dự phòng |
| Sách chưa dán mã vạch | Quy trình in tem mã vạch từ Web Admin khi nhập kho |
| Giá thay đổi khi khách đang trong giỏ | `validate` trả `PRICE_CHANGED`, Kiosk hiển thị giá mới để khách xác nhận lại |
| Phạm vi quá lớn | Ưu tiên MVP (mục 1.3), để mượn/trả và mã giảm giá ở giai đoạn sau |

---

## 15. TÓM TẮT NHỮNG ĐIỂM MỞ RỘNG SO VỚI Ý TƯỞNG BAN ĐẦU
1. Bổ sung **giữ chỗ tồn kho** (`SoLuongGiuCho`) vì thanh toán QR là bất đồng bộ; đặt logic ở Service + transaction, Trigger chỉ là chốt chặn.
2. Bổ sung bảng `GiaoDichThanhToan`, `LichSuTonKho`, `Kiosk`, `SuCoKiosk`, `NguoiDung`, `RefreshToken`, `NhatKyHeThong`.
3. Xác thực Kiosk bằng **API key theo máy**, tách khỏi tài khoản thủ thư.
4. Webhook **idempotent** + đối soát số tiền; thêm trạng thái `CanDoiSoat`.
5. Đề xuất **WPF + MVVM** cho Kiosk, kèm watchdog, heartbeat, xử lý lỗi toàn cục.
6. Danh sách API đầy đủ, chuẩn mã lỗi, kịch bản kiểm thử và lộ trình theo giai đoạn.

---

## 16. CẤU HÌNH DEMO TRÊN 1 LAPTOP WINDOWS (webcam laptop làm camera Kiosk)

Kiến trúc ở các mục 2–15 **giữ nguyên**. Khi demo, chỉ khác ở chỗ *triển khai*: cả 4 thành phần chạy trên cùng một máy và Kiosk dùng webcam tích hợp.

```
┌───────────────────── 1 Laptop Windows ─────────────────────┐
│  SQL Server (Express/LocalDB)                               │
│  ASP.NET Core API  ◄── https://localhost:5001               │
│  Web Admin (trình duyệt)  ──► gọi localhost:5001            │
│  Kiosk WPF (toàn màn hình) ──► gọi localhost:5001           │
│  Webcam laptop ──► CameraService của Kiosk                  │
└─────────────────────────────────────────────────────────────┘
       ▲ (tùy chọn) tunnel ngrok/cloudflared ◄── webhook thanh toán
       ▲ Điện thoại quét QR thanh toán trên màn hình laptop
```

### 16.1 Cái gì KHÔNG đổi
- Kiosk vẫn là client gọi API, không nối thẳng DB; logic giữ chỗ/trừ kho vẫn ở Backend.
- API, CSDL, luồng thanh toán, timeout 60 giây, heartbeat: giữ nguyên. Chỉ đổi địa chỉ API trong file cấu hình Kiosk thành `localhost`.

### 16.2 Cái cần điều chỉnh khi demo
| Hạng mục | Bản đầy đủ (mục 8) | Khi demo trên laptop |
|---|---|---|
| Camera | Camera USB gắn Kiosk | Webcam laptop. Vẫn dùng ZXing.Net + `CameraService`, chỉ cần chọn đúng camera index |
| Chất lượng quét | Camera lấy nét tốt, chiếu sáng riêng | Webcam thường **lấy nét cố định**: in mã to, đưa cách 15–25 cm, đủ sáng, tránh giấy bóng phản chiếu. Nên có ô **nhập mã thủ công** dự phòng |
| Xung đột camera | Chỉ Kiosk dùng camera | Camera chỉ 1 ứng dụng dùng tại một thời điểm. **Không bật quét camera trong Web Admin (trình duyệt)** cùng lúc; POS trên Web dùng máy quét USB hoặc nhập mã tay |
| Cảm ứng | Màn hình cảm ứng | Dùng chuột/touchpad (hoặc chạm nếu laptop có màn hình cảm ứng). Giữ nút to để phần trình bày vẫn đúng ý tưởng Kiosk |
| Chế độ Kiosk Windows (8.7) | Assigned Access, khóa phím tắt, watchdog | Bỏ qua khi demo: chạy app cửa sổ toàn màn hình (F11-style). Chỉ trình bày như phần "hướng triển khai thực tế" |
| Webhook thanh toán | Cổng thanh toán gọi server công khai | Cổng thanh toán **không gọi được `localhost`**. Chọn 1 trong 2: (a) dùng tunnel (ngrok/cloudflared) trỏ vào API, hoặc (b) nút **"Giả lập tiền về"** chỉ bật ở môi trường Dev, gọi đúng endpoint webhook |
| HTTPS | Chứng chỉ thật | `dotnet dev-certs https --trust` hoặc dùng HTTP trên localhost khi demo |
| CSDL | SQL Server | SQL Server Express/LocalDB cài sẵn trên laptop |

### 16.3 Kịch bản demo các tình huống đặc biệt
- **Mất camera:** không rút cáp được, nên mô phỏng bằng cách *Disable* camera trong Device Manager hoặc tắt quyền camera trong Windows Settings → Kiosk phải báo bảo trì, gửi sự cố lên Web Admin; bật lại thì tự phục hồi.
- **Race condition 2 Kiosk:** không cần 2 máy. Dùng **Postman/script gửi 2 request `checkout` song song** cho cuốn cuối cùng (chỉ 1 thành công), hoặc mở 2 cửa sổ Web/Swagger. Tránh mở 2 bản Kiosk cùng lúc vì cả hai đều đòi webcam.
- **Timeout 60 giây:** demo bình thường; có thể cho phép giảm xuống 15–20 giây bằng `/kiosks/config` để tiết kiệm thời gian trình bày.
- **Thanh toán QR thật:** hiển thị QR trên màn hình laptop, quét bằng điện thoại. Không xung đột với webcam vì điện thoại là thiết bị khác.
- **Kiosk offline:** demo bằng cách tắt Kiosk hoặc chặn heartbeat; Web Admin sau vài chu kỳ hiển thị *Offline*.

### 16.4 Lưu ý khi trình bày
Nói rõ trong báo cáo: *"Demo chạy trên 1 máy cho tiện; kiến trúc vẫn tách Backend/Web/Kiosk và có thể triển khai Kiosk trên máy riêng chỉ bằng cách đổi địa chỉ API."* Điều này thể hiện kiến trúc "Backend 1 cho 2" không bị phụ thuộc vào cách demo.
