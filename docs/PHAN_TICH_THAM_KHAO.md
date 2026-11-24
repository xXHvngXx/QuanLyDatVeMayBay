# PHÂN TÍCH THAM KHẢO NGHIỆP VỤ & UX ĐẶT VÉ MÁY BAY

Tài liệu tổng hợp kết quả khảo sát thực tế từ **Bước 0** (truy cập trình duyệt tới `https://newweb.vietjetair.com/vi` và `https://www.momo.vn`) kết hợp phân tích nghiệp vụ hàng không nội địa nhằm thiết kế nâng cấp hệ thống **SkyVN Air (QL_DatVeMayBay)**.

---

## 1. Kết quả quan sát trực tiếp từ Vietjet Air (`https://newweb.vietjetair.com/vi`)

### Những gì quan sát được trực tiếp trên giao diện công khai
- **Thanh điều hướng (Header)**: Tách rõ các nhóm chức năng chính mà khách hàng cần ngay lập tức: *Đặt vé / Tìm chuyến bay*, *Chuyến bay của tôi (Manage Booking)*, *Online Check-in*, *Dịch vụ chuyến bay*, chọn ngôn ngữ/tiền tệ (`VI / VND`), và *Đăng nhập*.
- **Form tìm kiếm nhỏ gọn (Search Widget)**:
  - Các trường cốt lõi xếp theo dòng ngang dễ quét bằng mắt: **Điểm khởi hành (`Từ`)**, nút **Đảo chiều hai sân bay (`Swap`)**, **Điểm đến (`Đến`)**, **Ngày đi (`DD/MM/YYYY`)**, **Hành khách** (mặc định `1 Người lớn, 0 Trẻ em, 0 Em bé`), và CTA nổi bật **Tìm chuyến bay**.
  - Các tab sản phẩm nằm phía trên form giúp người dùng không bị rối.
- **Giới hạn gặp phải khi khảo sát tự động**:
  - Khi bấm mở dropdown `Từ / Điểm khởi hành` để tương tác sâu hơn vào lịch giá vé, trang kích hoạt subframe chéo nguồn tới `https://www.google.com` (anti-bot / reCAPTCHA frame) và bị chặn điều hướng (`NAVIGATION_BLOCKED`). Theo quy tắc an toàn hệ thống, quá trình khảo sát trình duyệt trên Vietjet dừng lại tại đây và **không** vượt rào chống bot.

### Phần suy luận nghiệp vụ áp dụng cho đồ án (không quan sát trực tiếp do giới hạn anti-bot)
- **Một chiều / Khứ hồi (không có Nhiều chặng)**: Loại bỏ hoàn toàn tùy chọn Multi-city để tập trung vào hai luồng chuẩn của hàng không nội địa: `MOT_CHIEU` và `KHU_HOI`, mặc định validation `NgayVe >= NgayDi`.
- **Lưới lịch giá thấp nhất theo ngày/tháng**: Hiển thị đơn giá rút gọn dạng `890k`, `1.130k`, tô màu xanh nổi bật cho ngày rẻ nhất trong tháng, vô hiệu hóa các ngày không có chuyến bay mở bán.
- **Thẻ chuyến bay & phân tách giá**: Cần tách bạch rõ *Giá cơ bản (Base Fare)*, *Hệ số hạng ghế*, *Hệ số đối tượng hành khách* (Người lớn 100%, Trẻ em 90%, Em bé 10% giá cơ bản), và *Thuế & Phí sân bay* trước khi ra tổng thanh toán.

---

## 2. Kết quả quan sát trực tiếp từ MoMo (`https://www.momo.vn`)

### Phát hiện cấu trúc chính
- `momo.vn` đóng vai trò **cổng giới thiệu & chuyển tiếp sang ứng dụng di động (Desktop-to-App QR Handoff)**: khi nhấn *Đặt vé ngay* tại `/ve-may-bay`, website hiển thị modal mã QR kèm hướng dẫn 3 bước để quét mở app MoMo thay vì mở màn hình thanh toán web trực tiếp.

### Những gì quan sát được trực tiếp từ giao diện Đặt vé máy bay trên MoMo (`https://www.momo.vn/ve-may-bay`)
1. **Giao diện trang đặt vé máy bay & thẻ chặng bay**:
   - Khu vực đối tác hàng không hiển thị rõ các hãng bay nội địa (`Vietnam Airlines`, `Vietjet Air`, `Bamboo Airways`, `Pacific Airlines`).
   - Trang chặng bay nổi bật (`/ve-may-bay/chang-bay/...`) trình bày trực quan thời gian bay ước tính, khoảng giá tham khảo đã gồm thuế phí, ảnh điểm đến và mô tả ngắn.
2. **Quy trình đặt vé máy bay 4 bước chuẩn**:
   - Tìm kiếm (Điểm đi, Điểm đến, Ngày đi/về, Số lượng khách) → Chọn chuyến đi / chuyến về → Nhập thông tin hành khách (Họ tên, Danh xưng/Giới tính, Ngày sinh, **CMND/CCCD hoặc Hộ chiếu**, SĐT & Email liên hệ) + Dịch vụ bổ sung → Chọn phương thức thanh toán và xác nhận.
3. **Minh bạch giá vé**:
   - Hiển thị rõ giá cuối cùng đã bao gồm thuế, phí để người dùng nắm tổng chi phí trước khi xác nhận đặt vé.
4. **Quản lý đặt chỗ & Hóa đơn VAT**:
   - Sau khi đặt vé thành công, hệ thống lưu mã đặt chỗ (`PNR`) vào mục quản lý đặt chỗ để theo dõi lịch bay, tra cứu vé và yêu cầu xuất hóa đơn GTGT đúng hạn.
5. **Giới hạn quan sát trực tiếp trên web `momo.vn`**:
   - Khi nhấn nút `ĐẶT VÉ NGAY`, website desktop hiển thị modal quét mã QR chuyển sang ứng dụng di động thay vì mở trực tiếp form chọn ghế/checkout trên trình duyệt. Vì vậy phần chọn ghế, bộ đếm ngược giữ chỗ 15 phút và màn hình VietQR Sandbox trên web được thiết kế bổ sung theo chuẩn cổng thanh toán VietQR thực tế.

---

## 3. Bảng tổng hợp các quyết định thiết kế rút ra cho SkyVN Air

| Hạng mục | Thực trạng code cũ | Thiết kế nâng cấp dựa trên phân tích tham khảo |
|---|---|---|
| **Tìm chuyến** | Dropdown `<select>` thô, không đảo chiều, mặc định ngày cứng `2027-01-01`, không tách Người lớn / Trẻ em / Em bé | Chỉ còn **Một chiều / Khứ hồi**, ô chọn sân bay tìm theo tên thành phố & mã IATA + nút **Swap**, bộ đếm `NL / TE (2-11t) / EB (<2t, <= NL)`, tổng tối đa 9 khách |
| **Lịch giá vé** | Không có API/UI xem giá theo ngày | API `GET /Home/GiaTheoThang` + `sp_GiaThapNhatTheoNgay`: lưới lịch theo tháng hiện giá thấp nhất mỗi ngày (`890k`), tô xanh ngày rẻ nhất |
| **Nối chuyến (Transit)** | Chỉ hỗ trợ điểm dừng kỹ thuật tĩnh trên 1 `ChuyenBay` | Ghép 2 chặng thật trong `ChuyenBay` qua `sp_TimChuyenQuaCanh` (nối chuyến 60p nội địa / 120p quốc tế, tối đa 8h, tổng hành trình ≤ 24h) |
| **Khứ hồi** | Tách làm 2 `MaDatCho` (PNR) rời nhau | Hợp nhất **1 PNR (`PhieuDatCho`) duy nhất** chứa tất cả vé lượt đi, lượt về và chặng nối chuyến qua bảng `ChangBayDatCho`, thanh toán 1 lần, rollback đồng bộ |
| **Giấy tờ tùy thân** | Nhập tự do 1 chuỗi CCCD/Hộ chiếu | Bắt buộc chọn **CCCD (12 số)** hoặc **Hộ chiếu (6–9 ký tự + hạn ≥ 6 tháng sau ngày bay quốc tế)**, **Giấy khai sinh** cho Trẻ em/Em bé gắn với Người lớn đi cùng, kiểm tra trùng giấy tờ trên cùng chuyến bay |
| **Giữ chỗ & Đồng thời** | `HanThanhToan = +24 giờ`, `UNIQUE (MaChuyenBay, MaGhe)` chặn đặt lại ghế đã hủy | Giữ chỗ **15 phút**, Filtered Unique Index chỉ khóa ghế còn hiệu lực, `BEGIN TRAN` + `UPDLOCK, HOLDLOCK` bắt lỗi `1205/2627/2601`, SignalR realtime 4 trạng thái ghế |
| **Thanh toán & Dịch vụ thêm** | Submit form là đánh dấu đã thanh toán luôn (kể cả mua thêm dịch vụ) | Bảng `GiaoDichThanhToan` + `SandboxPaymentGateway` VietQR HMAC-verified & idempotent; mua thêm hành lý/suất ăn/đổi ghế sau đặt vé bắt buộc tạo giao dịch `DICH_VU` thanh toán VietQR thật |
| **Hóa đơn VAT 72h** | Mới check cơ bản chưa có thông tin doanh nghiệp/MST và số tiền bằng chữ | Chặn tuyệt đối sau 72h từ lúc thanh toán (cả vé và dịch vụ thêm), validate MST 10/13 số, 1 hóa đơn duy nhất/PNR, bản in VAT chuẩn có đọc số thành chữ tiếng Việt |
