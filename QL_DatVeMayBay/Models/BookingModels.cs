using System.Text.RegularExpressions;

namespace QL_DatVeMayBay.Models
{
    public class BookingDraftModel
    {
        public string MaChuyenBayDi { get; set; } = string.Empty;
        public string HangGheDi { get; set; } = "ECO";
        public string? MaChuyenBayDi2 { get; set; }
        public string? MaChuyenBayVe { get; set; }
        public string HangGheVe { get; set; } = "ECO";
        public string? MaChuyenBayVe2 { get; set; }
        public int NguoiLon { get; set; } = 1;
        public int TreEm { get; set; }
        public int EmBe { get; set; }
        public string? MaKM { get; set; }
        public string NguoiLienHe { get; set; } = string.Empty;
        public string SdtLienHe { get; set; } = string.Empty;
        public string EmailLienHe { get; set; } = string.Empty;
        public List<PassengerInputModel> Passengers { get; set; } = new();
    }

    public class PassengerInputModel
    {
        public string GioiTinh { get; set; } = "Nam";
        public string TenHanhKhach { get; set; } = string.Empty;
        public string LoaiHanhKhach { get; set; } = "Người lớn"; // Người lớn | Trẻ em | Em bé
        public DateTime NgaySinh { get; set; }
        public string LoaiGiayTo { get; set; } = "CCCD"; // CCCD | Hộ chiếu | Giấy khai sinh
        public string SoGiayTo { get; set; } = string.Empty;
        public DateTime? HanHoChieu { get; set; }
        public string QuocTich { get; set; } = "Việt Nam";
        public string? NguoiLonDiCung { get; set; }
    }

    public static class PassengerDocumentValidator
    {
        public static string? ValidatePassengers(
            List<PassengerInputModel> passengers,
            DateTime firstFlightDate,
            DateTime lastFlightDate,
            bool isInternationalRoute)
        {
            if (passengers.Count == 0 || passengers.Count > 9)
                return "Tổng số hành khách mỗi đơn phải từ 1 đến 9 người.";

            int adults = passengers.Count(p => p.LoaiHanhKhach == "Người lớn");
            int infants = passengers.Count(p => p.LoaiHanhKhach == "Em bé");
            if (adults < 1)
                return "Phải có ít nhất 1 Người lớn (≥ 12 tuổi) đi cùng trong đơn đặt vé.";
            if (infants > adults)
                return "Mỗi Người lớn chỉ được kèm tối đa 1 Em bé (< 2 tuổi) ngồi cùng.";

            var seenDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < passengers.Count; i++)
            {
                var p = passengers[i];
                int idx = i + 1;
                if (string.IsNullOrWhiteSpace(p.TenHanhKhach) || p.TenHanhKhach.Trim().Length < 2)
                    return $"Hành khách #{idx}: Vui lòng nhập đầy đủ họ tên.";

                if (p.NgaySinh >= DateTime.Today)
                    return $"Hành khách #{idx} ({p.TenHanhKhach}): Ngày sinh không hợp lệ.";

                int age = CalculateAgeAtDate(p.NgaySinh, firstFlightDate);

                if (p.LoaiHanhKhach == "Người lớn" && age < 12)
                    return $"Hành khách #{idx} ({p.TenHanhKhach}): Loại 'Người lớn' yêu cầu từ 12 tuổi trở lên tại ngày bay (tuổi tính được: {age}).";
                if (p.LoaiHanhKhach == "Trẻ em" && (age < 2 || age >= 12))
                    return $"Hành khách #{idx} ({p.TenHanhKhach}): Loại 'Trẻ em' phải từ 2 đến dưới 12 tuổi tại ngày bay (tuổi tính được: {age}).";
                if (p.LoaiHanhKhach == "Em bé" && age >= 2)
                    return $"Hành khách #{idx} ({p.TenHanhKhach}): Loại 'Em bé' phải dưới 2 tuổi tại ngày bay (tuổi tính được: {age}).";

                string doc = (p.SoGiayTo ?? string.Empty).Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(doc))
                    return $"Hành khách #{idx} ({p.TenHanhKhach}): Số giấy tờ tùy thân là bắt buộc.";

                if (isInternationalRoute)
                {
                    if (p.LoaiGiayTo != "Hộ chiếu")
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Chặng bay quốc tế bắt buộc dùng Hộ chiếu.";
                }

                if (p.LoaiGiayTo == "CCCD")
                {
                    if (p.LoaiHanhKhach != "Người lớn")
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Trẻ em/Em bé chọn Giấy khai sinh hoặc Hộ chiếu.";
                    if (!Regex.IsMatch(doc, @"^\d{12}$"))
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Số CCCD Việt Nam phải gồm đúng 12 chữ số.";
                }
                else if (p.LoaiGiayTo == "Hộ chiếu")
                {
                    if (!Regex.IsMatch(doc, @"^[A-Z0-9]{6,12}$"))
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Số Hộ chiếu phải gồm 6–12 ký tự chữ/số.";
                    if (!p.HanHoChieu.HasValue)
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Vui lòng nhập Ngày hết hạn Hộ chiếu.";
                    DateTime minExpiry = isInternationalRoute ? lastFlightDate.AddMonths(6) : lastFlightDate.Date;
                    if (p.HanHoChieu.Value.Date < minExpiry.Date)
                    {
                        return isInternationalRoute
                            ? $"Hành khách #{idx} ({p.TenHanhKhach}): Chặng quốc tế yêu cầu Hộ chiếu còn hạn ≥ 6 tháng tính từ ngày bay về ({minExpiry:dd/MM/yyyy})."
                            : $"Hành khách #{idx} ({p.TenHanhKhach}): Hộ chiếu đã hết hạn vào ngày bay.";
                    }
                }
                else if (p.LoaiGiayTo == "Giấy khai sinh")
                {
                    if (p.LoaiHanhKhach == "Người lớn")
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Người lớn bắt buộc dùng CCCD hoặc Hộ chiếu.";
                    if (doc.Length < 4)
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Vui lòng nhập số/mã Giấy khai sinh hợp lệ.";
                    if (string.IsNullOrWhiteSpace(p.NguoiLonDiCung))
                        return $"Hành khách #{idx} ({p.TenHanhKhach}): Trẻ em/Em bé cần khai báo tên Người lớn đi cùng.";
                }

                if (p.LoaiHanhKhach != "Em bé" && !seenDocs.Add($"{p.LoaiGiayTo}:{doc}"))
                    return $"Số giấy tờ {doc} bị trùng lặp giữa các hành khách trong cùng đơn!";
            }

            return null;
        }

        private static int CalculateAgeAtDate(DateTime dob, DateTime onDate)
        {
            int age = onDate.Year - dob.Year;
            if (dob.Date > onDate.Date.AddYears(-age)) age--;
            return age;
        }
    }
}
