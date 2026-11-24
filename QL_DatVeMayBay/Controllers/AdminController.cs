using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using QL_DatVeMayBay.Hubs;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class AdminController : Controller
    {
        private readonly IHubContext<SeatHub> _hubContext;
        private readonly string _connStr;

        public AdminController(IHubContext<SeatHub> hubContext, IConfiguration config)
        {
            _hubContext = hubContext;
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        private bool IsStaffLoggedIn() => HttpContext.Session.GetString("UserType") == "NV";
        private int CurrentRoleId() => HttpContext.Session.GetInt32("RoleID") ?? 0;

        public IActionResult Dashboard()
        {
            if (!IsStaffLoggedIn())
                return RedirectToAction("Login", "Account");

            // KPI tổng quan (đồng bộ trạng thái Hóa đơn = 'Đã thanh toán')
            ViewBag.TongDoanhThu = Convert.ToDecimal(DatabaseHelper.ExecuteScalar(
                "SELECT ISNULL(SUM(TongTien), 0) FROM HoaDon WHERE TrangThai = N'Đã thanh toán'"));
            ViewBag.TongVeDaBan = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM VeMayBay WHERE TrangThai IN (N'Đã xuất vé', N'Đã Check-in')"));
            ViewBag.SoChuyenMoBan = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM ChuyenBay WHERE TrangThai = N'Đang mở bán' AND NgayGioBay > GETDATE()"));
            ViewBag.TongKhachHang = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM KhachHang"));
            ViewBag.PendingPaymentsCount = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                "SELECT COUNT(*) FROM GiaoDichThanhToan WHERE TrangThai = N'Chờ duyệt'"));

            ViewBag.DoanhThuTheoHang = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_BaoCaoDoanhThuTheoHang ORDER BY TongDoanhThuVe DESC");
            ViewBag.TopTuyenBay = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_TopTuyenBayDatNhieuNhat");
            ViewBag.DanhSachChuyenBay = DatabaseHelper.ExecuteQuery("SELECT TOP 100 * FROM vw_DanhSachChuyenBayChiTiet WHERE NgayGioBay >= DATEADD(DAY, -1, GETDATE()) ORDER BY NgayGioBay ASC");
            ViewBag.DanhSachDonHang = DatabaseHelper.ExecuteQuery("SELECT TOP 40 * FROM vw_LichSuDatVeKhachHang ORDER BY NgayDat DESC");
            ViewBag.DanhSachGiaoDichCK = DatabaseHelper.ExecuteQuery(@"
                SELECT TOP 40 g.MaGiaoDich, g.MaDatCho, g.LoaiGiaoDich, g.SoTien, g.NoiDungCK, g.TrangThai,
                       g.AnhChuyenKhoanUrl, g.NgayGuiMinhChung, g.NgayDuyet, g.LyDoTuChoi, g.GhiChu, g.NgayTao,
                       pdc.NguoiLienHe, kh.TenKH, kh.SDT, ISNULL(nv.TenNV, N'') AS TenNVDuyet
                FROM GiaoDichThanhToan g
                JOIN PhieuDatCho pdc ON g.MaDatCho = pdc.MaDatCho
                JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
                LEFT JOIN NhanVien nv ON g.MaNV_Duyet = nv.MaNV
                WHERE g.AnhChuyenKhoanUrl IS NOT NULL OR g.TrangThai IN (N'Chờ duyệt', N'Chờ thanh toán')
                ORDER BY CASE WHEN g.TrangThai = N'Chờ duyệt' THEN 0 WHEN g.TrangThai = N'Chờ thanh toán' THEN 1 ELSE 2 END,
                         ISNULL(g.NgayGuiMinhChung, g.NgayTao) DESC;");
            ViewBag.DanhSachHuyDoi = DatabaseHelper.ExecuteQuery("SELECT TOP 30 * FROM YeuCauHuyDoiVe ORDER BY NgayYeuCau DESC");
            ViewBag.DanhSachKhuyenMai = DatabaseHelper.ExecuteQuery("SELECT * FROM KhuyenMai ORDER BY NgayKetThuc DESC");
            ViewBag.DanhSachNhanVien = DatabaseHelper.ExecuteQuery("SELECT nv.*, vt.TenRole FROM NhanVien nv JOIN VaiTro vt ON nv.RoleID = vt.RoleID");
            ViewBag.DanhSachDanhGia = DatabaseHelper.ExecuteQuery("SELECT dg.*, kh.TenKH FROM DanhGiaChuyenBay dg JOIN KhachHang kh ON dg.MaKH = kh.MaKH ORDER BY dg.NgayDanhGia DESC");

            ViewBag.TuyenBays = DatabaseHelper.ExecuteQuery("SELECT MaTuyenBay FROM TuyenBay ORDER BY MaTuyenBay");
            ViewBag.MayBays = DatabaseHelper.ExecuteQuery("SELECT MaMayBay, TenMayBay, MaHang FROM MayBay WHERE TrangThai = N'Sẵn sàng' ORDER BY MaHang, MaMayBay");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddFlight(string maChuyenBay, string maTuyenBay, string maMayBay, DateTime ngayGioBay, DateTime ngayGioDenDK, decimal giaVeCoBan)
        {
            if (!IsStaffLoggedIn() || CurrentRoleId() != 1)
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc mới có quyền thêm lịch bay!";
                return RedirectToAction("Dashboard");
            }

            if (ngayGioDenDK <= ngayGioBay)
            {
                TempData["Error"] = "Thời gian hạ cánh phải sau giờ khởi hành.";
                return RedirectToAction("Dashboard");
            }

            try
            {
                int isIntl = Convert.ToInt32(DatabaseHelper.ExecuteScalar(@"
                    SELECT CASE WHEN sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam' THEN 1 ELSE 0 END
                    FROM TuyenBay tb
                    JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
                    JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
                    WHERE tb.MaTuyenBay = @MaTB",
                    CommandType.Text, new SqlParameter("@MaTB", maTuyenBay)));
                decimal minFare = isIntl == 1 ? QL_DatVeMayBay.Services.FlightPricingHelper.InternationalMinBaseFare : QL_DatVeMayBay.Services.FlightPricingHelper.DomesticMinBaseFare;
                if (giaVeCoBan < minFare || giaVeCoBan > QL_DatVeMayBay.Services.FlightPricingHelper.MaxBaseFare)
                {
                    TempData["Error"] = $"Giá vé cơ bản {(isIntl == 1 ? "quốc tế" : "nội địa")} phải nằm trong khung hợp lý {minFare:N0}đ – {QL_DatVeMayBay.Services.FlightPricingHelper.MaxBaseFare:N0}đ.";
                    return RedirectToAction("Dashboard");
                }
                int thoiGianBay = (int)(ngayGioDenDK - ngayGioBay).TotalMinutes;
                int tongGhe = Convert.ToInt32(DatabaseHelper.ExecuteScalar(
                    "SELECT TongSoGhe FROM MayBay WHERE MaMayBay = @MaMB",
                    CommandType.Text,
                    new SqlParameter("@MaMB", maMayBay)));

                DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai)
                    VALUES (@MaCB, @MaTB, @MaMB, @GioBay, @GioDen, @ThoiGian, @Gia, @SoGhe, N'Đang mở bán')",
                    CommandType.Text,
                    new SqlParameter("@MaCB", maChuyenBay.Trim().ToUpperInvariant()),
                    new SqlParameter("@MaTB", maTuyenBay),
                    new SqlParameter("@MaMB", maMayBay),
                    new SqlParameter("@GioBay", ngayGioBay),
                    new SqlParameter("@GioDen", ngayGioDenDK),
                    new SqlParameter("@ThoiGian", thoiGianBay),
                    new SqlParameter("@Gia", giaVeCoBan),
                    new SqlParameter("@SoGhe", tongGhe));

                TempData["Success"] = $"Đã thêm lịch chuyến bay {maChuyenBay.ToUpperInvariant()} thành công!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi thêm chuyến bay: " + ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateFlightStatus(string maChuyenBay, string trangThai, decimal? giaMoi)
        {
            if (!IsStaffLoggedIn() || CurrentRoleId() != 1)
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc mới có quyền điều chỉnh lịch bay!";
                return RedirectToAction("Dashboard");
            }

            if (giaMoi.HasValue && giaMoi.Value > 0 && (giaMoi.Value < QL_DatVeMayBay.Services.FlightPricingHelper.DomesticMinBaseFare || giaMoi.Value > QL_DatVeMayBay.Services.FlightPricingHelper.MaxBaseFare))
            {
                TempData["Error"] = $"Giá vé cơ bản điều chỉnh phải từ {QL_DatVeMayBay.Services.FlightPricingHelper.DomesticMinBaseFare:N0}đ đến {QL_DatVeMayBay.Services.FlightPricingHelper.MaxBaseFare:N0}đ.";
                return RedirectToAction("Dashboard");
            }

            try
            {
                DatabaseHelper.ExecuteNonQuery(@"
                    UPDATE ChuyenBay
                    SET TrangThai = @TT,
                        GiaVeCoBan = CASE WHEN @Gia IS NOT NULL AND @Gia > 0 THEN @Gia ELSE GiaVeCoBan END
                    WHERE MaChuyenBay = @MaCB;",
                    CommandType.Text,
                    new SqlParameter("@TT", trangThai),
                    new SqlParameter("@Gia", giaMoi.HasValue ? giaMoi.Value : DBNull.Value),
                    new SqlParameter("@MaCB", maChuyenBay));
                TempData["Success"] = $"Đã cập nhật chuyến bay {maChuyenBay} -> {trangThai}.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi cập nhật chuyến bay: " + ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddPromo(string maKM, string tenKM, decimal phanTramGiam, decimal giamToiDa, decimal donHangToiThieu, int soLuong, DateTime ngayKetThuc)
        {
            if (!IsStaffLoggedIn() || CurrentRoleId() != 1)
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc mới có quyền tạo khuyến mãi!";
                return RedirectToAction("Dashboard");
            }

            if (phanTramGiam <= 0 || phanTramGiam > 50 || giamToiDa < 50000 || donHangToiThieu < 500000 || soLuong <= 0)
            {
                TempData["Error"] = "Thông số Voucher chưa hợp lý (% giảm 1–50%, giảm tối đa >= 50.000đ, đơn tối thiểu >= 500.000đ).";
                return RedirectToAction("Dashboard");
            }

            try
            {
                DatabaseHelper.ExecuteNonQuery(@"
                    INSERT INTO KhuyenMai (MaKM, TenKM, PhanTramGiam, GiamToiDa, DonHangToiThieu, SoLuong, NgayBatDau, NgayKetThuc, TrangThai)
                    VALUES (@MaKM, @TenKM, @PT, @Max, @Min, @SL, CAST(GETDATE() AS date), @End, 1)",
                    CommandType.Text,
                    new SqlParameter("@MaKM", maKM.Trim().ToUpperInvariant()),
                    new SqlParameter("@TenKM", tenKM.Trim()),
                    new SqlParameter("@PT", phanTramGiam),
                    new SqlParameter("@Max", giamToiDa),
                    new SqlParameter("@Min", donHangToiThieu),
                    new SqlParameter("@SL", soLuong),
                    new SqlParameter("@End", ngayKetThuc.Date));

                TempData["Success"] = $"Đã phát hành mã khuyến mãi {maKM.ToUpperInvariant()}!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Lỗi tạo khuyến mãi: " + ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TogglePromo(string maKM)
        {
            if (!IsStaffLoggedIn() || CurrentRoleId() != 1)
            {
                TempData["Error"] = "Không có quyền thao tác khuyến mãi!";
                return RedirectToAction("Dashboard");
            }

            DatabaseHelper.ExecuteNonQuery("UPDATE KhuyenMai SET TrangThai = CASE WHEN TrangThai = 1 THEN 0 ELSE 1 END WHERE MaKM = @MaKM",
                CommandType.Text, new SqlParameter("@MaKM", maKM));
            TempData["Success"] = $"Đã cập nhật trạng thái mã khuyến mãi {maKM}!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStaff(string maNV)
        {
            if (!IsStaffLoggedIn() || CurrentRoleId() != 1 || maNV == "NV001")
            {
                TempData["Error"] = "Thao tác không hợp lệ hoặc thiếu quyền Ban Giám Đốc!";
                return RedirectToAction("Dashboard");
            }

            DatabaseHelper.ExecuteNonQuery("UPDATE NhanVien SET TrangThai = CASE WHEN TrangThai = 1 THEN 0 ELSE 1 END WHERE MaNV = @MaNV",
                CommandType.Text, new SqlParameter("@MaNV", maNV));
            TempData["Success"] = $"Đã cập nhật trạng thái tài khoản nhân viên {maNV}!";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RunCleanupCursor()
        {
            if (!IsStaffLoggedIn())
                return RedirectToAction("Login", "Account");

            try
            {
                var dt = DatabaseHelper.ExecuteQuery("sp_TuDongHuyDonGiuChoHetHan_Cursor", CommandType.StoredProcedure);
                TempData["Success"] = $"Đã quét giải phóng {dt.Rows.Count} ghế từ các đơn đặt chỗ quá hạn thanh toán 15 phút!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Dashboard");
        }

        // BIỂU MẪU 6 (HÌNH 1.7): BÁO CÁO DOANH THU THEO HÃNG VÀ TUYẾN BAY
        [HttpGet]
        public IActionResult RevenueReport(int? thang, int? nam)
        {
            if (!IsStaffLoggedIn())
                return RedirectToAction("Login", "Account");

            DataTable dtHang;
            DataTable dtTuyen;
            if (thang.HasValue && nam.HasValue)
            {
                dtHang = DatabaseHelper.ExecuteQuery(@"
                    SELECT hhk.MaHang, hhk.TenHang,
                           COUNT(v.MaVe) AS TongSoVeDaBan,
                           ISNULL(SUM(v.GiaVe), 0) AS TongDoanhThuVe
                    FROM HangHangKhong hhk
                    LEFT JOIN MayBay mb ON hhk.MaHang = mb.MaHang
                    LEFT JOIN ChuyenBay cb ON mb.MaMayBay = cb.MaMayBay
                    LEFT JOIN VeMayBay v ON cb.MaChuyenBay = v.MaChuyenBay
                        AND v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
                        AND EXISTS (
                            SELECT 1 FROM HoaDon hd
                            WHERE hd.MaDatCho = v.MaDatCho AND hd.TrangThai = N'Đã thanh toán'
                              AND MONTH(hd.NgayThanhToan) = @Thang AND YEAR(hd.NgayThanhToan) = @Nam
                        )
                    GROUP BY hhk.MaHang, hhk.TenHang
                    ORDER BY TongDoanhThuVe DESC;",
                    CommandType.Text,
                    new SqlParameter("@Thang", thang.Value),
                    new SqlParameter("@Nam", nam.Value));

                dtTuyen = DatabaseHelper.ExecuteQuery(@"
                    SELECT TOP 10 tb.MaTuyenBay,
                           sbDi.ThanhPho + N' → ' + sbDen.ThanhPho AS ChangBay,
                           COUNT(v.MaVe) AS SoLuongVeBan,
                           ISNULL(SUM(v.GiaVe), 0) AS TongDoanhThu
                    FROM TuyenBay tb
                    JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
                    JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
                    JOIN ChuyenBay cb ON tb.MaTuyenBay = cb.MaTuyenBay
                    JOIN VeMayBay v ON cb.MaChuyenBay = v.MaChuyenBay
                    JOIN HoaDon hd ON v.MaDatCho = hd.MaDatCho
                    WHERE v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
                      AND hd.TrangThai = N'Đã thanh toán'
                      AND MONTH(hd.NgayThanhToan) = @Thang AND YEAR(hd.NgayThanhToan) = @Nam
                    GROUP BY tb.MaTuyenBay, sbDi.ThanhPho, sbDen.ThanhPho
                    ORDER BY TongDoanhThu DESC;",
                    CommandType.Text,
                    new SqlParameter("@Thang", thang.Value),
                    new SqlParameter("@Nam", nam.Value));
            }
            else
            {
                dtHang = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_BaoCaoDoanhThuTheoHang ORDER BY TongDoanhThuVe DESC");
                dtTuyen = DatabaseHelper.ExecuteQuery("SELECT * FROM vw_TopTuyenBayDatNhieuNhat");
            }

            ViewBag.Thang = thang;
            ViewBag.Nam = nam;
            ViewBag.DoanhThuTheoHang = dtHang;
            ViewBag.TopTuyenBay = dtTuyen;
            ViewBag.NguoiLap = HttpContext.Session.GetString("UserName") ?? "Quản trị viên";
            ViewBag.VaiTro = HttpContext.Session.GetString("RoleName") ?? "Quản trị hệ thống";
            return View("RevenueReport");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(string maGiaoDich, bool chapNhan, string? lyDoTuChoi)
        {
            if (!IsStaffLoggedIn() || (CurrentRoleId() != 1 && CurrentRoleId() != 2))
            {
                TempData["Error"] = "Chỉ Ban Giám Đốc hoặc Nhân viên Thu ngân mới có quyền duyệt thanh toán chuyển khoản!";
                return RedirectToAction("Dashboard");
            }

            string maNV = HttpContext.Session.GetString("UserId") ?? "NV001";
            string newHd = "HD" + DateTime.Now.ToString("yyMMddHHmm") + Random.Shared.Next(100, 999);
            if (newHd.Length > 15) newHd = newHd[..15];

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                string maDatCho = "";
                using (var cmd = new SqlCommand("sp_DuyetThanhToanChuyenKhoan", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MaGiaoDich", maGiaoDich);
                    cmd.Parameters.AddWithValue("@MaNV", maNV);
                    cmd.Parameters.AddWithValue("@ChapNhan", chapNhan ? 1 : 0);
                    cmd.Parameters.AddWithValue("@LyDoTuChoi", string.IsNullOrWhiteSpace(lyDoTuChoi) ? DBNull.Value : lyDoTuChoi.Trim());
                    cmd.Parameters.AddWithValue("@MaHDMoi", newHd);
                    using var rd = await cmd.ExecuteReaderAsync();
                    do
                    {
                        if (rd.FieldCount >= 4 && await rd.ReadAsync())
                        {
                            maDatCho = rd.GetString(0);
                            break;
                        }
                    } while (await rd.NextResultAsync());
                }

                if (!string.IsNullOrEmpty(maDatCho))
                {
                    if (chapNhan)
                    {
                        var legsSeats = new List<(string MaCB, int MaGhe)>();
                        using (var qSeats = new SqlCommand("SELECT MaChuyenBay, MaGhe FROM VeMayBay WHERE MaDatCho = @Ma AND MaGhe IS NOT NULL", conn))
                        {
                            qSeats.Parameters.AddWithValue("@Ma", maDatCho);
                            using var rd = await qSeats.ExecuteReaderAsync();
                            while (await rd.ReadAsync())
                                legsSeats.Add((rd.GetString(0), rd.GetInt32(1)));
                        }
                        foreach (var g in legsSeats.GroupBy(x => x.MaCB))
                        {
                            await _hubContext.Clients.Group($"FLIGHT_{g.Key}").SendAsync("SeatStatusChanged", new
                            {
                                maChuyenBay = g.Key,
                                seatIds = g.Select(x => x.MaGhe).ToList(),
                                isOccupied = true,
                                seatState = "sold",
                                message = $"Vé PNR {maDatCho} đã được Admin duyệt thanh toán."
                            });
                        }

                        await _hubContext.Clients.Group($"PAYMENT_{maDatCho}").SendAsync("PaymentStatusUpdated", new
                        {
                            maDatCho,
                            status = "Thành công",
                            message = "Admin đã duyệt ảnh chuyển khoản và kích hoạt vé điện tử!"
                        });
                        TempData["Success"] = $"Đã duyệt thanh toán {maGiaoDich} (PNR {maDatCho}) và xuất vé / hóa đơn thành công!";
                    }
                    else
                    {
                        await _hubContext.Clients.Group($"PAYMENT_{maDatCho}").SendAsync("PaymentStatusUpdated", new
                        {
                            maDatCho,
                            status = "Từ chối",
                            message = string.IsNullOrWhiteSpace(lyDoTuChoi)
                                ? "Ảnh xác nhận chuyển khoản chưa hợp lệ, vui lòng gửi lại!"
                                : $"Minh chứng bị từ chối: {lyDoTuChoi.Trim()}"
                        });
                        TempData["Success"] = $"Đã từ chối minh chứng {maGiaoDich} và gửi yêu cầu khách tải lại ảnh.";
                    }
                }
            }
            catch (SqlException ex)
            {
                TempData["Error"] = "Lỗi xử lý duyệt chuyển khoản: " + ex.Message;
            }

            return RedirectToAction("Dashboard");
        }
    }
}
