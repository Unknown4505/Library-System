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
* **1.3.4. Hoàn tất thanh toán Kiosk**
  * **Input:** Tín hiệu từ Backend (SignalR/Polling) thông báo đơn hàng đã Paid.
  * **Output:** Giao diện hiển thị "Thanh toán thành công", hiển thị điểm vừa được cộng thêm, sinh file hóa đơn PDF để hiển thị.

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

### 2.5. Báo cáo & Thống kê
* **2.5.1. Xem Dashboard Doanh thu**
  * **Input:** Khoảng thời gian (Ngày/Tuần/Tháng).
  * **Output:** Biểu đồ/bảng số liệu doanh thu tổng, có phân nhóm Kiosk vs Bán tại quầy.

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
  * **Output:** Tìm trong danh sách các CTKM đang Active, lọc ra các CTKM thỏa mãn điều kiện `MinOrderValue <= SubTotal`, tự động chọn 1 CTKM có mức giảm `DiscountAmount` cao nhất để áp dụng. Trả về mức chiết khấu.
