    using System;
    using System.Collections.Generic;
    using System.Linq;
    using QL_DatVeMayBay.DAL;
    using QL_DatVeMayBay.DTO;

    namespace QL_DatVeMayBay.BLL
    {
        public class ChuyenBayBLL
        {
            private readonly ChuyenBayDAL _chuyenBayDAL = new ChuyenBayDAL();

            #region 1. LẤY DỮ LIỆU & TÌM KIẾM DÙNG DTO

            /// <summary>
            /// Lấy toàn bộ danh sách chuyến bay và map sang DTO
            /// </summary>
            public List<ChuyenBayViewDTO> LayDanhSachChuyenBay()
            {
                var dsView = _chuyenBayDAL.GetAllChuyenBayChiTiet();

                return dsView.Select(x => new ChuyenBayViewDTO
                {
                    MaChuyenBay = x.MaChuyenBay,
                    TenHang = x.TenHangHangKhong,
                    SanBayDi = x.TenSanBayDi,
                    SanBayDen = x.TenSanBayDen,
                    NgayGioBay = x.NgayGioBay,
                    ThoiGianBay = x.ThoiGianBay,
                    GiaVeCoBan = x.GiaVeCoBan,
                    SoGheTrong = x.SoGheTrong,
                    TrangThai = x.TrangThaiChuyenBay 
                }).ToList();
            }

            /// <summary>
            /// Tìm kiếm chuyến bay qua Stored Procedure và map sang DTO
            /// </summary>
            public List<ChuyenBayViewDTO> TimKiemChuyenBay(string maSBDi, string maSBDen, DateTime? ngayBay, string maHangGhe = null, decimal? giaMax = null, string sapXep = null)
            {
                var dsResult = _chuyenBayDAL.TimKiemChuyenBay(maSBDi, maSBDen, ngayBay, maHangGhe, giaMax, sapXep);

                return dsResult.Select(x => new ChuyenBayViewDTO
                {
                    MaChuyenBay = x.MaChuyenBay,
                    TenHang = x.TenHang,
                    SanBayDi = x.SanBayDi,
                    SanBayDen = x.SanBayDen,
                    NgayGioBay = x.NgayGioBay,
                    ThoiGianBay = x.ThoiGianBay,
                    GiaVeCoBan = x.GiaVeCoBan,
                    SoGheTrong = x.SoGheTrong,
                    TrangThai = x.TrangThai
                }).ToList();
            }

            /// <summary>
            /// Lấy thông tin chuyến bay theo mã
            /// </summary>
            public ChuyenBay GetById(string maChuyenBay)
            {
                return _chuyenBayDAL.GetById(maChuyenBay);
            }

            #endregion

            #region 2. QUẢN LÝ THÊM / SỬA / XÓA (VALIDATION LOGIC)

            /// <summary>
            /// Thêm mới chuyến bay có kiểm tra điều kiện
            /// </summary>
            public bool ThemChuyenBay(ChuyenBay cb, out string errorMessage)
            {
                errorMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(cb.MaChuyenBay))
                {
                    errorMessage = "Mã chuyến bay không được để trống!";
                    return false;
                }

                if (cb.NgayGioBay < DateTime.Now)
                {
                    errorMessage = "Thời gian cất cánh không được nằm trong quá khứ!";
                    return false;
                }

                if (cb.NgayGioDenDK <= cb.NgayGioBay)
                {
                    errorMessage = "Thời gian đến dự kiến phải sau thời gian cất cánh!";
                    return false;
                }

                if (cb.GiaVeCoBan < 0)
                {
                    errorMessage = "Giá vé cơ bản không được nhỏ hơn 0!";
                    return false;
                }

                bool result = _chuyenBayDAL.Insert(cb);
                if (!result)
                {
                    errorMessage = "Thêm chuyến bay thất bại! Mã chuyến bay có thể đã tồn tại.";
                }

                return result;
            }

            /// <summary>
            /// Cập nhật chuyến bay có kiểm tra điều kiện
            /// </summary>
            public bool CapNhatChuyenBay(ChuyenBay cb, out string errorMessage)
            {
                errorMessage = string.Empty;

                if (cb.NgayGioDenDK <= cb.NgayGioBay)
                {
                    errorMessage = "Thời gian đến dự kiến phải sau thời gian cất cánh!";
                    return false;
                }

                if (cb.GiaVeCoBan < 0)
                {
                    errorMessage = "Giá vé cơ bản không được nhỏ hơn 0!";
                    return false;
                }

                bool result = _chuyenBayDAL.Update(cb);
                if (!result)
                {
                    errorMessage = "Cập nhật dữ liệu vào cơ sở dữ liệu thất bại!";
                }

                return result;
            }

            /// <summary>
            /// Cập nhật trạng thái chuyến bay (Đã cất cánh, Đã hủy, Trễ giờ...)
            /// </summary>
            public bool CapNhatTrangThai(string maChuyenBay, string trangThaiMoi, out string errorMessage)
            {
                errorMessage = string.Empty;

                if (string.IsNullOrWhiteSpace(maChuyenBay))
                {
                    errorMessage = "Mã chuyến bay không hợp lệ!";
                    return false;
                }

                bool result = _chuyenBayDAL.UpdateTrangThai(maChuyenBay, trangThaiMoi);
                if (!result)
                {
                    errorMessage = "Không thể cập nhật trạng thái chuyến bay!";
                }

                return result;
            }

            #endregion
        }
    }