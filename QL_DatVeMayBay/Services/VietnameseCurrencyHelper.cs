using System.Text;
using System.Text.RegularExpressions;

namespace QL_DatVeMayBay.Services
{
    public static class VietnameseCurrencyHelper
    {
        private static readonly string[] ChuSo = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
        private static readonly string[] DonVi = { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };

        public static string FormatShortPrice(decimal amount)
        {
            if (amount <= 0) return "0đ";
            long thousands = (long)Math.Round(amount / 1000m, 0);
            return $"{thousands:N0}k";
        }

        public static string FormatVnd(decimal amount) => $"{amount:N0} VNĐ";

        public static bool IsValidTaxCode(string? mst)
        {
            if (string.IsNullOrWhiteSpace(mst)) return false;
            return Regex.IsMatch(mst.Trim(), @"^(\d{10}|\d{10}-\d{3}|\d{13})$");
        }

        public static string NumberToVietnameseWords(decimal number)
        {
            long val = (long)Math.Round(Math.Abs(number), 0);
            if (val == 0) return "Không đồng.";

            var groups = new List<int>();
            while (val > 0)
            {
                groups.Add((int)(val % 1000));
                val /= 1000;
            }

            var sb = new StringBuilder();
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                int g = groups[i];
                if (g == 0) continue;

                bool showZeroHundred = i < groups.Count - 1 && g < 100;
                string block = ReadThreeDigits(g, showZeroHundred || i < groups.Count - 1);
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(block);
                if (!string.IsNullOrEmpty(DonVi[i]))
                {
                    sb.Append(' ').Append(DonVi[i]);
                }
            }

            string text = sb.ToString().Trim();
            if (text.Length > 0)
                text = char.ToUpper(text[0]) + text[1..];
            return text + " đồng chẵn.";
        }

        private static string ReadThreeDigits(int n, bool fullHundred)
        {
            int tram = n / 100;
            int chuc = (n % 100) / 10;
            int donvi = n % 10;
            var sb = new StringBuilder();

            if (tram > 0 || fullHundred)
            {
                sb.Append(ChuSo[tram]).Append(" trăm");
                if (chuc == 0 && donvi > 0) sb.Append(" linh");
            }

            if (chuc > 1)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ChuSo[chuc]).Append(" mươi");
                if (donvi == 1) sb.Append(" mốt");
                else if (donvi == 4) sb.Append(" tư");
                else if (donvi == 5) sb.Append(" lăm");
                else if (donvi > 0) sb.Append(' ').Append(ChuSo[donvi]);
            }
            else if (chuc == 1)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append("mười");
                if (donvi == 5) sb.Append(" lăm");
                else if (donvi > 0) sb.Append(' ').Append(ChuSo[donvi]);
            }
            else if (donvi > 0)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ChuSo[donvi]);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Biểu giá chuẩn hàng không nội địa &amp; khu vực Việt Nam (Vietnam Airlines / Vietjet Air):
    /// - Thuế &amp; phí sân bay Người lớn: 220.000đ/chặng nội địa (100k PSC + 20k soi chiếu + 100k quản trị/VAT) | 550.000đ/chặng quốc tế.
    /// - Trẻ em (2 - &lt;12 tuổi): 90% giá vé hạng ghế + 50% thuế/phí sân bay (110.000đ nội địa / 275.000đ quốc tế).
    /// - Em bé (&lt;2 tuổi, không chiếm ghế): 10% giá cơ bản (tối thiểu 100.000đ/chặng) + 0đ thuế/phí.
    /// </summary>
    public static class FlightPricingHelper
    {
        public const decimal DomesticAdultTax = 220000m;
        public const decimal InternationalAdultTax = 550000m;
        public const decimal EcoRescheduleFee = 350000m;
        public const decimal DomesticMinBaseFare = 390000m;
        public const decimal InternationalMinBaseFare = 1200000m;
        public const decimal MaxBaseFare = 25000000m;

        public static decimal AdultTax(bool isIntl) => isIntl ? InternationalAdultTax : DomesticAdultTax;
        public static decimal ChildTax(bool isIntl) => Math.Round(AdultTax(isIntl) * 0.50m / 1000m, 0) * 1000m;
        public static decimal ChildFare(decimal classFare) => Math.Round(classFare * 0.90m / 1000m, 0) * 1000m;
        public static decimal InfantFare(decimal baseFare) => Math.Max(100000m, Math.Round(baseFare * 0.10m / 1000m, 0) * 1000m);
    }
}
