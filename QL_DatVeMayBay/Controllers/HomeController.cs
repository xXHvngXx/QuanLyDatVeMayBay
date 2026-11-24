using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Services;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class HomeController : Controller
    {
        private readonly string _connStr;

        public HomeController(IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
        }

        public async Task<IActionResult> Index(
            string? maSanBayDi,
            string? maSanBayDen,
            DateTime? ngayDi,
            DateTime? ngayVe,
            string loaiHanhTrinh = "MOT_CHIEU",
            string cheDoBay = "ALL",
            string? maHang = null,
            string? khungGio = null,
            string sortBy = "GIO_ASC",
            int nguoiLon = 1,
            int treEm = 0,
            int emBe = 0,
            string? maKM = null)
        {
            if (loaiHanhTrinh != "KHU_HOI") loaiHanhTrinh = "MOT_CHIEU";

            nguoiLon = Math.Clamp(nguoiLon, 1, 9);
            treEm = Math.Clamp(treEm, 0, Math.Max(0, 9 - nguoiLon));
            emBe = Math.Clamp(emBe, 0, Math.Min(nguoiLon, Math.Max(0, 9 - nguoiLon - treEm)));
            int soGheCanDat = nguoiLon + treEm;
            int tongHanhKhach = nguoiLon + treEm + emBe;

            DateTime defaultDepart = ngayDi?.Date ?? DateTime.Today.AddDays(3);
            DateTime defaultReturn = ngayVe?.Date ?? defaultDepart.AddDays(3);
            if (loaiHanhTrinh == "KHU_HOI" && defaultReturn < defaultDepart)
            {
                ViewBag.SearchWarning = "Ngày về không được trước ngày đi — hệ thống đã tự điều chỉnh về cùng ngày đi.";
                defaultReturn = defaultDepart;
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var sanBays = new List<Dictionary<string, object>>();
            using (var cmdSB = new SqlCommand("SELECT MaSanBay, TenSanBay, ThanhPho, QuocGia, KhuVuc, HinhAnhUrl, MoTaDiemDen FROM SanBay WHERE TrangThai = 1 ORDER BY CASE WHEN QuocGia = N'Việt Nam' THEN 0 ELSE 1 END, ThanhPho", conn))
            using (var rd = await cmdSB.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    sanBays.Add(new Dictionary<string, object>
                    {
                        ["MaSanBay"] = rd.GetString(0),
                        ["TenSanBay"] = rd.GetString(1),
                        ["ThanhPho"] = rd.GetString(2),
                        ["QuocGia"] = rd.GetString(3),
                        ["KhuVuc"] = rd.GetString(4),
                        ["HinhAnhUrl"] = rd.IsDBNull(5) ? $"/images/destinations/{rd.GetString(0)}.jpg" : rd.GetString(5),
                        ["MoTaDiemDen"] = rd.IsDBNull(6) ? "" : rd.GetString(6)
                    });
                }
            }

            var hangs = new List<Dictionary<string, object>>();
            using (var cmdH = new SqlCommand("SELECT MaHang, TenHang, Logo FROM HangHangKhong WHERE TrangThai = 1 ORDER BY MaHang", conn))
            using (var rd = await cmdH.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    hangs.Add(new Dictionary<string, object>
                    {
                        ["MaHang"] = rd.GetString(0),
                        ["TenHang"] = rd.GetString(1),
                        ["Logo"] = rd.IsDBNull(2) ? "" : rd.GetString(2)
                    });
                }
            }

            var hangGhes = new List<Dictionary<string, object>>();
            using (var cmdHG = new SqlCommand("SELECT MaHangGhe, TenHangGhe, HeSoGia, MoTa FROM HangGhe ORDER BY HeSoGia ASC", conn))
            using (var rd = await cmdHG.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    hangGhes.Add(new Dictionary<string, object>
                    {
                        ["MaHangGhe"] = rd.GetString(0),
                        ["TenHangGhe"] = rd.GetString(1),
                        ["HeSoGia"] = rd.GetDecimal(2),
                        ["MoTa"] = rd.IsDBNull(3) ? "" : rd.GetString(3)
                    });
                }
            }

            bool daLoc = !string.IsNullOrEmpty(maSanBayDi) || !string.IsNullOrEmpty(maSanBayDen) || ngayDi.HasValue || cheDoBay != "ALL" || !string.IsNullOrEmpty(maHang) || !string.IsNullOrEmpty(khungGio);
            if (!string.IsNullOrEmpty(maSanBayDi) && maSanBayDi == maSanBayDen)
                ViewBag.SearchWarning = "Sân bay đi và sân bay đến không được trùng nhau.";

            string di = string.IsNullOrWhiteSpace(maSanBayDi) ? "SGN" : maSanBayDi.Trim().ToUpperInvariant();
            string den = string.IsNullOrWhiteSpace(maSanBayDen) ? "DLI" : maSanBayDen.Trim().ToUpperInvariant();

            ViewBag.SanBays = sanBays;
            ViewBag.Hangs = hangs;
            ViewBag.HangGhes = hangGhes;
            ViewBag.DaLoc = daLoc;
            ViewBag.LoaiHanhTrinh = loaiHanhTrinh;
            ViewBag.CheDoBay = cheDoBay;
            ViewBag.MaHang = maHang ?? "";
            ViewBag.KhungGio = khungGio ?? "";
            ViewBag.SortBy = sortBy;
            ViewBag.NguoiLon = nguoiLon;
            ViewBag.TreEm = treEm;
            ViewBag.EmBe = emBe;
            ViewBag.SoGheCanDat = soGheCanDat;
            ViewBag.TongHanhKhach = tongHanhKhach;
            ViewBag.MaSanBayDi = di;
            ViewBag.MaSanBayDen = den;
            ViewBag.MaKM = maKM?.Trim().ToUpperInvariant() ?? "";
            ViewBag.NgayDi = defaultDepart.ToString("yyyy-MM-dd");
            ViewBag.NgayVe = defaultReturn.ToString("yyyy-MM-dd");
            ViewBag.OriginInfo = sanBays.FirstOrDefault(x => x["MaSanBay"].ToString() == di);
            ViewBag.DestinationInfo = sanBays.FirstOrDefault(x => x["MaSanBay"].ToString() == den);

            if (daLoc)
            {
                var directOut = await QueryDirectFlightsAsync(conn, di, den, defaultDepart, cheDoBay, maHang, khungGio, sortBy, soGheCanDat);
                var transitOut = cheDoBay != "THANG"
                    ? await QueryTransitFlightsAsync(conn, di, den, defaultDepart, soGheCanDat, maHang, khungGio, sortBy)
                    : new List<Dictionary<string, object>>();

                ViewBag.LichChuyenBay = directOut;
                ViewBag.ChuyenQuaCanhDi = transitOut;

                if (loaiHanhTrinh == "KHU_HOI")
                {
                    ViewBag.LichChuyenBayVe = await QueryDirectFlightsAsync(conn, den, di, defaultReturn, cheDoBay, maHang, khungGio, sortBy, soGheCanDat);
                    ViewBag.ChuyenQuaCanhVe = cheDoBay != "THANG"
                        ? await QueryTransitFlightsAsync(conn, den, di, defaultReturn, soGheCanDat, maHang, khungGio, sortBy)
                        : new List<Dictionary<string, object>>();
                }
            }
            else
            {
                ViewBag.LichChuyenBay = await QueryFeaturedFlightsAsync(conn);
                ViewBag.ChuyenQuaCanhDi = new List<Dictionary<string, object>>();
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GiaTheoThang(string di = "SGN", string den = "HAN", int? thang = null, int? nam = null, int soNguoi = 1)
        {
            DateTime target = new DateTime(nam ?? DateTime.Today.Year, thang ?? DateTime.Today.Month, 1);
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            using var cmd = new SqlCommand("sp_GiaThapNhatTheoNgay", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@MaSanBayDi", di.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@MaSanBayDen", den.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@Thang", target.Month);
            cmd.Parameters.AddWithValue("@Nam", target.Year);
            cmd.Parameters.AddWithValue("@SoNguoi", Math.Clamp(soNguoi, 1, 9));

            var days = new List<object>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                decimal? giaMin = rd.IsDBNull(3) ? null : rd.GetDecimal(3);
                days.Add(new
                {
                    ngay = rd.GetDateTime(0).ToString("yyyy-MM-dd"),
                    ngayTrongThang = rd.GetInt32(1),
                    soChuyenBay = rd.GetInt32(2),
                    giaThapNhat = giaMin,
                    giaRutGon = giaMin.HasValue ? VietnameseCurrencyHelper.FormatShortPrice(giaMin.Value) : null,
                    laNgayReNhat = rd.GetInt32(4) == 1
                });
            }

            return Json(new
            {
                thang = target.Month,
                nam = target.Year,
                tenThang = $"Tháng {target.Month:00}/{target.Year}",
                di = di.ToUpperInvariant(),
                den = den.ToUpperInvariant(),
                days
            });
        }

        private async Task<List<Dictionary<string, object>>> QueryFeaturedFlightsAsync(SqlConnection conn)
        {
            const string sql = @"
                SELECT TOP 12 v.*,
                       dbo.fn_TinhGiaVeThucTe(v.GiaVeCoBan, 'ECO') AS GiaECO,
                       dbo.fn_TinhGiaVeThucTe(v.GiaVeCoBan, 'BUS') AS GiaBUS,
                       dbo.fn_TinhGiaVeThucTe(v.GiaVeCoBan, 'FIR') AS GiaFIR,
                       dbo.fn_DemSoGheTrongTheoHang(v.MaChuyenBay, 'ECO') AS GheECO,
                       dbo.fn_DemSoGheTrongTheoHang(v.MaChuyenBay, 'BUS') AS GheBUS
                FROM vw_DanhSachChuyenBayChiTiet v
                WHERE v.TrangThaiChuyenBay = N'Đang mở bán'
                  AND v.SoGheTrong > 0
                  AND v.NgayGioBay > DATEADD(MINUTE, 60, GETDATE())
                ORDER BY CASE WHEN v.MaSanBayDen IN ('DLI', 'PQC', 'DAD', 'HAN', 'CXR') THEN 0 ELSE 1 END,
                         CASE WHEN v.NgayGioBay >= DATEADD(DAY, 1, GETDATE()) THEN 0 ELSE 1 END,
                         v.GiaVeCoBan ASC, v.NgayGioBay ASC;";
            using var cmd = new SqlCommand(sql, conn);
            return await ReadDictListAsync(cmd);
        }

        private async Task<List<Dictionary<string, object>>> QueryDirectFlightsAsync(
            SqlConnection conn, string di, string den, DateTime ngay, string cheDoBay, string? maHang, string? khungGio, string sortBy, int soGheCan)
        {
            string orderClause = sortBy switch
            {
                "GIA_ASC" => "v.GiaVeCoBan ASC, v.NgayGioBay ASC",
                "THOI_GIAN_ASC" => "v.ThoiGianBay ASC, v.GiaVeCoBan ASC",
                _ => "v.NgayGioBay ASC, v.GiaVeCoBan ASC"
            };

            string sql = $@"
                SELECT v.*,
                       dbo.fn_TinhGiaVeThucTe(v.GiaVeCoBan, 'ECO') AS GiaECO,
                       dbo.fn_TinhGiaVeThucTe(v.GiaVeCoBan, 'BUS') AS GiaBUS,
                       dbo.fn_TinhGiaVeThucTe(v.GiaVeCoBan, 'FIR') AS GiaFIR,
                       dbo.fn_DemSoGheTrongTheoHang(v.MaChuyenBay, 'ECO') AS GheECO,
                       dbo.fn_DemSoGheTrongTheoHang(v.MaChuyenBay, 'BUS') AS GheBUS
                FROM vw_DanhSachChuyenBayChiTiet v
                WHERE v.TrangThaiChuyenBay = N'Đang mở bán'
                  AND v.SoGheTrong >= @SoGheCan
                  AND v.NgayGioBay > DATEADD(MINUTE, 60, GETDATE())
                  AND v.MaSanBayDi = @Di
                  AND v.MaSanBayDen = @Den
                  AND CAST(v.NgayGioBay AS date) = @Ngay
                  AND (@MaHang IS NULL OR @MaHang = '' OR v.MaHang = @MaHang)
                  AND (@CheDo = 'ALL' OR (@CheDo = 'THANG' AND v.LaChuyenQuaCanh = 0) OR (@CheDo = 'QUA_CANH' AND v.LaChuyenQuaCanh = 1))
                  AND (
                      @KhungGio IS NULL OR @KhungGio = ''
                      OR (@KhungGio = 'SANG'  AND DATEPART(HOUR, v.NgayGioBay) BETWEEN 0 AND 11)
                      OR (@KhungGio = 'CHIEU' AND DATEPART(HOUR, v.NgayGioBay) BETWEEN 12 AND 17)
                      OR (@KhungGio = 'TOI'   AND DATEPART(HOUR, v.NgayGioBay) BETWEEN 18 AND 23)
                  )
                ORDER BY {orderClause};";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Di", di);
            cmd.Parameters.AddWithValue("@Den", den);
            cmd.Parameters.AddWithValue("@Ngay", ngay.Date);
            cmd.Parameters.AddWithValue("@SoGheCan", Math.Max(1, soGheCan));
            cmd.Parameters.AddWithValue("@MaHang", string.IsNullOrWhiteSpace(maHang) ? DBNull.Value : maHang.Trim().ToUpperInvariant());
            cmd.Parameters.AddWithValue("@CheDo", cheDoBay);
            cmd.Parameters.AddWithValue("@KhungGio", string.IsNullOrWhiteSpace(khungGio) ? DBNull.Value : khungGio.Trim().ToUpperInvariant());
            return await ReadDictListAsync(cmd);
        }

        private async Task<List<Dictionary<string, object>>> QueryTransitFlightsAsync(
            SqlConnection conn, string di, string den, DateTime ngay, int soGheCan, string? maHang, string? khungGio, string sortBy)
        {
            using var cmd = new SqlCommand("sp_TimChuyenQuaCanh", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@MaSanBayDi", di);
            cmd.Parameters.AddWithValue("@MaSanBayDen", den);
            cmd.Parameters.AddWithValue("@NgayBay", ngay.Date);
            cmd.Parameters.AddWithValue("@SoHanhKhach", Math.Max(1, soGheCan));
            cmd.Parameters.AddWithValue("@MaHang", string.IsNullOrWhiteSpace(maHang) ? DBNull.Value : maHang.Trim().ToUpperInvariant());

            var rows = await ReadDictListAsync(cmd);

            if (!string.IsNullOrWhiteSpace(khungGio))
            {
                rows = rows.Where(r =>
                {
                    int hr = Convert.ToDateTime(r["Chuyen1_GioDi"]).Hour;
                    return khungGio switch
                    {
                        "SANG" => hr <= 11,
                        "CHIEU" => hr >= 12 && hr <= 17,
                        "TOI" => hr >= 18,
                        _ => true
                    };
                }).ToList();
            }

            return sortBy switch
            {
                "GIA_ASC" => rows.OrderBy(r => Convert.ToDecimal(r["TongGiaVeCoBan"])).ToList(),
                "THOI_GIAN_ASC" => rows.OrderBy(r => Convert.ToInt32(r["TongThoiGianHanhTrinhPhut"])).ToList(),
                _ => rows.OrderBy(r => Convert.ToDateTime(r["Chuyen1_GioDi"])).ToList()
            };
        }

        private static async Task<List<Dictionary<string, object>>> ReadDictListAsync(SqlCommand cmd)
        {
            var list = new List<Dictionary<string, object>>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < rd.FieldCount; i++)
                    row[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
                list.Add(row);
            }
            return list;
        }

        public IActionResult Privacy() => View();
    }
}
