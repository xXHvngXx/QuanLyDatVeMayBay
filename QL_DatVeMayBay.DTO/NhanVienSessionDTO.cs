using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QL_DatVeMayBay.DTO
{
    public class NhanVienSessionDTO
    {
        public string MaNV { get; set; }
        public string TenNV { get; set; }
        public string Email { get; set; }
        public string ChucVu { get; set; }
        public int RoleID { get; set; }
        public string TenRole { get; set; }
    }
}
