# 📖 Hướng dẫn sử dụng Tài liệu — BookKiosk

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

> **Đọc file này TRƯỚC KHI đọc bất kỳ file nào khác.**
> Mục tiêu: mỗi thành viên biết chính xác — mình làm phần nào, đọc gì, khi nào, và không phải đoán.

---

## 1. Tổng quan hệ thống (1 phút nắm bắt)

**BookKiosk** là hệ thống bán sách tự phục vụ gồm 3 thành phần:

```
┌──────────────────────────────────┬──────────────────────────────────────┐
│  Kiosk App (WPF)                 │  Web Admin (ASP.NET MVC)             │
│  - Tìm sách (tên/thể loại)       │  - Thủ thư / Admin quản lý          │
│  - Xem tồn kho (còn/hết hàng)   │  - Bán tại quầy, xem báo cáo       │
│  - Xem vị trí kệ sách           │  - Quản lý kho, nhập hàng          │
│  - Quét mã & thanh toán QR        │  - Giám sát trạng thái Kiosk        │
│  - Xuất hóa đơn (PDF) sau TT     │                                      │
│  [DEV] Nhập mã thủ công          │  [DEV] Nhập mã thủ công (POS)     │
└──────────────┬───────────────────┴────────────────┬─────────────────────┘
               │            HTTPS / REST            │
               └──────────────────┬─────────────────┘
                                  ▼
                  ┌───────────────────────────────┐
                  │  ASP.NET Core Web API         │
                  │  Controller → Service → Repo  │──► SQL Server
                  └───────────────────────────────┘
                                  │
                                  ▼
                  SePay webhook ← ngrok tunnel (local dev)
```

**Phạm vi MVP:** Chỉ bán sách. Không có mượn/trả.

---

## 2. Bản đồ tài liệu (Docs Map)

```
docs/
├── 00-how-to-use-docs.md       ← BẠN ĐANG Ở ĐÂY — đọc đầu tiên
├── 01-overview.md              ← Kiến trúc, actors, công nghệ, nguyên tắc thiết kế
├── 02-project-structure.md     ← Cấu trúc thư mục từng project, naming convention đầy đủ
├── 03-database.md              ← Schema CSDL: bảng, cột, kiểu dữ liệu, index, DDL mẫu
├── 04-api-reference.md         ← Đặc tả từng endpoint: URL, method, request/response JSON mẫu
├── 05-business-pipeline.md     ← Luồng nghiệp vụ: checkout, giữ chỗ kho, webhook SePay
├── 06-local-setup.md           ← Cài đặt và chạy toàn bộ project trên máy local
├── 07-security.md              ← JWT, API key Kiosk, phân quyền, CORS, webhook signature
└── 08-kiosk-app.md             ← WPF: màn hình, MVVM, Services, xử lý hardware
```

> ⚠️ **Quy tắc bất biến:** Không tạo file `.md` mới để giải thích từng hàm/class.
> Đó là việc của **XML comment trong C#** (xem mục 7).
> File `.md` chỉ dùng cho kiến trúc, luồng nghiệp vụ, contract giữa các module.

---

## 3. Phân công thành viên — Ai làm gì, file nào của ai

> ⚠️ **Leader sẽ cập nhật bảng này sau khi chốt phân công.**
> Điền GitHub username thật vào đây và vào file `.github/CODEOWNERS`.

### 👑 Leader (bạn) — PM, Architect, System Integrator, Code Reviewer
**GitHub username:** `@<dien-username-vao-day>`

**Phụ trách:**
```
.github/CODEOWNERS              ← chỉ Leader được sửa
docs/                           ← maintain toàn bộ docs
BookKiosk.Domain/Entities/      ← Entity class (source of truth cho cả nhóm)
BookKiosk.Application/Interfaces/ ← Interface / contract giữa các layer
BookKiosk.Infrastructure/Data/  ← DbContext, EF Configurations, Migrations
```

**Đọc:** Tất cả file docs từ `01` → `08`

---

### 👤 Member 1 — [Chưa phân công]
**GitHub username:** `@<dien-username-vao-day>`

**Phụ trách:** *(Leader điền sau)*
```
[Để trống — chờ Leader phân công]
```

**Đọc trước khi code:** *(Leader điền sau)*

---

### 👤 Member 2 — [Chưa phân công]
**GitHub username:** `@<dien-username-vao-day>`

**Phụ trách:** *(Leader điền sau)*
```
[Để trống — chờ Leader phân công]
```

**Đọc trước khi code:** *(Leader điền sau)*

---

> 📌 **Các phần việc cần phân công** (Leader chọn ai làm gì):
>
> | Phần việc | Thư mục | Docs cần đọc trước |
> |---|---|---|
> | Backend API | `BookKiosk.API/`, `BookKiosk.Application/Services/`, `BookKiosk.Infrastructure/Repos/`, `BookKiosk.Infrastructure/Payment/` | `03` → `04` → `05` → `07` |
> | WPF Kiosk App | `BookKiosk.Kiosk/Pages/`, `BookKiosk.Kiosk/ViewModels/`, `BookKiosk.Kiosk/Services/` | `08` → `04` → `05` |
> | CMS Web Admin | `BookKiosk.CMS/Controllers/`, `BookKiosk.CMS/Views/`, `BookKiosk.CMS/ViewModels/` | `04` → `03` → `07` |

---

## 4. File dùng chung — Phải báo Leader trước khi sửa

> Sửa các file này ảnh hưởng đến **tất cả mọi người**.

| File / Thư mục | Ai có thể sửa | Cần báo ai |
|---|---|---|
| `BookKiosk.Domain/Entities/` | Bất kỳ | **Leader + cả nhóm** — thêm/xóa cột ảnh hưởng Migration và toàn bộ layer |
| `BookKiosk.Application/Interfaces/` | Bất kỳ | **Leader + cả nhóm** — đổi Interface phá vỡ contract |
| `BookKiosk.Application/DTOs/` | Bất kỳ | **Leader + cả nhóm** — đổi DTO ảnh hưởng API response và Kiosk |
| `appsettings.json` | Bất kỳ | **Cả nhóm** |
| `BookKiosk.Infrastructure/Data/BookKioskDbContext.cs` | Leader | **Cả nhóm** |
| `.github/CODEOWNERS` | **Leader only** | — |
| `docs/02-project-structure.md` | Bất kỳ | **Cả nhóm** — naming convention thay đổi |
| `docs/04-api-reference.md` | Member phụ trách Backend | **Cả nhóm** — API contract thay đổi |
| `docs/03-database.md` | Leader | **Cả nhóm** — schema thay đổi |

---

## 5. Reading path theo Role

| Role | Thứ tự đọc bắt buộc |
|---|---|
| **Tất cả thành viên** | `00` → `01` → `02` (trước mọi thứ) |
| **Backend API** | tiếp theo: `03` → `04` → `05` → `07` → `06` |
| **WPF Kiosk** | tiếp theo: `08` → `04` → `05` → `06` |
| **CMS Web Admin** | tiếp theo: `04` → `03` → `07` → `06` |
| **Leader** | tất cả: `01` → `02` → `03` → `04` → `05` → `06` → `07` → `08` |

---

## 6. Checklist Onboarding — Khi mới vào nhóm

Làm theo thứ tự, không bỏ bước:

```
[ ] 1. Đọc file này (00-how-to-use-docs.md) — xác định role của mình ở mục 3
[ ] 2. Đọc 01-overview.md — hiểu hệ thống tổng quan
[ ] 3. Đọc 02-project-structure.md — thuộc naming convention
[ ] 4. Làm theo 06-local-setup.md — clone repo, chạy được API trên máy local
[ ] 5. Mở Swagger UI: http://localhost:5001/swagger — gọi thử GET /api/books
[ ] 6. Đọc các file docs theo thứ tự role của mình (xem mục 5)
[ ] 7. Tạo branch: git checkout -b feat/<module>-<mo-ta-ngan>
[ ] 8. Code task nhỏ đầu tiên → push → mở Pull Request
```

---

## 7. Hai lớp tài liệu — Viết gì ở đâu

| Lớp | Nơi lưu | Nội dung | Ai maintain |
|---|---|---|---|
| **Kiến trúc & Contract** | `docs/*.md` | Luồng nghiệp vụ, schema DB, API spec, naming convention | Leader + người phụ trách phần đó |
| **Implementation detail** | XML comment trong `.cs` | Giải thích hàm làm gì, tham số, return, ví dụ gọi | Người viết hàm đó |

### Khi nào tạo file `.md` mới?
- Workflow nhiều bước span qua nhiều file → `docs/*.md`
- Contract giữa các module → `docs/*.md`
- Hướng dẫn team → `docs/*.md`

### Khi nào KHÔNG tạo file `.md`?
- Giải thích hàm/class cụ thể → **XML comment trong `.cs`**

### Chuẩn XML comment tối thiểu (C#)

```csharp
/// <summary>
/// Tạo đơn hàng, giữ chỗ tồn kho trong 1 transaction.
/// Trả về lỗi OUT_OF_STOCK nếu tồn kho không đủ.
/// </summary>
/// <param name="request">Thông tin giỏ hàng từ Kiosk.</param>
/// <returns>OrderDto chứa OrderId và thời hạn thanh toán.</returns>
/// <exception cref="OutOfStockException">Khi sách vừa hết trong lúc giữ chỗ.</exception>
public async Task<OrderDto> CheckoutAsync(CheckoutRequestDto request)
```

---

## 8. Git Workflow hàng ngày

```
1. Pull code mới nhất
   git pull origin main

2. Tạo branch riêng
   git checkout -b feat/<module>-<mo-ta-ngan>
   Ví dụ: feat/checkout-api, feat/cart-page, fix/stock-race-condition

3. Code + commit thường xuyên
   git add <files>
   git commit -m "<type>(<scope>): <mô tả ngắn>"

4. Push + mở Pull Request
   git push origin feat/checkout-api
   → Mở PR → GitHub tự tag người review theo CODEOWNERS

5. Người được tag review → approve → Leader merge vào main
```

### Commit message format

```
<type>(<scope>): <mô tả ngắn tiếng Việt hoặc Anh>

type:
  feat     — thêm tính năng mới         [MVP]
  fix      — sửa bug
  docs     — chỉ thay đổi docs
  refactor — tái cấu trúc, không đổi behavior
  test     — thêm/sửa test
  chore    — cấu hình, dependency, gitignore

scope: tên module (checkout, cart, payment, kiosk, cms, db, auth, ...)

Ví dụ đúng ✅:
  feat(checkout): implement stock reservation with transaction
  fix(payment): handle duplicate webhook by reference code
  docs(api): add POST /orders/checkout request/response example
  refactor(cart): extract CartService from CartViewModel

Ví dụ sai ❌:
  update code
  fix bug
  thêm tính năng mới
  WIP
```

### CODEOWNERS hoạt động thế nào?

```
Bạn sửa code → push lên branch → mở Pull Request
                                        │
               GitHub nhìn vào .github/CODEOWNERS
                                        │
               Tự động tag đúng người phụ trách file đó vào PR
                                        │
               PR chỉ được merge sau khi người đó approve
```

> Không ảnh hưởng đến việc chạy code local. Chỉ kiểm soát merge vào `main`.

---

## 9. Naming Convention nhanh

> Chi tiết đầy đủ ở `02-project-structure.md`. Đây là bảng tra nhanh.

| Hạng mục | Convention | Ví dụ đúng ✅ | Ví dụ sai ❌ |
|---|---|---|---|
| Class / Entity | `PascalCase` | `Book`, `OrderDetail` | `book`, `order_detail` |
| Interface | `I` + `PascalCase` | `IBookRepository` | `BookRepositoryInterface` |
| Method | `PascalCase` | `CheckoutAsync()` | `checkout()`, `do_checkout()` |
| Property | `PascalCase` | `TotalAmount`, `CreatedAt` | `totalAmount` |
| Private field | `_camelCase` | `_bookRepo` | `bookRepo`, `m_repo` |
| DTO | Tên + `Dto` | `BookKioskDto` | `BookKioskData` |
| ViewModel (WPF) | Tên + `ViewModel` | `CartViewModel` | `CartVM` |
| API endpoint | `kebab-case`, số nhiều | `/api/stock-receipts` | `/api/StockReceipts` |
| Tên bảng DB | `PascalCase`, số nhiều | `Books`, `OrderDetails` | `book`, `tbl_order` |
| Tên cột DB | `PascalCase` | `BookId`, `TotalAmount` | `book_id` |
| Branch Git | `type/mo-ta` | `feat/checkout-api` | `feature1`, `fixbug` |

---

## 10. Ký hiệu dùng xuyên suốt Docs

| Ký hiệu | Ý nghĩa |
|---|---|
| ✅ | Đã chốt, không thay đổi |
| ⚠️ | Cần lưu ý, dễ nhầm |
| 🔒 | Liên quan bảo mật — bắt buộc thực hiện |
| 📌 | Quan trọng với đồ án / demo |
| `[MVP]` | Thuộc phạm vi MVP — phải làm |
| `[LATER]` | Tính năng giai đoạn sau — chưa làm |
| `[DEV-ONLY]` | Chỉ bật ở môi trường Development |
| `→` | Dẫn đến / tiếp theo trong luồng |
| `❌` | Không được làm / sai pattern |

---

## 11. Nguyên tắc cốt lõi — 8 điều bắt buộc

```
✅ 1. Logic tính tiền, trừ kho, phân quyền → CHỈ nằm ở Backend API
✅ 2. Kiosk là client "mỏng" → chỉ hiển thị số Backend trả về, không tự tính
✅ 3. Hiển thị tồn kho thực tế (còn/hết) → đọc từ Backend, không giữ local
✅ 4. Mọi thay đổi tồn kho phải có bản ghi StockHistories
✅ 5. API key / connection string → LUÔN từ appsettings/.env, không hardcode
✅ 6. Cập nhật docs cùng lúc với merge code thay đổi schema/API/luồng
🔒 7. [DEV-ONLY] Ô nhập mã thủ công (Kiosk + POS) CHỈ hiển khi `env=Development`
       Không bao giờ để thiết bị Kiosk thật có cửa tắt này
🚫 8. KHÔNG tạo Entity hoặc viết HttpClient trước khi Leader confirm
       docs/03-database.md và docs/04-api-reference.md
       → Thiếu contract → conflict schema, gọi sai API, deadlock giữa các member
```

---

## 12. Checklist trước khi commit code

```
[ ] XML comment có ở tất cả method public (summary, param, returns)
[ ] Không hardcode connection string, API key, secret trong code
[ ] Đặt tên biến/hàm/bảng theo naming convention (xem mục 9)
[ ] Nếu thêm/sửa API       → cập nhật docs/04-api-reference.md
[ ] Nếu thêm/sửa bảng/cột  → cập nhật docs/03-database.md + chạy Migration
[ ] Nếu thay đổi luồng     → cập nhật docs/05-business-pipeline.md
[ ] Commit message đúng format (xem mục 8)
```

---

## 13. Câu hỏi thường gặp (FAQ)

| Câu hỏi | Trả lời |
|---|---|
| Đặt file mới ở thư mục nào? | Đọc `02-project-structure.md` |
| Request/response API trông như thế nào? | Đọc `04-api-reference.md` — có JSON mẫu từng endpoint |
| Tại sao bảng `Books` có cột `ReservedQuantity`? | Đọc `05-business-pipeline.md` → Cơ chế giữ chỗ tồn kho |
| Chạy project bị lỗi DB / SePay / ngrok? | Đọc `06-local-setup.md` → Troubleshooting |
| Token JWT hết hạn khi test? | Đọc `07-security.md` → Refresh token flow |
| Muốn thêm tính năng mới? | Đọc `05` → `04` → báo Leader → tạo branch `feat/...` |
| Sửa file dùng chung có cần hỏi không? | Có — xem mục 4, báo Leader trước |
| PR của tôi bị tag ai review? | Tùy file bạn sửa — xem `.github/CODEOWNERS` |
