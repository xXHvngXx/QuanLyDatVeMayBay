using System.Collections.Generic;
using QL_DatVeMayBay.DAL;

namespace QL_DatVeMayBay.BLL
{
    public class SanBayBLL
    {
        private readonly SanBayDAL _sanBayDAL = new SanBayDAL();

        /// <summary>
        /// Lấy danh sách tất cả sân bay 
        /// </summary>
        public List<SanBay> LayDanhSachSanBay()
        {
            return _sanBayDAL.GetAll();
        }

        /// <summary>
        /// Lấy thông tin sân bay theo mã sân bay
        /// </summary>
        public SanBay GetById(string maSanBay)
        {
            if (string.IsNullOrWhiteSpace(maSanBay)) return null;
            return _sanBayDAL.GetById(maSanBay);
        }
    }
}