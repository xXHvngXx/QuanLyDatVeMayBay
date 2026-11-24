using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using QL_DatVeMayBay.Services;
using System.Data;
using System.Text.RegularExpressions;

namespace QL_DatVeMayBay.Controllers
{
    public class AccountController : Controller
    {
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string loginType, string taiKhoan, string matKhau, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (string.IsNullOrWhiteSpace(taiKhoan) || string.IsNullOrWhiteSpace(matKhau))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ tài khoản và mật khẩu!";
                return View();
            }

            string accountKey = taiKhoan.Trim();

            if (loginType == "NV")
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(@"
                    SELECT nv.MaNV, nv.TenNV, nv.TaiKhoan, nv.MatKhau, nv.RoleID, vt.TenRole
                    FROM NhanVien nv
                    JOIN VaiTro vt ON nv.RoleID = vt.RoleID
                    WHERE nv.TaiKhoan = @TaiKhoan AND nv.TrangThai = 1",
                    CommandType.Text,
                    new SqlParameter("@TaiKhoan", accountKey));

                if (dt.Rows.Count > 0 && PasswordHasherService.VerifyPassword(matKhau, dt.Rows[0]["MatKhau"].ToString()!))
                {
                    DataRow r = dt.Rows[0];
                    HttpContext.Session.SetString("UserType", "NV");
                    HttpContext.Session.SetString("UserId", r["MaNV"].ToString()!);
                    HttpContext.Session.SetString("UserName", $"{r["TenNV"]} ({r["TenRole"]})");
                    HttpContext.Session.SetInt32("RoleID", Convert.ToInt32(r["RoleID"]));
                    HttpContext.Session.SetString("RoleName", r["TenRole"].ToString()!);
                    return RedirectToAction("Dashboard", "Admin");
                }
            }
            else
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_DangNhapKhachHang",
                    CommandType.StoredProcedure,
                    new SqlParameter("@TaiKhoan", accountKey));

                if (dt.Rows.Count > 0 && PasswordHasherService.VerifyPassword(matKhau, dt.Rows[0]["MatKhau"].ToString()!))
                {
                    DataRow r = dt.Rows[0];
                    string maKH = r["MaKH"].ToString()!;
                    HttpContext.Session.SetString("UserType", "KH");
                    HttpContext.Session.SetString("UserId", maKH);
                    HttpContext.Session.SetString("MaKH", maKH);
                    HttpContext.Session.SetString("UserName", r["TenKH"].ToString()!);
                    HttpContext.Session.SetString("UserEmail", r["Email"]?.ToString() ?? "");
                    HttpContext.Session.SetString("UserSDT", r["SDT"]?.ToString() ?? "");
                    HttpContext.Session.SetString("UserCCCD", r["CCCD"]?.ToString() ?? r["HoChieu"]?.ToString() ?? "");

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Index", "Home");
                }
            }

            ViewBag.Error = "Tài khoản hoặc mật khẩu không chính xác (hoặc tài khoản đã bị khóa)!";
            return View();
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(
            string tenKH,
            string gioiTinh,
            DateTime ngaySinh,
            string sdt,
            string email,
            string matKhau,
            string? diaChi,
            string loaiKhachHang,
            string? loaiGiayTo = null,
            string? soGiayTo = null,
            string? cccd = null,
            string? hoChieu = null,
            DateTime? hanHoChieu = null,
            string? quocTich = null)
        {
            if (string.IsNullOrWhiteSpace(tenKH) || string.IsNullOrWhiteSpace(sdt) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(matKhau) || matKhau.Length < 6)
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin (mật khẩu tối thiểu 6 ký tự).";
                return View();
            }

            // Hỗ trợ nhập 1 trong các giấy tờ: CCCD / Hộ chiếu / Giấy khai sinh (trẻ em hoặc khách quốc tế)
            string docNumber = !string.IsNullOrWhiteSpace(soGiayTo)
                ? soGiayTo.Trim()
                : (!string.IsNullOrWhiteSpace(cccd) ? cccd.Trim() : (hoChieu?.Trim() ?? ""));
            string docType = !string.IsNullOrWhiteSpace(loaiGiayTo)
                ? loaiGiayTo.Trim()
                : (string.IsNullOrWhiteSpace(cccd) && !string.IsNullOrWhiteSpace(hoChieu)
                    ? "Hộ chiếu"
                    : (loaiKhachHang == "Nước ngoài" ? "Hộ chiếu" : "CCCD"));

            if (string.IsNullOrWhiteSpace(docNumber))
            {
                ViewBag.Error = "Vui lòng nhập Số CCCD, Hộ chiếu hoặc Giấy khai sinh để đăng ký tài khoản.";
                return View();
            }

            string? finalCccd = null;
            string? finalHoChieu = null;
            if (docType == "Hộ chiếu")
            {
                string pp = docNumber.ToUpperInvariant();
                if (!Regex.IsMatch(pp, @"^[A-Z0-9]{6,12}$"))
                {
                    ViewBag.Error = "Số Hộ chiếu phải gồm 6-12 ký tự chữ/số hợp lệ.";
                    return View();
                }
                finalHoChieu = pp;
            }
            else if (docType == "Giấy khai sinh")
            {
                string ks = docNumber.ToUpperInvariant();
                if (!Regex.IsMatch(ks, @"^[A-Z0-9\-/]{4,20}$"))
                {
                    ViewBag.Error = "Số Giấy khai sinh phải từ 4-20 ký tự chữ/số.";
                    return View();
                }
                finalCccd = ks;
            }
            else
            {
                if (!Regex.IsMatch(docNumber, @"^\d{12}$"))
                {
                    ViewBag.Error = "Số CCCD phải gồm đúng 12 chữ số (hoặc đổi Loại giấy tờ sang Hộ chiếu / Giấy khai sinh).";
                    return View();
                }
                finalCccd = docNumber;
            }

            try
            {
                string hashedPwd = PasswordHasherService.HashPassword(matKhau);
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "sp_DangKyKhachHang",
                    CommandType.StoredProcedure,
                    new SqlParameter("@TenKH", tenKH.Trim()),
                    new SqlParameter("@GioiTinh", gioiTinh),
                    new SqlParameter("@NgaySinh", ngaySinh.Date),
                    new SqlParameter("@SDT", sdt.Trim()),
                    new SqlParameter("@Email", email.Trim().ToLowerInvariant()),
                    new SqlParameter("@MatKhau", hashedPwd),
                    new SqlParameter("@DiaChi", string.IsNullOrWhiteSpace(diaChi) ? DBNull.Value : diaChi.Trim()),
                    new SqlParameter("@LoaiKhachHang", loaiKhachHang),
                    new SqlParameter("@CCCD", (object?)finalCccd ?? DBNull.Value),
                    new SqlParameter("@HoChieu", (object?)finalHoChieu ?? DBNull.Value),
                    new SqlParameter("@HanHoChieu", hanHoChieu.HasValue ? hanHoChieu.Value.Date : DBNull.Value),
                    new SqlParameter("@QuocTich", string.IsNullOrWhiteSpace(quocTich) ? "Việt Nam" : quocTich.Trim())
                );

                TempData["Success"] = $"Đăng ký thành công! Mã khách hàng của bạn là {dt.Rows[0]["MaKH"]}. Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            catch (SqlException ex)
            {
                ViewBag.Error = ex.Message;
                return View();
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }
    }
}
