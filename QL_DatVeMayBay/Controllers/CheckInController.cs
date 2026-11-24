using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Data;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class CheckInController : Controller
    {
        [HttpGet]
        public IActionResult Index(string? maDatCho, string? tenHanhKhach)
        {
            if (!string.IsNullOrWhiteSpace(maDatCho))
            {
                string query = @"
                    SELECT * FROM vw_ChiTietVeVaHanhKhach
                    WHERE MaDatCho = @PNR
                      AND (@TenHK IS NULL OR UPPER(TenHanhKhach) LIKE N'%' + UPPER(@TenHK) + N'%')
                    ORDER BY HuongBay ASC, ThuTuChang ASC;";

                DataTable dt = DatabaseHelper.ExecuteQuery(
                    query,
                    CommandType.Text,
                    new SqlParameter("@PNR", maDatCho.Trim().ToUpperInvariant()),
                    new SqlParameter("@TenHK", string.IsNullOrWhiteSpace(tenHanhKhach) ? DBNull.Value : tenHanhKhach.Trim())
                );

                if (dt.Rows.Count > 0)
                    ViewBag.DanhSachVe = dt;
                else
                    ViewBag.Error = "Không tìm thấy vé phù hợp với mã PNR và họ tên hành khách đã nhập!";
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmCheckIn(string maVe, string maDatCho, string? tenHanhKhach)
        {
            try
            {
                DataTable dt = DatabaseHelper.ExecuteQuery(
                    "SELECT TrangThaiVe, NgayGioBay FROM vw_ChiTietVeVaHanhKhach WHERE MaVe = @MaVe AND MaDatCho = @PNR",
                    CommandType.Text,
                    new SqlParameter("@MaVe", maVe),
                    new SqlParameter("@PNR", maDatCho.Trim().ToUpperInvariant()));

                if (dt.Rows.Count == 0)
                    throw new Exception("Không tìm thấy thông tin vé hợp lệ!");

                string trangThai = dt.Rows[0]["TrangThaiVe"].ToString()!;
                if (trangThai != "Đã xuất vé")
                    throw new Exception($"Vé đang ở trạng thái '{trangThai}', không thể thực hiện Check-in!");

                DateTime gioBay = Convert.ToDateTime(dt.Rows[0]["NgayGioBay"]);
                double gioConLai = (gioBay - DateTime.Now).TotalHours;
                if (gioConLai < 1.0)
                    throw new Exception("Quầy làm thủ tục trực tuyến đã đóng (trước giờ cất cánh dưới 60 phút). Vui lòng làm thủ tục trực tiếp tại sân bay!");
                if (gioConLai > 24.0)
                    throw new Exception($"Check-in trực tuyến chỉ mở trong vòng 24 giờ đến 1 giờ trước giờ bay (hiện còn {gioConLai:0.0} giờ). Vui lòng quay lại sau!");

                DatabaseHelper.ExecuteNonQuery(
                    "UPDATE VeMayBay SET TrangThai = N'Đã Check-in' WHERE MaVe = @MaVe AND TrangThai = N'Đã xuất vé'",
                    CommandType.Text,
                    new SqlParameter("@MaVe", maVe));

                TempData["Success"] = $"Check-in thành công cho vé {maVe}! Thẻ lên máy bay điện tử (Boarding Pass) đã được khởi tạo.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Index", new { maDatCho, tenHanhKhach });
        }
    }
}
