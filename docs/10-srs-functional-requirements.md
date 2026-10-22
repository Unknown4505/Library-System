# Đặc tả Yêu cầu Chức năng (Functional Requirements) - Cấp độ 1, 2, 3

Tài liệu này đặc tả các yêu cầu phần mềm theo cấu trúc phân cấp, chi tiết đến các chức năng cụ thể cùng Đầu vào (Input) và Đầu ra (Output) dựa trên nghiệp vụ của hệ thống BookKiosk.

---

## 1. Hệ thống Kiosk (Khách hàng tự phục vụ)

### 1.1. Tra cứu và Gợi ý Sách
* **1.1.1. Hiển thị gợi ý sách**
  * **Input:** Request màn hình trang chủ (khi Kiosk ở trạng thái Idle).
  * **Output:** Danh sách sách Bán chạy nhất, Tìm kiếm nhiều nhất, Mới nhập kho (hiển thị ảnh bìa, tên sách, giá, số lượng tồn kho khả dụng).
* **1.1.2. Tìm kiếm sách**
  * **Input:** Từ khóa tìm kiếm (nhập tên sách, tên tác giả, hoặc thể loại).
  * **Output:** Danh sách sách khớp từ khóa, hiển thị trạng thái "Còn hàng" (Available > 0) hoặc "Hết hàng".
* **1.1.3. Xem vị trí kệ sách**
  * **Input:** Lựa chọn Xem vị trí của một cuốn sách cụ thể.
  * **Output:** Bản đồ/Sơ đồ cửa hàng có highlight vị trí kệ chứa sách đó.

### 1.2. Giỏ hàng và Checkout (Self-Checkout)
* **1.2.1. Thêm sản phẩm qua quét mã vạch**
  * **Input:** Tín hiệu quét từ Webcam (Mã vạch/ISBN) khi khách đưa sách vào.
  * **Output:** Thông vị sách được thêm vào giỏ hàng hiển thị trên màn hình. Nếu số lượng thêm vượt quá `AvailableStock`, cảnh báo "Sách đã hết hàng".
* **1.2.2. Nhập mã thủ công [DEV-ONLY]**
  * **Input:** Mã sách nhập bằng tay qua bàn phím ảo (chỉ khả dụng môi trường Development).
  * **Output:** Sách được thêm vào giỏ (tương tự 1.2.1).

### 1.3. Thanh toán và Thẻ Thành viên tại Kiosk
* **1.3.1. Xác thực thẻ thành viên**
  * **Input:** Số điện thoại nhập từ màn hình Kiosk.
  * **Output:** Thông tin thành viên (Tên hiển thị, số điểm tích lũy hiện có).
* **1.3.2. Áp dụng điểm tích lũy**
  * **Input:** Số điểm khách muốn sử dụng (tối đa 100 điểm = 100,000 VNĐ).
  * **Output:** Số tiền thanh toán được trừ đi tương ứng (`TotalAmount = SubTotal - Discount - Points`).
* **1.3.3. Tạo đơn hàng & Chờ thanh toán QR**
  * **Input:** Yêu cầu Thanh toán với danh sách sách, điểm TV sử dụng, ID của máy Kiosk.
  * **Output:** Tạo đơn hàng ở trạng thái `Pending`, cộng dồn vào `ReservedQuantity` (tạm giữ kho), tạo và hiển thị QR Code động SePay, bắt đầu đếm ngược timeout 3-4 phút.
* **1.3.4. ProductDiscount và chọn Promotion tốt nhất**
  * **Input:** Giỏ hàng cùng các Promotion active trong khoảng UTC, có detail đúng `PromotionType`.
  * **Output:** Backend tính benefit thực tế của từng Promotion, chọn tối đa một Promotion có benefit lớn nhất; đồng hạng chọn `PromotionId ASC`. ProductDiscount giảm mỗi đơn vị tối đa bằng `UnitPriceAtTime`.
* **1.3.5. Promotion kết hợp điểm và zero payable**
  * **Input:** Promotion đã chọn và số điểm hợp lệ do khách yêu cầu.
  * **Output:** Cho phép Promotion + points, không stacking nhiều Promotion. Nếu tổng cuối `<= 0`, trả 400 `BAD_REQUEST`, rollback toàn bộ và không tạo QR/Pending.
* **1.3.6. Snapshot tại Pending**
  * **Input:** Checkout đã chọn Promotion và tính tổng hợp lệ.
  * **Output:** Persist promotion name/type, points amount, book title, unit price và discount per unit trong cùng transaction; không tính lại khi Paid.
* **1.3.7. Hoàn tất thanh toán Kiosk**
  * **Input:** Tín hiệu từ Backend (SignalR/Polling) thông báo đơn hàng đã Paid.
  * **Output:** Giao diện hiển thị "Thanh toán thành công", hiển thị điểm vừa được cộng thêm, sinh file hóa đơn PDF để hiển thị; hoàn tất Receipt rồi dọn phiên và về `SearchPage`.

### 1.4. Idle Advertising và vòng đời điều hướng — PLANNED

* **1.4.1. Auto Ads khi Idle**
  * **Input:** App vừa khởi động hoặc Kiosk chuyển về `IdlePage`.
  * **Output:** Slideshow tự động; mặc định đề xuất 8 giây/slide và có thể cấu hình; không tạo timer/request trùng.
* **1.4.2. Current Promotions**
  * **Input:** Dữ liệu promotion từ Backend.
  * **Output:** Chỉ hiển thị promotion `IsActive` trong `StartDate..EndDate`; `promotionType=1` dùng order discount, `promotionType=2` dùng danh sách product discount; tiền là numeric và không suy diễn mức giảm.
* **1.4.3. New Arrivals**
  * **Input:** Backend lấy lần nhập gần nhất từ `ImportReceipt.CreatedAt`/`ImportReceiptDetail.BookId` trong rolling 30 ngày UTC.
  * **Output:** Tối đa 10 sách active và `AvailableStock > 0`, mỗi sách một lần; sort lần nhập gần nhất giảm dần rồi `BookId ASC`; hiển thị ảnh bìa, tên, giá, tồn và lần nhập; không dùng `Books.CreatedAt` hoặc mock/local inference.
* **1.4.4. Best Sellers**
  * **Input:** Backend tổng `OrderDetails.Quantity` từ order `Paid` có `CompletedAt` trong rolling 30 ngày UTC.
  * **Output:** Tối đa 10 sách active và `AvailableStock > 0`; sort tổng bán giảm dần rồi `BookId ASC`; không tính cart local, `Pending` hoặc `Cancelled`.
* **1.4.5. Touch to Start**
  * **Input:** Khách chạm màn hình quảng cáo.
  * **Output:** Dừng/dispose slideshow timer và điều hướng tới `SearchPage`.
* **1.4.6. Inactivity to Idle**
  * **Input:** Không có tương tác hợp lệ trong 60 giây tại `SearchPage`.
  * **Output:** Chuyển về Idle Advertising; không áp dụng khi phiên member/checkout/payment/receipt đang hoạt động.
* **1.4.7. Paid Receipt to Search**
  * **Input:** Backend xác nhận `Paid`, Receipt đã tải/hiển thị/in an toàn và hết mốc 5 giây hiện hành.
  * **Output:** Xóa cart và state giao dịch đã hoàn tất, không gọi cancel, điều hướng `SearchPage`; sau đó inactivity timer mới bắt đầu phiên mới.
* **1.4.8. Error/Offline fallback**
  * **Input:** API advertising lỗi, timeout hoặc không có dữ liệu hợp lệ.
  * **Output:** Không crash; bỏ nhóm rỗng và hiển thị “Chạm để bắt đầu”/asset tĩnh đã duyệt; không giả promotion thực tế.
* **1.4.9. Contract response, cache và retry**
  * **Input:** `ApiResponseDto<KioskAdvertisingDto>` từ `GET /api/kiosk/advertising`.
  * **Output:** FE xử lý shape cố định, enum 1/2, nullable image, empty arrays, `success/code/message/data`; HTTP 200 `PARTIAL_SUCCESS` với allow-list `unavailableSections`; HTTP 503 `SERVICE_UNAVAILABLE` khi toàn bộ nguồn lỗi; cache tối đa 5 phút nhưng không vượt `endDate`; không request mỗi slide; tối đa một retry mỗi lượt Idle sau ít nhất 30 giây và fallback/cooldown an toàn.
* **1.4.10. An toàn ProductDiscount**
  * **Input:** Contract có thể chứa `promotionType=2` và danh sách `productDiscounts` theo từng sách.
  * **Output:** Không quảng cáo hoặc tự tính ProductDiscount như ưu đãi Kiosk cho đến khi Checkout thực tế hỗ trợ và được xác minh; mở rộng Checkout là scope riêng, không thuộc Advertising #37/#38.

---

## 2. Hệ thống Quản trị (Web Admin / CMS)

### 2.1. Quản lý Danh mục và Tồn kho
* **2.1.1. Cập nhật thông tin Danh mục sách**
  * **Input:** Form thông tin (Tên sách, Tác giả, ISBN, Giá, Thể loại, Ảnh).
  * **Output:** Dữ liệu sách được cập nhật trong CSDL.
* **2.1.2. Lập Phiếu nhập kho**
  * **Input:** Phiếu nhập gồm (Nhà cung cấp, danh sách sách nhập, số lượng mỗi loại, giá nhập).
  * **Output:** Phiếu nhập được lưu lại, tự động tăng số lượng `StockQuantity` thực tế.

### 2.2. Bán hàng tại quầy (POS)
* **2.2.1. Tạo đơn hàng POS**
  * **Input:** Danh sách sách (quét bằng USB scanner), thông tin thành viên, thao tác click áp dụng chiết khấu/khuyến mãi (thủ công từ nhân viên).
  * **Output:** Đơn hàng POS trạng thái `Pending`, cộng `ReservedQuantity` (tạm giữ kho).
* **2.2.2. Xác nhận thanh toán tiền mặt / mã QR**
  * **Input:** Lệnh click "Đã nhận đủ tiền" từ nhân viên sau khi nhận tiền từ khách.
  * **Output:** Chuyển trạng thái đơn thành `Paid`, trừ kho vật lý `StockQuantity`, tích điểm cho khách (nếu có), xuất dữ liệu hóa đơn.
* **2.2.3. Hủy đơn hàng POS**
  * **Input:** Lệnh "Hủy đơn" từ nhân viên POS.
  * **Output:** Đổi trạng thái `Cancelled`, nhả kho `ReservedQuantity` trở lại.

### 2.3. Quản lý Thẻ Thành viên
* **2.3.1. Đăng ký thành viên mới**
  * **Input:** Số điện thoại, Họ tên khách hàng.
  * **Output:** Tài khoản thành viên mới được khởi tạo (Điểm ban đầu = 0).

### 2.4. Quản lý Khuyến mãi (Promotion)
* **2.4.1. Thiết lập quy tắc Khuyến mãi**
  * **Input:** Tham số KM (Điều kiện tổng đơn tối thiểu, Số tiền giảm, Ngày bắt đầu - kết thúc, trạng thái).
  * **Output:** Luật khuyến mãi được lưu để Backend tự động tính toán (ở tính năng 3.3.1).
* **2.4.2. ProductDiscount nhiều sách**
  * **Input:** Promotion type 2 cùng danh sách không rỗng các `bookId` duy nhất và `discountAmount > 0`.
  * **Output:** Persist toàn bộ detail; mỗi sách có mức giảm riêng, không dùng phần trăm.
* **2.4.3. PUT replace-all**
  * **Input:** Toàn bộ state đích của Promotion và detail.
  * **Output:** Validate trước, thay thế detail trong một transaction; lỗi rollback, không để bản ghi cũ dư lại và không tính lại đơn đã chốt.

### 2.5. Báo cáo & Thống kê
* **2.5.1. Xem Dashboard Doanh thu**
  * **Input:** Khoảng thời gian (Ngày/Tuần/Tháng).
  * **Output:** Biểu đồ/bảng số liệu doanh thu tổng, có phân nhóm Kiosk vs Bán tại quầy.
* **2.5.2. Cảnh báo tồn kho thấp trên Dashboard Admin — APPROVED 21/10/2026**
  * **Nguồn dữ liệu:** Toàn bộ đầu sách do Backend trả về từ `GET /api/inventory/stock?lowOnly=true` theo contract tại [API Reference](04-api-reference.md).
  * **Low Stock KPI:** Tổng số đầu sách đang thuộc nhóm tồn kho thấp trên toàn hệ thống; KPI không bị giới hạn bởi số mục preview.
  * **Low Stock Preview:** Tối đa 10 đầu sách có `AvailableStock` thấp nhất, sắp xếp tăng dần để ưu tiên xử lý; không tính trùng đầu sách.
  * Khi Backend trả hơn 10 cảnh báo, ví dụ 18 bản ghi, KPI phải là 18 còn preview chỉ hiển thị 10.
  * Dashboard phải có đường dẫn đến trang quản lý tồn kho để xem danh sách chi tiết.
  * **Trạng thái triển khai:** Đây là business requirement đã được Leader duyệt. Implementation PR #40 tại HEAD `84bc85d` vẫn tính KPI từ danh sách đã giới hạn `.Take(10)` và phải được sửa, targeted retest trước khi nghiệm thu.

---

## 3. Hệ thống Backend API (Xử lý nghiệp vụ ngầm)

### 3.1. Tích hợp Thanh toán SePay
* **3.1.1. Nhận & Xác thực Webhook**
  * **Input:** Request HTTP POST gửi từ SePay (gồm Signature, Mã tham chiếu - ReferenceCode, Số tiền, Nội dung ck).
  * **Output:** 
    * Nếu sai Signature: Trả HTTP 401.
    * Nếu trùng `ReferenceCode` (Idempotent): Bỏ qua, trả HTTP 200.
    * Nếu khớp đơn hàng: Trừ kho `StockQuantity` và `ReservedQuantity`, đổi trạng thái đơn thành `Paid`, cộng điểm tích lũy cho member.
    * Nếu số tiền nhận không khớp đơn (khách sửa sai số tiền): Đánh dấu bất thường, không trừ kho, báo lỗi để nhân viên xử lý thủ công.

### 3.2. Background Worker
* **3.2.1. Nhả kho tự động (Timeout Cleanup)**
  * **Input:** Định kỳ mỗi 1 phút (Cron job), truy vấn các đơn hàng trạng thái `Pending` tạo trước thời điểm timeout (vd quá 4 phút).
  * **Output:** Đổi trạng thái các đơn vi phạm thành `Cancelled`, trừ đi `ReservedQuantity` để số lượng khả dụng `AvailableStock` tăng trở lại.

### 3.3. Promotion Engine
* **3.3.1. Tự động áp dụng Khuyến mãi (Kiosk)**
  * **Input:** Tổng giá trị giỏ hàng (`SubTotal`) khi khách bấm Thanh toán trên Kiosk.
  * **Output:** Tính benefit OrderDiscount/ProductDiscount hợp lệ, tự động chọn một CTKM có benefit cao nhất; đồng hạng `PromotionId ASC`.
* **3.3.2. POS chọn Promotion thủ công**
  * **Input:** `promotionId` do nhân viên chọn.
  * **Output:** Chỉ áp Promotion đó nếu hợp lệ; nếu không hợp lệ trả lỗi có message, không tự thay thế.
* **3.3.3. Receipt snapshot và legacy**
  * **Input:** Order Paid.
  * **Output:** Trả persisted totals/snapshot. Đơn legacy fallback title và points theo rule lịch sử; promotion name/type có thể nullable, không suy đoán ProductDiscount.

---

## 4. Yêu cầu target bổ sung và phạm vi

### 4.1 Nhân viên, tài khoản và vai trò — PLANNED

- Lưu hồ sơ `Employees` độc lập với `Members`; một Employee có tối đa một tài khoản User và User có tối đa một Employee.
- Giữ `Admin=1`, `Staff=2`, thêm `Manager=3`; vô hiệu hóa thay vì xóa lịch sử.
- Auth/User/Employee API và CMS chưa được triển khai trong scaffold này.

### 4.2 Sổ lương cơ bản — PLANNED

- Mỗi nhân viên tối đa một `SalaryPayment` cho mỗi tháng/năm, trạng thái `Draft` hoặc `Paid`.
- Lưu snapshot lương cơ bản, phụ cấp, khấu trừ, lương thực nhận, người/thời điểm chốt; tiền không âm.
- Không bao gồm thuế, bảo hiểm, payroll phức tạp hoặc chuyển tiền ngân hàng.

### 4.3 Hóa đơn Kiosk — PLANNED #28/#29

- Chỉ đơn `Paid` có `CompletedAt` hợp lệ được lấy hóa đơn; dữ liệu từ `Orders`/`OrderDetails`, không có bảng `Invoices`.
- Kiosk hiển thị/in/PDF từ `ReceiptDto`; không dùng giá/tổng local cart; không lộ giá vốn hoặc PII.

### 4.4 Ngoài phạm vi

Không có `BookTypes` (`Categories` là loại sách), đặt hàng online/pre-order, bảng giữ chỗ khách hàng hoặc quản trị sự cố thiết bị như quy trình kinh doanh chính. `Kiosks` và `KioskIncidents` vẫn thuộc schema kỹ thuật.
