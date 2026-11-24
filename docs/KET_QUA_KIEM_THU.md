# KẾT QUẢ KIỂM THỬ END-TO-END & ĐỒNG THỜI (QL_DatVeMayBay V2)

- **Ngày thực hiện kiểm thử:** 24/11/2026
- **Môi trường chạy thật:** ASP.NET Core MVC (.NET 8) tại `http://localhost:5099` kết nối trực tiếp SQL Server `QL_DatVeMayBay` trên instance mặc định `.` (`MSSQLSERVER`).
- **Kết quả tổng hợp:** **15/15 kịch bản đạt (PASS)** (10 kịch bản cốt lõi + 5 kịch bản quy trình Vietjet 4 bước) — 0 lỗi build, 0 warning, 0 ngoại lệ runtime.

---

## 1. Bảng kết quả kiểm thử chi tiết

| Mã Test | Kịch bản kiểm thử | Kết quả | Bằng chứng quan sát thực tế |
|---|---|---|---|
| `V1_SearchSGNtoDLI_MatrixAndRealPhoto` | Tìm chuyến `SGN → DLI` hiển thị ảnh thật Đà Lạt (`DLI.jpg`) và ma trận 3 hạng vé (`FIR / BUS / ECO`) | **PASS** | `CB = VJ620-261127`, ảnh `DLI.jpg` trả HTTP 200 |
| `V2_PassengersPage_DynamicPassportFix` | Bước 2 (`/Booking/Passengers`): đổi loại giấy tờ sang `Hộ chiếu` tự cập nhật nhãn `Số Hộ chiếu *`, placeholder & ô Hạn hộ chiếu | **PASS** | JS `syncDocFields` kích hoạt đồng bộ trên giao diện |
| `V3_PostPassengersRedirectsToServices` | Lưu thông tin hành khách đã validate và điều hướng sang Bước 3 (`/Booking/SelectServices`) | **PASS** | HTTP 302 → `/Booking/SelectServices` |
| `V4_SelectServices_Exact3Cards` | Bước 3 chỉ hiển thị Chọn chỗ ngồi, Hành lý/Nối chuyến, Suất ăn; bỏ hoàn toàn Taxi, Dịch vụ theo hành khách & Thú cưng | **PASS** | `Has3=True`, `OmitsTaxiPet=True` |
| `V5_Hold15Min_And_VietQRPayment` | Giữ chỗ 15p tự động xếp ghế theo hạng + dịch vụ (`BG20`, `ML01`) → Thanh toán VietQR Callback thành công | **PASS** | Sinh PNR `PNR2611247163`, tổng `1.725.000đ`, `HoaDon=1`, vé `Đã xuất vé` |
| `T1_HomeAndImagery` | Trang chủ chỉ hỗ trợ Một chiều & Khứ hồi (đã loại bỏ Nhiều chặng), tải ảnh SVG điểm đến & hãng bay | **PASS** | `GET /` = 200; có `Một chiều`, `Khứ hồi`, không chứa `Nhiều chặng`; `PQC.svg` & `VN.svg` trả HTTP 200 |
| `T2_LowestPriceCalendarAPI` | API lịch giá vé thấp nhất theo tháng (`GET /Home/GiaTheoThang?di=SGN&den=HAN&thang=12&nam=2026`) | **PASS** | Trả về đủ 31 ngày tháng 12/2026, có 7 ngày `laNgayReNhat = true`, giá rút gọn dạng `k` |
| `T3_TransitSearchSP` | Tìm chuyến bay nối chuyến qua Stored Procedure `sp_TimChuyenQuaCanh` (`HPH -> VCS`) | **PASS** | Tự động ghép cặp chặng `HPH -> SGN -> VCS` thỏa thời gian nối chuyến hợp lệ |
| `T4_PBKDF2LoginAndAdminDash` | Đăng nhập khách hàng & nhân viên bằng PBKDF2-SHA256 + truy cập Admin Dashboard | **PASS** | `duyenan@gmail.com` & `admin` đăng nhập HTTP 302; `GET /Admin/Dashboard` = 200 |
| `T5_ConcurrencyRace5Threads` | 5 luồng HTTP gửi POST đồng thời tranh chấp đúng 1 ghế trên chuyến `VN120` | **PASS** | `SeatId = 1180`: **1/5 luồng thắng** (redirect `/Payment`), 4 luồng còn lại bị chặn; DB `COUNT(*) = 1` |
| `T6_IntlPassport6MonthRule` | Ràng buộc Hộ chiếu còn hạn ≥ 6 tháng cho chặng bay quốc tế (`VN909: SGN -> SIN`) | **PASS** | Hộ chiếu hết hạn 23/01/2027 (chưa đủ 6 tháng sau ngày bay) bị chặn ngay ở server và quay về `SelectSeats` |
| `T7_RoundTripSinglePNR_IdempotentPay` | Đặt vé Khứ hồi (`VN120 + VN121`, 1 Người lớn + 1 Trẻ em) trong **1 PNR duy nhất** + gọi `POST /Payment/Callback` **2 lần** | **PASS** | Sinh 1 PNR `PNR2611243126` chứa 4 vé (2 chặng × 2 khách); phát lại callback vẫn chỉ sinh đúng **1 `HoaDon`** |
| `T8_PostBookingAddonRequiresPayment` | Mua thêm hành lý `BG20` (220.000đ) sau khi đặt vé | **PASS** | Tạo dịch vụ trạng thái `Chờ thanh toán`, chuyển hướng sang VietQR (`DICH_VU`), chỉ chuyển `Đã thanh toán` sau Callback |
| `T9_VAT72h_CorpAndOver72hBlock` | Xuất hóa đơn VAT doanh nghiệp có MST trong 72h & chặn hóa đơn quá 72h (`PNR00020` - 96h) | **PASS** | `RequestVatInvoice` MST `0318202699` trả 200 kèm đọc số thành chữ tiếng Việt; `PNR00020` quá 72h bị chặn (302) |
| `T10_CancelTicketReleasesSeat` | Hủy 1 vé trên `VN120` & kiểm tra hoàn ghế trống + khả năng đặt lại (Filtered Unique Index) | **PASS** | `SoGheTrong` của `VN120` tăng `61 -> 62`, ghế vừa hủy xuất hiện lại trong `GetAvailableSeats` |

---

## 2. Xác nhận quy mô dữ liệu CSDL (`Database/QL_DatVeMayBay.sql`)
- `HangHangKhong`: 5 | `SanBay`: 23 | `TuyenBay`: 54 | `MayBay`: 22 | `HangGhe`: 3 | `GheNgoi`: 1,448
- `ChuyenBay`: 4,787 (phủ từ hôm nay đến +185 ngày, gồm đủ 12/2026 & 01–02/2027)
- `DichVuBoSung`: 10 | `NhanVien`: 10 | `KhachHang`: 35 | `KhuyenMai`: 10
- `PhieuDatCho`: 105 (mẫu khởi tạo ban đầu) | `ChangBayDatCho`: 126 | `VeMayBay`: 126 | `HoaDon`: 84 | `DanhGiaChuyenBay`: 3
