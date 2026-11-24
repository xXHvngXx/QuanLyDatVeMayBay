using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace QL_DatVeMayBay.Services
{
    public record PaymentInitiationResult(
        string MaGiaoDich,
        string MaDatCho,
        string LoaiGiaoDich,
        decimal SoTien,
        string NoiDungCK,
        string QrImageUrl,
        string Signature,
        DateTime HanThanhToan);

    public interface IPaymentGateway
    {
        string ComputeSignature(string maGiaoDich, string maDatCho, decimal soTien, string loaiGiaoDich);
        bool VerifySignature(string maGiaoDich, string maDatCho, decimal soTien, string loaiGiaoDich, string signature);
        Task<PaymentInitiationResult> CreateOrGetTransactionAsync(
            SqlConnection conn,
            string maDatCho,
            string loaiGiaoDich,
            string phuongThuc,
            decimal soTien,
            DateTime hanThanhToan,
            string? maGiaoDichOverride = null);
    }

    public class SandboxPaymentGateway : IPaymentGateway
    {
        private readonly string _secret;
        private readonly string _bankCode;
        private readonly string _bankAccount;
        private readonly string _accountName;

        public SandboxPaymentGateway(IConfiguration config)
        {
            _secret = config["PaymentGateway:HmacSecret"] ?? "SkyVNAir_Sandbox_HMAC_Secret_2026_ChangeInProd";
            _bankCode = config["PaymentGateway:BankCode"] ?? "MB";
            _bankAccount = config["PaymentGateway:BankAccount"] ?? "0901234567";
            _accountName = config["PaymentGateway:AccountName"] ?? "CONG TY HANG KHONG SKYVN AIR";
        }

        public string ComputeSignature(string maGiaoDich, string maDatCho, decimal soTien, string loaiGiaoDich)
        {
            string payload = $"{maGiaoDich}|{maDatCho}|{Math.Round(soTien, 0):0}|{loaiGiaoDich}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public bool VerifySignature(string maGiaoDich, string maDatCho, decimal soTien, string loaiGiaoDich, string signature)
        {
            if (string.IsNullOrWhiteSpace(signature)) return false;
            string expected = ComputeSignature(maGiaoDich, maDatCho, soTien, loaiGiaoDich);
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
        }

        public async Task<PaymentInitiationResult> CreateOrGetTransactionAsync(
            SqlConnection conn,
            string maDatCho,
            string loaiGiaoDich,
            string phuongThuc,
            decimal soTien,
            DateTime hanThanhToan,
            string? maGiaoDichOverride = null)
        {
            // Tái sử dụng giao dịch đang chờ nếu cùng đơn & cùng số tiền
            if (string.IsNullOrEmpty(maGiaoDichOverride))
            {
                const string checkSql = @"
                    SELECT TOP 1 MaGiaoDich, SoTien, NoiDungCK, QrImageUrl, Signature, HanThanhToan
                    FROM GiaoDichThanhToan
                    WHERE MaDatCho = @Ma AND LoaiGiaoDich = @Loai
                      AND (TrangThai = N'Chờ duyệt' OR (TrangThai = N'Chờ thanh toán' AND HanThanhToan > GETDATE()))
                    ORDER BY CASE WHEN TrangThai = N'Chờ duyệt' THEN 0 ELSE 1 END, NgayTao DESC;";
                using var checkCmd = new SqlCommand(checkSql, conn);
                checkCmd.Parameters.AddWithValue("@Ma", maDatCho);
                checkCmd.Parameters.AddWithValue("@Loai", loaiGiaoDich);
                using var rd = await checkCmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    var existingId = rd.GetString(0);
                    var existingAmt = rd.GetDecimal(1);
                    if (Math.Abs(existingAmt - soTien) < 1m)
                    {
                        return new PaymentInitiationResult(
                            existingId,
                            maDatCho,
                            loaiGiaoDich,
                            existingAmt,
                            rd.IsDBNull(2) ? maDatCho : rd.GetString(2),
                            rd.IsDBNull(3) ? BuildQrUrl(existingAmt, maDatCho) : rd.GetString(3),
                            rd.IsDBNull(4) ? ComputeSignature(existingId, maDatCho, existingAmt, loaiGiaoDich) : rd.GetString(4),
                            rd.GetDateTime(5));
                    }
                }
            }

            string maGD = maGiaoDichOverride ?? $"TXN_{maDatCho}_{DateTime.Now:yyyyMMddHHmmss}_{Random.Shared.Next(100, 999)}";
            string noiDung = $"{maDatCho} {loaiGiaoDich}";
            string qrUrl = BuildQrUrl(soTien, noiDung);
            string sig = ComputeSignature(maGD, maDatCho, soTien, loaiGiaoDich);

            const string insertSql = @"
                INSERT INTO GiaoDichThanhToan
                (MaGiaoDich, MaDatCho, LoaiGiaoDich, PhuongThuc, SoTien, NoiDungCK, QrImageUrl, TrangThai, Signature, NgayTao, HanThanhToan)
                VALUES
                (@MaGD, @MaDC, @Loai, @PT, @Tien, @ND, @QR, N'Chờ thanh toán', @Sig, GETDATE(), @Han);";

            using var cmd = new SqlCommand(insertSql, conn);
            cmd.Parameters.AddWithValue("@MaGD", maGD);
            cmd.Parameters.AddWithValue("@MaDC", maDatCho);
            cmd.Parameters.AddWithValue("@Loai", loaiGiaoDich);
            cmd.Parameters.AddWithValue("@PT", string.IsNullOrWhiteSpace(phuongThuc) ? "Chuyển khoản ngân hàng (VietQR)" : phuongThuc);
            cmd.Parameters.AddWithValue("@Tien", soTien);
            cmd.Parameters.AddWithValue("@ND", noiDung);
            cmd.Parameters.AddWithValue("@QR", qrUrl);
            cmd.Parameters.AddWithValue("@Sig", sig);
            cmd.Parameters.AddWithValue("@Han", hanThanhToan);
            await cmd.ExecuteNonQueryAsync();

            return new PaymentInitiationResult(maGD, maDatCho, loaiGiaoDich, soTien, noiDung, qrUrl, sig, hanThanhToan);
        }

        public string BuildQrUrl(decimal amount, string addInfo)
        {
            long amt = (long)Math.Round(amount, 0);
            return $"https://img.vietqr.io/image/{_bankCode}-{_bankAccount}-compact2.png?amount={amt}&addInfo={Uri.EscapeDataString(addInfo)}&accountName={Uri.EscapeDataString(_accountName)}";
        }
    }
}
