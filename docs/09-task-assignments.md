# Tiến độ Dự án & Phân công công việc — BookKiosk

Tài liệu này quy định tiến độ thực hiện dự án (15 tuần) và phân công công việc chi tiết cho từng thành viên theo từng Module.

## 1. Bảng tiến độ 15 Tuần

| Tuần | Thời gian | Nội dung công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Kết quả mong đợi |
|:---:|---|---|:---:|:---:|:---:|---|
| **1** | Tuần 1 | Phân tích yêu cầu, chốt kiến trúc & Database | [x] | [ ] | [ ] | Hoàn thành Docs, Setup GitHub, Db Schema |
| **2** | Tuần 2 | Khởi tạo Solution, cấu hình EF Core | [x] | [ ] | [ ] | Project chạy được, kết nối SQL Server |
| **3** | Tuần 3 | API: CRUD Sách, Danh mục, Khu vực | [ ] | [ ] | [ ] | Các API cơ bản sẵn sàng |
| **4** | Tuần 4 | Kiosk WPF: Dàn Layout, Trang chủ, Tìm kiếm | [ ] | [ ] | [ ] | Kiosk có UI cơ bản, binding data giả |
| **5** | Tuần 5 | API: Thành viên (Điểm), Checkout | [ ] | [ ] | [ ] | Logic giữ kho, tính tiền hoạt động |
| **6** | Tuần 6 | Kiosk WPF: Giỏ hàng, Quét mã vạch | [ ] | [ ] | [ ] | Kiosk quét được sách, tính tổng tiền |
| **7** | Tuần 7 | API: Tích hợp SePay Webhook | [ ] | [ ] | [ ] | Tự động sinh QR và xác nhận thanh toán |
| **8** | Tuần 8 | Kiosk WPF: Màn hình QR, In hóa đơn | [ ] | [ ] | [ ] | Thanh toán thành công, in hóa đơn ảo/thật |
| **9** | Tuần 9 | CMS MVC: Giao diện Admin, Dashboard | [ ] | [ ] | [ ] | CMS CRUD được sách, danh mục |
| **10** | Tuần 10 | CMS MVC: Quản lý Kho, Khuyến mãi | [ ] | [ ] | [ ] | Xử lý nghiệp vụ kho bãi, nhập hàng |
| **11** | Tuần 11 | **Kiểm thử tích hợp & Sửa lỗi** | [ ] | [ ] | [ ] | Toàn bộ luồng Kiosk và CMS trơn tru |
| **12** | Tuần 12 | Chỉnh sửa UI/UX, Báo cáo thống kê | [ ] | [ ] | [ ] | Tối ưu trải nghiệm, xuất báo cáo doanh thu |
| **13** | Tuần 13 | Viết Báo cáo đồ án (Word/PDF) | [ ] | [ ] | [ ] | Hoàn thiện tài liệu kiến trúc, Database |
| **14** | Tuần 14 | Deploy IIS, Setup máy thật (nếu có) | [ ] | [ ] | [ ] | Chạy demo trên môi trường giả lập/thật |
| **15** | Tuần 15 | **Review & Nộp đồ án** | [ ] | [ ] | [ ] | Nộp mã nguồn và báo cáo |

---

## 2. Phân công công việc chi tiết theo Module

> **Quy ước:** 
> - **Leader (@Unknown4505):** Kiến trúc, Review PR, Database, Support.
> - **BE (@thien33):** Phụ trách WebAPI, xử lý logic Application, Entity Framework.
> - **FE (@lehuukhang):** Phụ trách Kiosk WPF và giao diện CMS MVC.

### MODULE 0: SETUP & DATABASE (Tuần 1-2)
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Ghi chú | Trạng thái |
|---|---|:---:|:---:|:---:|---|---|
| 0.1 | Khởi tạo Solution, 6 Projects theo cấu trúc | [x] | | | | `[x]` |
| 0.2 | Tạo file Markdown Docs, CODEOWNERS | [x] | | | | `[x]` |
| 0.3 | Code Domain Entities & Enums | | [ ] | | Chờ Leader review | `[ ]` |
| 0.4 | Cấu hình DbContext & Configurations | | [ ] | | | `[ ]` |
| 0.5 | Chạy Migration & Seed Data mẫu | | [ ] | | Cần ít nhất 50 sách | `[ ]` |

### MODULE 1: CORE API & KIOSK UI (Tuần 3-4)
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Ghi chú | Trạng thái |
|---|---|:---:|:---:|:---:|---|---|
| 1.1 | API: `GET /api/books` (Phân trang, tìm kiếm) | | [ ] | | | `[ ]` |
| 1.2 | API: `GET /api/recommendations` | | [ ] | | Top bán chạy, mới | `[ ]` |
| 1.3 | Kiosk: Layout `MainWindow` (Navigation) | | | [ ] | Setup Frame | `[ ]` |
| 1.4 | Kiosk: `HomePage` (Slider, Gợi ý) | | | [ ] | Dùng data giả trước | `[ ]` |
| 1.5 | Kiosk: `SearchPage` (Danh sách sách) | | | [ ] | | `[ ]` |

### MODULE 2: CHECKOUT & PAYMENT (Tuần 5-8)
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Ghi chú | Trạng thái |
|---|---|:---:|:---:|:---:|---|---|
| 2.1 | API: `POST /api/orders/checkout` | | [ ] | | Tính tiền, giữ kho | `[ ]` |
| 2.2 | API: Tích hợp SePay (Sinh QR) | | [ ] | | Sinh mã VNPay/PayOS | `[ ]` |
| 2.3 | API: Tích hợp SePay (Webhook xác nhận) | | [ ] | | Kiểm tra chữ ký bảo mật | `[ ]` |
| 2.4 | Kiosk: `CartPage` & Tích hợp máy quét mã vạch | | | [ ] | Quét ISBN/Barcode | `[ ]` |
| 2.5 | Kiosk: `PaymentPage` (Hiển thị QR & Đếm ngược) | | | [ ] | Poll trạng thái liên tục | `[ ]` |
| 2.6 | Kiosk: Sinh file PDF & Gọi API máy in nhiệt | | | [ ] | Hỗ trợ in Mock (Demo) | `[ ]` |

### MODULE 3: CMS ADMIN WEB (Tuần 9-10)
| # | Công việc | Leader | BE (@thien33) | FE (@lehuukhang) | Ghi chú | Trạng thái |
|---|---|:---:|:---:|:---:|---|---|
| 3.1 | CMS: CRUD Danh mục, Sách (`BooksController`) | | | [ ] | Gọi API hoặc tiêm Service | `[ ]` |
| 3.2 | CMS: Nhập kho (`InventoryController`) | | | [ ] | | `[ ]` |
| 3.3 | CMS: Tạo Khuyến mãi (`PromotionsController`) | | | [ ] | | `[ ]` |
| 3.4 | API: Các Endpoint phục vụ CMS CRUD | | [ ] | | Nếu tách biệt Backend | `[ ]` |

---

## 3. Tổng hợp khối lượng công việc

| Thành viên | Số đầu việc chính | Module phụ trách |
|---|---|---|
| **@Unknown4505** | ~10 | Kiến trúc, Code Review, Kiểm thử |
| **@thien33** | ~20 | Toàn bộ API Core, Webhook Thanh toán, Database |
| **@lehuukhang** | ~20 | Giao diện WPF, Tích hợp phần cứng, Giao diện Web CMS |

---

## 4. Lưu ý Phụ thuộc (Dependencies)

**dY" QUY TẮC VÀNG (LÀM SONG SONG):**
- **FE (@lehuukhang)** KHÔNG CẦN CHỜ **BE (@thien33)** làm xong API! Hãy dựng giao diện WPF và CMS bằng dữ liệu JSON giả (Mock Data) hoặc ViewModel cứng.
- Khi BE hoàn thành API, FE chỉ việc thay Data giả bằng lời gọi `HttpClient` (hoặc `Fetch` trong CMS).
- BE làm tới đâu, viết Unit Test (hoặc test Postman) tới đó để đảm bảo API không bị chết giữa chừng làm sập Kiosk.
