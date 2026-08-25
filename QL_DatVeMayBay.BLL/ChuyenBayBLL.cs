using System;
using System.Collections.Generic;
using System.Linq;
using QL_DatVeMayBay.DAL;
using QL_DatVeMayBay.DTO;

namespace QL_DatVeMayBay.BLL
{
    public class ChuyenBayBLL
    {
        public List<ChuyenBayViewDTO> LayDanhSachChuyenBay()
        {
            using (var db = new QL_DatVeMayBayEntities())
            {
                return db.ChuyenBay.Select(x => new ChuyenBayViewDTO
                {
                    MaChuyenBay = x.MaChuyenBay,
                    TenHang = x.MayBay != null && x.MayBay.HangHangKhong != null
                              ? x.MayBay.HangHangKhong.TenHang : "",

                    SanBayDi = x.TuyenBay != null && x.TuyenBay.SanBay != null
                               ? x.TuyenBay.SanBay.TenSanBay : (x.TuyenBay != null ? x.TuyenBay.MaSanBayDi : ""),
                    SanBayDen = x.TuyenBay != null && x.TuyenBay.SanBay1 != null
                                ? x.TuyenBay.SanBay1.TenSanBay : (x.TuyenBay != null ? x.TuyenBay.MaSanBayDen : ""),

                    NgayGioBay = x.NgayGioBay,
                    ThoiGianBay = x.ThoiGianBay,
                    GiaVeCoBan = x.GiaVeCoBan,
                    SoGheTrong = x.SoGheTrong,
                    TrangThai = x.TrangThai
                }).ToList();
            }
        }

        public List<ChuyenBayViewDTO> TimKiemChuyenBay(string maSBDi, string maSBDen, DateTime? ngayBay)
        {
            using (var db = new QL_DatVeMayBayEntities())
            {
                var query = db.ChuyenBay.AsQueryable();

                if (!string.IsNullOrEmpty(maSBDi))
                    query = query.Where(x => x.TuyenBay.MaSanBayDi == maSBDi);

                if (!string.IsNullOrEmpty(maSBDen))
                    query = query.Where(x => x.TuyenBay.MaSanBayDen == maSBDen);

                if (ngayBay.HasValue)
                    query = query.Where(x => x.NgayGioBay.Year == ngayBay.Value.Year
                                          && x.NgayGioBay.Month == ngayBay.Value.Month
                                          && x.NgayGioBay.Day == ngayBay.Value.Day);

                return query.Select(x => new ChuyenBayViewDTO
                {
                    MaChuyenBay = x.MaChuyenBay,
                    TenHang = x.MayBay != null && x.MayBay.HangHangKhong != null
                              ? x.MayBay.HangHangKhong.TenHang : "",
                    SanBayDi = x.TuyenBay != null && x.TuyenBay.SanBay1 != null
                                ? x.TuyenBay.SanBay1.TenSanBay : (x.TuyenBay != null ? x.TuyenBay.MaSanBayDi : ""),
                    SanBayDen = x.TuyenBay != null && x.TuyenBay.SanBay != null
                                ? x.TuyenBay.SanBay.TenSanBay : (x.TuyenBay != null ? x.TuyenBay.MaSanBayDen : ""),
                    NgayGioBay = x.NgayGioBay,
                    ThoiGianBay = x.ThoiGianBay,
                    GiaVeCoBan = x.GiaVeCoBan,
                    SoGheTrong = x.SoGheTrong,
                    TrangThai = x.TrangThai
                }).ToList();
            }
        }
    }
}