using System;
using System.Collections.Generic;
using System.Linq;

namespace QL_DatVeMayBay.DAL
{
    public class SanBayDAL
    {
        private readonly QL_DatVeMayBayEntities _db = new QL_DatVeMayBayEntities();

        // Lấy toàn bộ danh sách sân bay
        public List<SanBay> GetAll()
        {
            try
            {
                return _db.SanBay.ToList();
            }
            catch
            {
                return new List<SanBay>();
            }
        }

        // Lấy sân bay theo mã
        public SanBay GetById(string maSanBay)
        {
            return _db.SanBay.FirstOrDefault(x => x.MaSanBay == maSanBay);
        }
    }
}