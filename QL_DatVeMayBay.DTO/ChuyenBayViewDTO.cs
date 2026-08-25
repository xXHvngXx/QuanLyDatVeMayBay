using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QL_DatVeMayBay.DTO
{
    public class ChuyenBayViewDTO
    {
        public string MaChuyenBay { get; set; }
        public string TenHang { get; set; }
        public string SanBayDi { get; set; }
        public string SanBayDen { get; set; }
        public DateTime NgayGioBay { get; set; }
        public int? ThoiGianBay { get; set; }
        public decimal GiaVeCoBan { get; set; }
        public int SoGheTrong { get; set; }
        public string TrangThai { get; set; }
    }
}
