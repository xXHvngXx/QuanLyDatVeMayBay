using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Hubs;
using QL_DatVeMayBay.Services;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class PaymentController : Controller
    {
        private readonly string _connStr;
        private readonly IPaymentGateway _gateway;
        private readonly IHubContext<SeatHub> _hubContext;
        private readonly IWebHostEnvironment _env;

        public PaymentController(
            IConfiguration config,
            IPaymentGateway gateway,
            IHubContext<SeatHub> hubContext,
            IWebHostEnvironment env)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
            _gateway = gateway;
            _hubContext = hubContext;
            _env = env;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadProof(
            string maGiaoDich,
            string maDatCho,
            string loaiGiaoDich,
            decimal soTien,
            string signature,
            IFormFile? proofImage,
            string? ghiChu)
        {
            if (!_gateway.VerifySignature(maGiaoDich, maDatCho, soTien, loaiGiaoDich, signature))
            {
                TempData["Error"] = "Chữ ký xác thực giao dịch (HMAC) không hợp lệ!";
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }

            if (proofImage == null || proofImage.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn ảnh chụp màn hình / biên lai xác nhận chuyển khoản!";
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }

            if (proofImage.Length > 5 * 1024 * 1024)
            {
                TempData["Error"] = "Dung lượng ảnh xác nhận tối đa 5MB.";
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }

            string ext = Path.GetExtension(proofImage.FileName).ToLowerInvariant();
            if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            {
                TempData["Error"] = "Chỉ chấp nhận file ảnh JPG, PNG hoặc WEBP.";
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }

            // Kiểm tra magic bytes tránh upload nhầm file giả mạo
            using var ms = new MemoryStream();
            await proofImage.CopyToAsync(ms);
            byte[] header = ms.ToArray();
            bool validImageBytes =
                (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) || // JPEG
                (header.Length >= 4 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) || // PNG
                (header.Length >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
                    && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P'); // WEBP
            if (!validImageBytes)
            {
                TempData["Error"] = "Tệp tải lên không phải ảnh hợp lệ (JPG/PNG/WEBP).";
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }

            string folder = Path.Combine(_env.WebRootPath, "uploads", "payment-proofs");
            Directory.CreateDirectory(folder);
            string safePrefix = string.Concat(maDatCho.Where(char.IsLetterOrDigit));
            string fileName = $"{safePrefix}_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
            string fullPath = Path.Combine(folder, fileName);
            await System.IO.File.WriteAllBytesAsync(fullPath, header);
            string relativeUrl = $"/uploads/payment-proofs/{fileName}";

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                using var cmd = new SqlCommand("sp_GuiMinhChungChuyenKhoan", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaGiaoDich", maGiaoDich);
                cmd.Parameters.AddWithValue("@AnhUrl", relativeUrl);
                cmd.Parameters.AddWithValue("@GhiChu", string.IsNullOrWhiteSpace(ghiChu) ? DBNull.Value : ghiChu.Trim());
                await cmd.ExecuteNonQueryAsync();

                await _hubContext.Clients.Group($"PAYMENT_{maDatCho}").SendAsync("PaymentStatusUpdated", new
                {
                    maDatCho,
                    status = "Chờ duyệt",
                    message = "Đã nhận ảnh xác nhận chuyển khoản, đang chờ Admin / Thu ngân duyệt!"
                });

                TempData["Success"] = $"Đã gửi ảnh xác nhận chuyển khoản cho mã PNR {maDatCho}! Ghế của bạn đang được khóa an toàn để chờ Admin / Thu ngân kiểm tra và xuất vé.";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Callback(
            string maGiaoDich,
            string maDatCho,
            string loaiGiaoDich,
            decimal soTien,
            string phuongThuc,
            string ketQua, // SUCCESS | FAILED | CANCELLED
            string signature)
        {
            if (!_gateway.VerifySignature(maGiaoDich, maDatCho, soTien, loaiGiaoDich, signature))
            {
                TempData["Error"] = "Chữ ký xác thực giao dịch (HMAC) không hợp lệ!";
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }

            string normalizedResult = ketQua?.Trim().ToUpperInvariant() switch
            {
                "SUCCESS" => "SUCCESS",
                "FAILED" => "FAILED",
                _ => "CANCELLED"
            };

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            try
            {
                // Cập nhật phương thức đã chọn trước khi xác nhận giao dịch
                using (var upMethod = new SqlCommand("UPDATE GiaoDichThanhToan SET PhuongThuc = @PT WHERE MaGiaoDich = @GD AND TrangThai = N'Chờ thanh toán'", conn))
                {
                    upMethod.Parameters.AddWithValue("@PT", string.IsNullOrWhiteSpace(phuongThuc) ? "VietQR" : phuongThuc);
                    upMethod.Parameters.AddWithValue("@GD", maGiaoDich);
                    await upMethod.ExecuteNonQueryAsync();
                }

                string newHd = "HD" + DateTime.Now.ToString("yyMMddHHmm") + Random.Shared.Next(100, 999);
                if (newHd.Length > 15) newHd = newHd[..15];

                string finalState = "";
                bool replay = false;

                using (var cmd = new SqlCommand("sp_XacNhanThanhToanGiaoDich", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MaGiaoDich", maGiaoDich);
                    cmd.Parameters.AddWithValue("@KetQua", normalizedResult);
                    cmd.Parameters.AddWithValue("@MaHDMoi", newHd);
                    using var rd = await cmd.ExecuteReaderAsync();
                    do
                    {
                        if (rd.FieldCount >= 4 && await rd.ReadAsync())
                        {
                            finalState = rd.GetString(2);
                            replay = rd.GetInt32(3) == 1;
                            break;
                        }
                    } while (await rd.NextResultAsync());
                }

                // Cập nhật SignalR sơ đồ ghế & màn hình thanh toán
                if (normalizedResult == "SUCCESS")
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
                            message = $"Thanh toán thành công cho PNR {maDatCho}."
                        });
                    }

                    await _hubContext.Clients.Group($"PAYMENT_{maDatCho}").SendAsync("PaymentStatusUpdated", new
                    {
                        maDatCho,
                        status = "Thành công",
                        message = "Giao dịch đã được xác nhận thành công!"
                    });

                    TempData["Success"] = replay
                        ? $"Giao dịch {maGiaoDich} đã được xử lý trước đó (Idempotent) cho PNR {maDatCho}."
                        : $"Thanh toán {phuongThuc} thành công! Vé/Dịch vụ cho mã đặt chỗ {maDatCho} đã được kích hoạt.";
                    return RedirectToAction("Lookup", "Booking", new { maDatCho });
                }

                TempData["Error"] = $"Giao dịch {maGiaoDich} kết thúc với trạng thái: {finalState}. Bạn có thể thử thanh toán lại trong thời gian giữ chỗ.";
                return RedirectToAction("Payment", "Booking", new { maDatCho });
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Payment", "Booking", new { maDatCho, maGD = maGiaoDich });
            }
        }
    }
}
