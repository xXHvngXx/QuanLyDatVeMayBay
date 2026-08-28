using System;
using System.Collections.Generic;
using System.Linq;

namespace QL_DatVeMayBay.DAL
{
    public class ChuyenBayDAL
    {
        private readonly QL_DatVeMayBayEntities _db = new QL_DatVeMayBayEntities();

        #region 1. TRUY VẤN VIEW & TIM KIẾM

        /// <summary>
        /// Lấy toàn bộ danh sách chuyến bay chi tiết 
        /// </summary>
        public List<vw_DanhSachChuyenBayChiTiet> GetAllChuyenBayChiTiet()
        {
            return _db.vw_DanhSachChuyenBayChiTiet.ToList();
        }

        /// <summary>
        /// Lọc chuyến bay
        /// </summary>
        public List<sp_TimKiemChuyenBay_Result> TimKiemChuyenBay(string maSanBayDi, string maSanBayDen, DateTime? ngayBay, string maHangGhe = null, decimal? giaMax = null, string sapXep = null)
        {
            try
            {
                return _db.sp_TimKiemChuyenBay(
                    maSanBayDi,
                    maSanBayDen,
                    ngayBay,
                    maHangGhe,
                    giaMax,
                    sapXep
                ).ToList();
            }
            catch
            {
                return new List<sp_TimKiemChuyenBay_Result>();
            }
        }

        /// <summary>
        /// Lấy thông tin 1 chuyến bay theo Mã chuyến bay
        /// </summary>
        public ChuyenBay GetById(string maChuyenBay)
        {
            return _db.ChuyenBay.FirstOrDefault(cb => cb.MaChuyenBay == maChuyenBay);
        }

        #endregion

        #region 2. QUẢN LÝ THÊM / SỬA / XÓA (ADMIN & NHÂN VIÊN)

        /// <summary>
        /// Thêm mới một chuyến bay
        /// </summary>
        public bool Insert(ChuyenBay cb)
        {
            try
            {
                _db.ChuyenBay.Add(cb);
                return _db.SaveChanges() > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Cập nhật thông tin chuyến bay (Trạng thái, Giờ bay...)
        /// </summary>
        public bool Update(ChuyenBay cb)
        {
            try
            {
                var existingCB = _db.ChuyenBay.FirstOrDefault(x => x.MaChuyenBay == cb.MaChuyenBay);
                if (existingCB == null) return false;

                existingCB.MaTuyenBay = cb.MaTuyenBay;
                existingCB.MaMayBay = cb.MaMayBay;
                existingCB.NgayGioBay = cb.NgayGioBay;
                existingCB.NgayGioDenDK = cb.NgayGioDenDK;
                existingCB.GiaVeCoBan = cb.GiaVeCoBan;
                existingCB.TrangThai = cb.TrangThai;

                return _db.SaveChanges() > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hủy chuyến bay 
        /// </summary>
        public bool UpdateTrangThai(string maChuyenBay, string trangThaiMoi)
        {
            try
            {
                var cb = _db.ChuyenBay.FirstOrDefault(x => x.MaChuyenBay == maChuyenBay);
                if (cb == null) return false;

                cb.TrangThai = trangThaiMoi;
                return _db.SaveChanges() > 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}