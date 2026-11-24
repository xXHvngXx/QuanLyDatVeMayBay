-- ====================================================================================================
-- HỆ THỐNG: QUẢN LÝ ĐẶT VÉ MÁY BAY SKYVN AIR (PTTK HTTT - V2 PRODUCTION UPGRADE)
-- SQL Server 2019/2022+ | ADO.NET Stored Procedures | Transactional Concurrency | Filtered Indexes
-- ====================================================================================================
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

USE master;
GO

IF EXISTS (SELECT name FROM sys.server_principals WHERE name = 'airline_admin')    DROP LOGIN airline_admin;
IF EXISTS (SELECT name FROM sys.server_principals WHERE name = 'airline_thungan')  DROP LOGIN airline_thungan;
IF EXISTS (SELECT name FROM sys.server_principals WHERE name = 'airline_banve')    DROP LOGIN airline_banve;
IF EXISTS (SELECT name FROM sys.server_principals WHERE name = 'airline_cskh')     DROP LOGIN airline_cskh;
GO

USE msdb;
GO
BEGIN TRY
    IF EXISTS (SELECT job_id FROM msdb.dbo.sysjobs WHERE name = 'auto_maintenance_QL_DatVeMayBay')
        EXEC sp_delete_job @job_name = 'auto_maintenance_QL_DatVeMayBay';
END TRY
BEGIN CATCH
END CATCH
GO

USE master;
GO
IF DB_ID('QL_DatVeMayBay') IS NOT NULL
BEGIN
    ALTER DATABASE QL_DatVeMayBay SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE QL_DatVeMayBay;
END
GO

CREATE DATABASE QL_DatVeMayBay;
GO
USE QL_DatVeMayBay;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ====================================================================================================
-- PHẦN 1: CẤU TRÚC BẢNG CHUẨN HÓA (21 BẢNG)
-- ====================================================================================================

-- 1. Hãng Hàng Không
CREATE TABLE HangHangKhong
(
    MaHang      varchar(10)   NOT NULL,
    TenHang     nvarchar(100) NOT NULL UNIQUE,
    QuocGia     nvarchar(50)  NOT NULL DEFAULT N'Việt Nam',
    Logo        varchar(255)  NULL,
    TrangThai   bit           NOT NULL DEFAULT 1,
    CONSTRAINT PK_HangHangKhong PRIMARY KEY (MaHang)
);
GO

-- 2. Sân Bay & Điểm Đến (Kèm hình ảnh và mô tả điểm đến)
CREATE TABLE SanBay
(
    MaSanBay         varchar(10)   NOT NULL,
    TenSanBay        nvarchar(100) NOT NULL,
    ThanhPho         nvarchar(100) NOT NULL,
    QuocGia          nvarchar(50)  NOT NULL DEFAULT N'Việt Nam',
    KhuVuc           nvarchar(50)  NOT NULL DEFAULT N'Nội địa',
    HinhAnhUrl       varchar(255)  NOT NULL DEFAULT '/images/destinations/default.svg',
    HinhAnhBannerUrl varchar(255)  NULL,
    MoTaDiemDen      nvarchar(300) NULL,
    TrangThai        bit           NOT NULL DEFAULT 1,
    CONSTRAINT PK_SanBay PRIMARY KEY (MaSanBay)
);
GO

-- 3. Tuyến Bay
CREATE TABLE TuyenBay
(
    MaTuyenBay   varchar(15)   NOT NULL,
    MaSanBayDi   varchar(10)   NOT NULL,
    MaSanBayDen  varchar(10)   NOT NULL,
    KhoangCachKm decimal(8,2)  NULL CHECK (KhoangCachKm > 0),
    CONSTRAINT PK_TuyenBay PRIMARY KEY (MaTuyenBay),
    CONSTRAINT FK_TuyenBay_SBDi  FOREIGN KEY (MaSanBayDi)  REFERENCES SanBay(MaSanBay),
    CONSTRAINT FK_TuyenBay_SBDen FOREIGN KEY (MaSanBayDen) REFERENCES SanBay(MaSanBay),
    CONSTRAINT CHK_TuyenBay_KhacNhau CHECK (MaSanBayDi <> MaSanBayDen)
);
GO

-- 4. Máy Bay
CREATE TABLE MayBay
(
    MaMayBay    varchar(15)  NOT NULL,
    TenMayBay   nvarchar(60) NOT NULL,
    MaHang      varchar(10)  NOT NULL,
    TongSoGhe   int          NOT NULL CHECK (TongSoGhe > 0),
    TrangThai   nvarchar(30) NOT NULL DEFAULT N'Sẵn sàng'
                CHECK (TrangThai IN (N'Sẵn sàng', N'Đang bay', N'Bảo trì')),
    CONSTRAINT PK_MayBay PRIMARY KEY (MaMayBay),
    CONSTRAINT FK_MayBay_Hang FOREIGN KEY (MaHang) REFERENCES HangHangKhong(MaHang)
);
GO

-- 5. Hạng Ghế
CREATE TABLE HangGhe
(
    MaHangGhe  varchar(10)   NOT NULL,
    TenHangGhe nvarchar(50)  NOT NULL UNIQUE,
    HeSoGia    decimal(4,2)  NOT NULL DEFAULT 1.00 CHECK (HeSoGia >= 1.00),
    MoTa       nvarchar(200) NULL,
    CONSTRAINT PK_HangGhe PRIMARY KEY (MaHangGhe)
);
GO

-- 6. Sơ Đồ Ghế Ngồi
CREATE TABLE GheNgoi
(
    MaGhe      int IDENTITY(1,1) NOT NULL,
    MaMayBay   varchar(15)       NOT NULL,
    SoGhe      varchar(10)       NOT NULL,
    MaHangGhe  varchar(10)       NOT NULL,
    ViTri      nvarchar(20)      NOT NULL DEFAULT N'Tiêu chuẩn' CHECK (ViTri IN (N'Cửa sổ', N'Giữa', N'Lối đi', N'Tiêu chuẩn')),
    GiaChonGhe money             NOT NULL DEFAULT 0 CHECK (GiaChonGhe >= 0),
    TrangThai  bit               NOT NULL DEFAULT 1,
    CONSTRAINT PK_GheNgoi PRIMARY KEY (MaGhe),
    CONSTRAINT FK_GheNgoi_MayBay  FOREIGN KEY (MaMayBay)  REFERENCES MayBay(MaMayBay) ON DELETE CASCADE,
    CONSTRAINT FK_GheNgoi_HangGhe FOREIGN KEY (MaHangGhe) REFERENCES HangGhe(MaHangGhe),
    CONSTRAINT UQ_MayBay_SoGhe UNIQUE (MaMayBay, SoGhe)
);
GO

-- 7. Chuyến Bay
CREATE TABLE ChuyenBay
(
    MaChuyenBay   varchar(20)  NOT NULL,
    MaTuyenBay    varchar(15)  NOT NULL,
    MaMayBay      varchar(15)  NOT NULL,
    NgayGioBay    datetime     NOT NULL,
    NgayGioDenDK  datetime     NOT NULL,
    ThoiGianBay   int          NULL,
    GiaVeCoBan    money        NOT NULL CHECK (GiaVeCoBan > 0),
    SoGheTrong    int          NOT NULL CHECK (SoGheTrong >= 0),
    TrangThai     nvarchar(30) NOT NULL DEFAULT N'Đang mở bán'
                  CHECK (TrangThai IN (N'Đang mở bán', N'Hết vé', N'Đã cất cánh', N'Đã hủy', N'Hoãn chuyến')),
    CONSTRAINT PK_ChuyenBay PRIMARY KEY (MaChuyenBay),
    CONSTRAINT FK_ChuyenBay_Tuyen  FOREIGN KEY (MaTuyenBay) REFERENCES TuyenBay(MaTuyenBay),
    CONSTRAINT FK_ChuyenBay_MayBay FOREIGN KEY (MaMayBay)   REFERENCES MayBay(MaMayBay),
    CONSTRAINT CHK_ThoiGianBay CHECK (NgayGioDenDK > NgayGioBay)
);
GO

CREATE NONCLUSTERED INDEX IX_ChuyenBay_Search
ON ChuyenBay (MaTuyenBay, NgayGioBay, TrangThai) INCLUDE (MaMayBay, GiaVeCoBan, SoGheTrong);
GO

-- 8. Điểm Dừng Kỹ Thuật Trên Cùng Số Hiệu Chuyến Bay (Stopover)
CREATE TABLE ChiTietChuyenBay
(
    MaCTCB        int IDENTITY(1,1) NOT NULL,
    MaChuyenBay   varchar(20)       NOT NULL,
    MaSanBayTG    varchar(10)       NOT NULL,
    ThoiGianDung  int               NOT NULL CHECK (ThoiGianDung > 0),
    ThuTuDung     int               NOT NULL DEFAULT 1,
    GhiChu        nvarchar(200)     NULL,
    CONSTRAINT PK_ChiTietChuyenBay PRIMARY KEY (MaCTCB),
    CONSTRAINT FK_CTCB_ChuyenBay FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay) ON DELETE CASCADE,
    CONSTRAINT FK_CTCB_SanBay    FOREIGN KEY (MaSanBayTG)  REFERENCES SanBay(MaSanBay),
    CONSTRAINT UQ_ChuyenBay_SanBayTG UNIQUE (MaChuyenBay, MaSanBayTG)
);
GO

-- 9. Vai Trò
CREATE TABLE VaiTro
(
    RoleID  int           NOT NULL,
    TenRole nvarchar(50)  NOT NULL UNIQUE,
    MoTa    nvarchar(150) NULL,
    CONSTRAINT PK_VaiTro PRIMARY KEY (RoleID)
);
GO

-- 10. Nhân Viên (Mật khẩu PBKDF2-SHA256)
CREATE TABLE NhanVien
(
    MaNV      varchar(10)   NOT NULL,
    TenNV     nvarchar(60)  NOT NULL,
    SDT       varchar(15)   NULL UNIQUE,
    Email     varchar(100)  NULL UNIQUE,
    ChucVu    nvarchar(50)  NULL,
    Luong     money         NOT NULL DEFAULT 0 CHECK (Luong >= 0),
    TaiKhoan  varchar(50)   NOT NULL UNIQUE,
    MatKhau   varchar(255)  NOT NULL,
    RoleID    int           NOT NULL DEFAULT 2,
    TrangThai bit           NOT NULL DEFAULT 1,
    NgayTao   datetime      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_NhanVien PRIMARY KEY (MaNV),
    CONSTRAINT FK_NhanVien_VaiTro FOREIGN KEY (RoleID) REFERENCES VaiTro(RoleID)
);
GO

-- 11. Khách Hàng (Mật khẩu PBKDF2-SHA256)
CREATE TABLE KhachHang
(
    MaKH             varchar(10)   NOT NULL,
    TenKH            nvarchar(60)  NOT NULL,
    GioiTinh         nvarchar(10)  NOT NULL CHECK (GioiTinh IN (N'Nam', N'Nữ', N'Khác')),
    NgaySinh         date          NOT NULL,
    SDT              varchar(15)   NOT NULL UNIQUE,
    Email            varchar(100)  NULL UNIQUE,
    MatKhau          varchar(255)  NOT NULL,
    DiaChi           nvarchar(200) NULL,
    LoaiKhachHang    nvarchar(30)  NOT NULL DEFAULT N'Việt Nam'
                     CHECK (LoaiKhachHang IN (N'Việt Nam', N'Nước ngoài', N'Doanh nghiệp')),
    CCCD             varchar(20)   NULL,
    HoChieu          varchar(30)   NULL,
    HanHoChieu       date          NULL,
    QuocTich         nvarchar(50)  NOT NULL DEFAULT N'Việt Nam',
    CoQuanCongTac    nvarchar(150) NULL,
    SoTK_DoanhNghiep varchar(30)   NULL,
    TrangThaiKhoa    bit           NOT NULL DEFAULT 0,
    NgayDangKy       datetime      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_KhachHang PRIMARY KEY (MaKH)
);
GO

-- 12. Khuyến Mãi / Voucher
CREATE TABLE KhuyenMai
(
    MaKM             varchar(20)   NOT NULL,
    TenKM            nvarchar(120) NOT NULL,
    PhanTramGiam     decimal(5,2)  NOT NULL DEFAULT 0 CHECK (PhanTramGiam >= 0 AND PhanTramGiam <= 100),
    GiamToiDa        money         NOT NULL DEFAULT 0 CHECK (GiamToiDa >= 0),
    DonHangToiThieu  money         NOT NULL DEFAULT 0 CHECK (DonHangToiThieu >= 0),
    NgayBatDau       date          NOT NULL,
    NgayKetThuc      date          NOT NULL,
    SoLuong          int           NOT NULL DEFAULT 100 CHECK (SoLuong >= 0),
    TrangThai        bit           NOT NULL DEFAULT 1,
    CONSTRAINT PK_KhuyenMai PRIMARY KEY (MaKM),
    CONSTRAINT CHK_KhuyenMai_Ngay CHECK (NgayKetThuc >= NgayBatDau)
);
GO

-- 13. Phiếu Đặt Chỗ (1 PNR duy nhất cho Một chiều / Khứ hồi / Quá cảnh nối chuyến)
CREATE TABLE PhieuDatCho
(
    MaDatCho           varchar(15)   NOT NULL,
    MaKH               varchar(10)   NOT NULL,
    NgayDat            datetime      NOT NULL DEFAULT GETDATE(),
    LoaiHanhTrinh      varchar(20)   NOT NULL DEFAULT 'MOT_CHIEU'
                       CHECK (LoaiHanhTrinh IN ('MOT_CHIEU', 'KHU_HOI', N'Một chiều', N'Khứ hồi')),
    MaDatChoLienKet    varchar(15)   NULL,
    LaHanhTrinhQuaCanh bit           NOT NULL DEFAULT 0,
    NguoiLienHe        nvarchar(60)  NULL,
    SDTLienHe          varchar(20)   NULL,
    EmailLienHe        varchar(100)  NULL,
    TongTienGoc        money         NOT NULL DEFAULT 0 CHECK (TongTienGoc >= 0),
    MaKM               varchar(20)   NULL,
    TienGiam           money         NOT NULL DEFAULT 0 CHECK (TienGiam >= 0),
    TongTienSauGiam    money         NOT NULL DEFAULT 0 CHECK (TongTienSauGiam >= 0),
    HanThanhToan       datetime      NOT NULL,
    TrangThai          nvarchar(30)  NOT NULL DEFAULT N'Chờ thanh toán'
                       CHECK (TrangThai IN (N'Chờ thanh toán', N'Chờ duyệt thanh toán', N'Đã thanh toán', N'Đã hủy', N'Hết hạn giữ chỗ', N'Hoàn tất')),
    GhiChu             nvarchar(255) NULL,
    CONSTRAINT PK_PhieuDatCho PRIMARY KEY (MaDatCho),
    CONSTRAINT FK_PhieuDatCho_KH FOREIGN KEY (MaKH) REFERENCES KhachHang(MaKH),
    CONSTRAINT FK_PhieuDatCho_KM FOREIGN KEY (MaKM) REFERENCES KhuyenMai(MaKM)
);
GO

-- 14. Hành Trình Chặng Bay Trong PNR (Hỗ trợ Khứ hồi + Nối chuyến 2 chặng trên cùng 1 PNR)
CREATE TABLE ChangBayDatCho
(
    MaChang              int IDENTITY(1,1) NOT NULL,
    MaDatCho             varchar(15)       NOT NULL,
    HuongBay             varchar(10)       NOT NULL CHECK (HuongBay IN ('DI', 'VE')),
    ThuTuChang           int               NOT NULL DEFAULT 1,
    MaChuyenBay          varchar(20)       NOT NULL,
    ThoiGianNoiChuyenPhut int              NULL,
    LaQuaCanh            bit               NOT NULL DEFAULT 0,
    CONSTRAINT PK_ChangBayDatCho PRIMARY KEY (MaChang),
    CONSTRAINT FK_CBDC_DatCho    FOREIGN KEY (MaDatCho)    REFERENCES PhieuDatCho(MaDatCho) ON DELETE CASCADE,
    CONSTRAINT FK_CBDC_ChuyenBay FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay),
    CONSTRAINT UQ_CBDC_DatCho_Huong_ThuTu UNIQUE (MaDatCho, HuongBay, ThuTuChang)
);
GO

-- 15. Vé Máy Bay (Filtered Unique Index cho ghế & giấy tờ đang hoạt động)
CREATE TABLE VeMayBay
(
    MaVe            varchar(25)   NOT NULL,
    MaDatCho        varchar(15)   NOT NULL,
    HuongBay        varchar(10)   NOT NULL DEFAULT 'DI' CHECK (HuongBay IN ('DI', 'VE')),
    ThuTuChang      int           NOT NULL DEFAULT 1,
    MaChuyenBay     varchar(20)   NOT NULL,
    MaGhe           int           NULL,
    MaHangGhe       varchar(10)   NOT NULL,
    TenHanhKhach    nvarchar(60)  NOT NULL,
    NgaySinh        date          NULL,
    CCCD_HanhKhach  varchar(30)   NULL,
    LoaiGiayTo      nvarchar(30)  NOT NULL DEFAULT N'CCCD'
                    CHECK (LoaiGiayTo IN (N'CCCD', N'Hộ chiếu', N'Giấy khai sinh')),
    SoGiayTo        varchar(30)   NULL,
    HanHoChieu      date          NULL,
    QuocTich        nvarchar(50)  NOT NULL DEFAULT N'Việt Nam',
    LoaiHanhKhach   nvarchar(20)  NOT NULL DEFAULT N'Người lớn'
                    CHECK (LoaiHanhKhach IN (N'Người lớn', N'Trẻ em', N'Em bé')),
    NguoiLonDiCung  nvarchar(60)  NULL,
    GiaCoBan        money         NOT NULL DEFAULT 0 CHECK (GiaCoBan >= 0),
    ThuePhi         money         NOT NULL DEFAULT 0 CHECK (ThuePhi >= 0),
    PhiChonGhe      money         NOT NULL DEFAULT 0 CHECK (PhiChonGhe >= 0),
    GiaVe           money         NOT NULL CHECK (GiaVe > 0),
    TrangThai       nvarchar(30)  NOT NULL DEFAULT N'Đã đặt chỗ'
                    CHECK (TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in', N'Đã hủy', N'Đã đổi')),
    CONSTRAINT PK_VeMayBay PRIMARY KEY (MaVe),
    CONSTRAINT FK_VeMayBay_PhieuDat FOREIGN KEY (MaDatCho)    REFERENCES PhieuDatCho(MaDatCho) ON DELETE CASCADE,
    CONSTRAINT FK_VeMayBay_Chuyen   FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay),
    CONSTRAINT FK_VeMayBay_Ghe      FOREIGN KEY (MaGhe)       REFERENCES GheNgoi(MaGhe),
    CONSTRAINT FK_VeMayBay_HangGhe  FOREIGN KEY (MaHangGhe)   REFERENCES HangGhe(MaHangGhe),
    CONSTRAINT CHK_VeMayBay_GheEmBe CHECK (LoaiHanhKhach = N'Em bé' OR MaGhe IS NOT NULL)
);
GO

-- Chỉ khóa duy nhất những ghế còn hiệu lực (không khóa ghế đã hủy/đổi)
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE UNIQUE NONCLUSTERED INDEX UX_VeMayBay_ActiveSeat
ON VeMayBay (MaChuyenBay, MaGhe)
WHERE MaGhe IS NOT NULL AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in');
GO

-- Chống trùng giấy tờ hành khách còn hiệu lực trên cùng chuyến bay
CREATE UNIQUE NONCLUSTERED INDEX UX_VeMayBay_ActivePassengerDoc
ON VeMayBay (MaChuyenBay, SoGiayTo)
WHERE SoGiayTo IS NOT NULL AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in');
GO

-- 16. Dịch Vụ Bổ Sung
CREATE TABLE DichVuBoSung
(
    MaDV                varchar(15)   NOT NULL,
    TenDV               nvarchar(100) NOT NULL,
    LoaiDV              nvarchar(30)  NOT NULL CHECK (LoaiDV IN (N'Hành lý', N'Suất ăn', N'Chọn ghế', N'Phòng chờ', N'Ưu tiên')),
    DonGia              money         NOT NULL CHECK (DonGia >= 0),
    MoTa                nvarchar(255) NULL,
    GioiHanGioTruocBay  int           NOT NULL DEFAULT 3,
    TrangThai           bit           NOT NULL DEFAULT 1,
    CONSTRAINT PK_DichVuBoSung PRIMARY KEY (MaDV)
);
GO

-- 17. Chi Tiết Dịch Vụ Đặt Vé
CREATE TABLE ChiTietDichVuDatVe
(
    MaCTDV              int IDENTITY(1,1) NOT NULL,
    MaDatCho            varchar(15)       NOT NULL,
    MaVe                varchar(25)       NULL,
    MaDV                varchar(15)       NOT NULL,
    SoLuong             int               NOT NULL DEFAULT 1 CHECK (SoLuong > 0),
    DonGia              money             NOT NULL CHECK (DonGia >= 0),
    ThanhTien           money             NOT NULL CHECK (ThanhTien >= 0),
    PhuongThucTT        nvarchar(50)      NULL,
    TrangThaiThanhToan  nvarchar(30)      NOT NULL DEFAULT N'Chờ thanh toán'
                        CHECK (TrangThaiThanhToan IN (N'Chờ thanh toán', N'Đã thanh toán', N'Đã hủy')),
    MaGiaoDich          varchar(100)      NULL,
    NgayMua             datetime          NOT NULL DEFAULT GETDATE(),
    NgayThanhToan       datetime          NULL,
    GhiChu              nvarchar(200)     NULL,
    CONSTRAINT PK_ChiTietDichVuDatVe PRIMARY KEY (MaCTDV),
    CONSTRAINT FK_CTDV_DatCho FOREIGN KEY (MaDatCho) REFERENCES PhieuDatCho(MaDatCho) ON DELETE CASCADE,
    CONSTRAINT FK_CTDV_DichVu FOREIGN KEY (MaDV)     REFERENCES DichVuBoSung(MaDV)
);
GO

-- 18. Giao Dịch Thanh Toán Online (VietQR Sandbox & Idempotent Webhook)
CREATE TABLE GiaoDichThanhToan
(
    MaGiaoDich    varchar(100)  NOT NULL,
    MaDatCho      varchar(15)   NOT NULL,
    LoaiGiaoDich  varchar(20)   NOT NULL DEFAULT 'VE_MAY_BAY'
                  CHECK (LoaiGiaoDich IN ('VE_MAY_BAY', 'DICH_VU', 'DOI_VE')),
    PhuongThuc        nvarchar(50)  NOT NULL DEFAULT N'Chuyển khoản ngân hàng (VietQR)',
    SoTien            money         NOT NULL CHECK (SoTien >= 0),
    NoiDungCK         nvarchar(120) NULL,
    QrImageUrl        nvarchar(500) NULL,
    AnhChuyenKhoanUrl nvarchar(300) NULL,
    NgayGuiMinhChung  datetime      NULL,
    MaNV_Duyet        varchar(10)   NULL,
    NgayDuyet         datetime      NULL,
    LyDoTuChoi        nvarchar(255) NULL,
    TrangThai         nvarchar(30)  NOT NULL DEFAULT N'Chờ thanh toán'
                      CHECK (TrangThai IN (N'Chờ thanh toán', N'Chờ duyệt', N'Thành công', N'Thất bại', N'Đã hủy', N'Hết hạn')),
    Signature     varchar(256)  NULL,
    NgayTao       datetime      NOT NULL DEFAULT GETDATE(),
    HanThanhToan  datetime      NOT NULL,
    NgayCapNhat   datetime      NULL,
    GhiChu        nvarchar(255) NULL,
    CONSTRAINT PK_GiaoDichThanhToan PRIMARY KEY (MaGiaoDich),
    CONSTRAINT FK_GDTT_DatCho FOREIGN KEY (MaDatCho) REFERENCES PhieuDatCho(MaDatCho) ON DELETE CASCADE
);
GO

-- 19. Hóa Đơn & Hóa Đơn VAT 72h
CREATE TABLE HoaDon
(
    MaHD                varchar(15)   NOT NULL,
    MaDatCho            varchar(15)   NOT NULL UNIQUE,
    MaNV                varchar(10)   NULL,
    NgayLap             datetime      NOT NULL DEFAULT GETDATE(),
    TienVe              money         NOT NULL DEFAULT 0 CHECK (TienVe >= 0),
    TienDichVu          money         NOT NULL DEFAULT 0 CHECK (TienDichVu >= 0),
    TongTien            money         NOT NULL CHECK (TongTien >= 0),
    PhuongThuc          nvarchar(50)  NOT NULL DEFAULT N'VietQR'
                        CHECK (PhuongThuc IN (N'Tiền mặt', N'Chuyển khoản ngân hàng (VietQR)', N'VietQR', N'Ví MoMo', N'Ví ZaloPay', N'Thẻ ATM nội địa', N'VISA/MasterCard', N'Internet Banking')),
    MaGiaoDichNganHang  varchar(100)  NULL,
    NgayThanhToan       datetime      NOT NULL DEFAULT GETDATE(),
    TrangThai           nvarchar(30)  NOT NULL DEFAULT N'Đã thanh toán'
                        CHECK (TrangThai IN (N'Đã thanh toán', N'Đã hủy', N'Đã hoàn tiền')),
    DaXuatHoaDon        bit           NOT NULL DEFAULT 0,
    NgayYeuCauXuatVAT   datetime      NULL,
    LoaiHoaDonVAT       nvarchar(20)  NOT NULL DEFAULT N'Cá nhân' CHECK (LoaiHoaDonVAT IN (N'Cá nhân', N'Doanh nghiệp')),
    TenCongTyVAT        nvarchar(150) NULL,
    MaSoThueVAT         varchar(20)   NULL,
    DiaChiCongTyVAT     nvarchar(250) NULL,
    EmailNhanHoaDon     varchar(100)  NULL,
    GhiChu              nvarchar(250) NULL,
    CONSTRAINT PK_HoaDon PRIMARY KEY (MaHD),
    CONSTRAINT FK_HoaDon_DatCho   FOREIGN KEY (MaDatCho) REFERENCES PhieuDatCho(MaDatCho),
    CONSTRAINT FK_HoaDon_NhanVien FOREIGN KEY (MaNV)     REFERENCES NhanVien(MaNV)
);
GO

-- 20. Yêu Cầu Hủy / Đổi Vé
CREATE TABLE YeuCauHuyDoiVe
(
    MaYeuCau       int IDENTITY(1,1) NOT NULL,
    MaVe           varchar(25)       NOT NULL,
    LoaiYeuCau     nvarchar(20)      NOT NULL CHECK (LoaiYeuCau IN (N'Hủy vé', N'Đổi vé')),
    NgayYeuCau     datetime          NOT NULL DEFAULT GETDATE(),
    LyDo           nvarchar(255)     NULL,
    MaChuyenBayMoi varchar(20)       NULL,
    MaGheMoi       int               NULL,
    PhanTramHoan   decimal(5,2)      NOT NULL DEFAULT 0,
    SoTienGoc      money             NOT NULL DEFAULT 0,
    PhiPhat        money             NOT NULL DEFAULT 0,
    SoTienHoan     money             NOT NULL DEFAULT 0,
    PhiChenhLech   money             NOT NULL DEFAULT 0,
    TrangThai      nvarchar(30)      NOT NULL DEFAULT N'Đang chờ duyệt'
                   CHECK (TrangThai IN (N'Đang chờ duyệt', N'Đã chấp thuận', N'Bị từ chối', N'Đã hoàn tất')),
    MaNVXuLy       varchar(10)       NULL,
    NgayXuLy       datetime          NULL,
    GhiChu         nvarchar(200)     NULL,
    CONSTRAINT PK_YeuCauHuyDoiVe PRIMARY KEY (MaYeuCau),
    CONSTRAINT FK_YeuCau_VeBay      FOREIGN KEY (MaVe)           REFERENCES VeMayBay(MaVe),
    CONSTRAINT FK_YeuCau_ChuyenMoi  FOREIGN KEY (MaChuyenBayMoi) REFERENCES ChuyenBay(MaChuyenBay),
    CONSTRAINT FK_YeuCau_GheMoi     FOREIGN KEY (MaGheMoi)       REFERENCES GheNgoi(MaGhe),
    CONSTRAINT FK_YeuCau_NV         FOREIGN KEY (MaNVXuLy)       REFERENCES NhanVien(MaNV)
);
GO

-- 21. Đánh Giá Chuyến Bay
CREATE TABLE DanhGiaChuyenBay
(
    MaDanhGia   int IDENTITY(1,1) NOT NULL,
    MaKH        varchar(10)       NOT NULL,
    MaChuyenBay varchar(20)       NOT NULL,
    SoSao       int               NOT NULL CHECK (SoSao BETWEEN 1 AND 5),
    NoiDung     nvarchar(500)     NULL,
    NgayDanhGia datetime          NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_DanhGia PRIMARY KEY (MaDanhGia),
    CONSTRAINT FK_DanhGia_KH     FOREIGN KEY (MaKH)        REFERENCES KhachHang(MaKH),
    CONSTRAINT FK_DanhGia_Chuyen FOREIGN KEY (MaChuyenBay) REFERENCES ChuyenBay(MaChuyenBay)
);
GO

-- ====================================================================================================
-- PHẦN 2: USER-DEFINED TABLE TYPE CHO ĐẶT VÉ ĐA CHẶNG / KHỨ HỒI / QUÁ CẢNH
-- ====================================================================================================

CREATE TYPE ChiTietDatVe_Type AS TABLE
(
    HuongBay       varchar(10),
    ThuTuChang     int,
    MaChuyenBay    varchar(20),
    MaGhe          int,
    MaHangGhe      varchar(10),
    TenHanhKhach   nvarchar(60),
    NgaySinh       date,
    CCCD_HanhKhach varchar(30),
    LoaiGiayTo     nvarchar(30),
    SoGiayTo       varchar(30),
    HanHoChieu     date,
    QuocTich       nvarchar(50),
    LoaiHanhKhach  nvarchar(20),
    NguoiLonDiCung nvarchar(60),
    GiaCoBan       money,
    ThuePhi        money,
    GiaVe          money
);
GO

-- ====================================================================================================
-- PHẦN 3: USER-DEFINED FUNCTIONS (TÍNH GIÁ VÉ, THUẾ PHÍ, HOÀN TIỀN, DOANH THU)
-- ====================================================================================================

CREATE FUNCTION fn_TinhGiaVeThucTe
(
    @GiaCoBan  money,
    @MaHangGhe varchar(10)
)
RETURNS money
AS
BEGIN
    DECLARE @HeSo decimal(4,2) = 1.00;
    SELECT @HeSo = ISNULL(HeSoGia, 1.00) FROM HangGhe WHERE MaHangGhe = @MaHangGhe;
    RETURN ROUND(@GiaCoBan * @HeSo, -3);
END;
GO

-- Công thức giá vé chuẩn: (GiaVeCoBan * HeSoGia * HeSoLoaiHK) + ThuePhi
-- Người lớn: 100% + Thuế phí (150k nội địa / 350k quốc tế)
-- Trẻ em (2-11 tuổi): 90% + Thuế phí (150k nội địa / 350k quốc tế)
-- Em bé (<2 tuổi): 10% giá cơ bản, Thuế phí 0đ
CREATE FUNCTION fn_TinhChiTietGiaVe
(
    @GiaCoBan      money,
    @MaHangGhe     varchar(10),
    @LoaiHanhKhach nvarchar(20),
    @LaQuocTe      bit = 0
)
RETURNS @Result TABLE
(
    GiaSauHang   money,
    HeSoLoaiHK   decimal(4,2),
    TienVe       money,
    ThuePhi      money,
    TongCong     money
)
AS
BEGIN
    DECLARE @GiaSauHang money = dbo.fn_TinhGiaVeThucTe(@GiaCoBan, @MaHangGhe);
    DECLARE @HeSoHK decimal(4,2) = CASE
        WHEN @LoaiHanhKhach = N'Trẻ em' THEN 0.90
        WHEN @LoaiHanhKhach = N'Em bé'  THEN 0.10
        ELSE 1.00
    END;
    DECLARE @TienVe money = CASE
        WHEN @LoaiHanhKhach = N'Em bé' THEN CASE WHEN ROUND(@GiaCoBan * 0.10, -3) < 100000 THEN 100000 ELSE ROUND(@GiaCoBan * 0.10, -3) END
        ELSE ROUND(@GiaSauHang * @HeSoHK, -3)
    END;
    DECLARE @ThueBase money = CASE WHEN @LaQuocTe = 1 THEN 550000 ELSE 220000 END;
    DECLARE @ThuePhi money = CASE
        WHEN @LoaiHanhKhach = N'Em bé'  THEN 0
        WHEN @LoaiHanhKhach = N'Trẻ em' THEN ROUND(@ThueBase * 0.50, -3)
        ELSE @ThueBase
    END;
    INSERT INTO @Result VALUES (@GiaSauHang, @HeSoHK, @TienVe, @ThuePhi, @TienVe + @ThuePhi);
    RETURN;
END;
GO

CREATE FUNCTION fn_TinhTienHoanTraHuyVe (@MaVe varchar(25))
RETURNS @KetQua TABLE
(
    GiaVeGoc     money,
    GioConLai    decimal(10,2),
    PhanTramHoan decimal(5,2),
    SoTienHoan   money,
    PhiPhat      money
)
AS
BEGIN
    DECLARE @GiaVe money, @NgayGioBay datetime;
    SELECT @GiaVe = v.GiaVe, @NgayGioBay = cb.NgayGioBay
    FROM VeMayBay v
    JOIN ChuyenBay cb ON v.MaChuyenBay = cb.MaChuyenBay
    WHERE v.MaVe = @MaVe;

    IF @GiaVe IS NOT NULL
    BEGIN
        DECLARE @GioConLai decimal(10,2) = DATEDIFF(MINUTE, GETDATE(), @NgayGioBay) / 60.0;
        DECLARE @PhanTram decimal(5,2) = CASE
            WHEN @GioConLai > 72.0 THEN 80.00
            WHEN @GioConLai > 0    THEN 50.00
            ELSE 0.00
        END;
        DECLARE @TienHoan money = ROUND(@GiaVe * (@PhanTram / 100.0), -3);
        DECLARE @Phat money = @GiaVe - @TienHoan;
        INSERT INTO @KetQua VALUES (@GiaVe, @GioConLai, @PhanTram, @TienHoan, @Phat);
    END
    RETURN;
END;
GO

CREATE FUNCTION fn_DemSoGheTrongTheoHang
(
    @MaChuyenBay varchar(20),
    @MaHangGhe   varchar(10)
)
RETURNS int
AS
BEGIN
    DECLARE @SoGheTrong int = 0;
    DECLARE @MaMayBay varchar(15);
    SELECT @MaMayBay = MaMayBay FROM ChuyenBay WHERE MaChuyenBay = @MaChuyenBay;

    SELECT @SoGheTrong = COUNT(*)
    FROM GheNgoi g
    WHERE g.MaMayBay = @MaMayBay
      AND g.MaHangGhe = @MaHangGhe
      AND g.TrangThai = 1
      AND NOT EXISTS (
          SELECT 1 FROM VeMayBay v
          WHERE v.MaChuyenBay = @MaChuyenBay
            AND v.MaGhe = g.MaGhe
            AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
      );
    RETURN ISNULL(@SoGheTrong, 0);
END;
GO

CREATE FUNCTION fn_DoanhThuThangNam (@Thang int, @Nam int)
RETURNS money
AS
BEGIN
    DECLARE @TongDoanhThu money = 0;
    SELECT @TongDoanhThu = SUM(hd.TongTien)
    FROM HoaDon hd
    WHERE MONTH(hd.NgayThanhToan) = @Thang
      AND YEAR(hd.NgayThanhToan) = @Nam
      AND hd.TrangThai = N'Đã thanh toán';
    RETURN ISNULL(@TongDoanhThu, 0);
END;
GO

-- ====================================================================================================
-- PHẦN 4: TRIGGERS ĐỒNG BỘ GHẾ TRỐNG & TỔNG TIỀN PNR
-- ====================================================================================================

CREATE TRIGGER trg_VeMayBay_AfterInsert
ON VeMayBay AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN VeMayBay v ON i.MaChuyenBay = v.MaChuyenBay AND i.MaGhe = v.MaGhe
        WHERE i.MaGhe IS NOT NULL
          AND v.MaVe <> i.MaVe
          AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
    )
    BEGIN
        RAISERROR(N'Ghế vừa có hành khách khác đặt hoặc giữ chỗ. Vui lòng chọn ghế khác!', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    UPDATE cb
    SET cb.SoGheTrong = CASE WHEN cb.SoGheTrong - cnt.SoLuong < 0 THEN 0 ELSE cb.SoGheTrong - cnt.SoLuong END
    FROM ChuyenBay cb
    JOIN (
        SELECT MaChuyenBay, COUNT(*) AS SoLuong
        FROM inserted
        WHERE MaGhe IS NOT NULL
          AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        GROUP BY MaChuyenBay
    ) cnt ON cb.MaChuyenBay = cnt.MaChuyenBay;

    UPDATE ChuyenBay
    SET TrangThai = N'Hết vé'
    WHERE SoGheTrong <= 0
      AND TrangThai = N'Đang mở bán'
      AND MaChuyenBay IN (SELECT DISTINCT MaChuyenBay FROM inserted);
END;
GO

CREATE TRIGGER trg_VeMayBay_AfterUpdate
ON VeMayBay AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Hoàn ghế trống khi vé chuyển từ trạng thái giữ/xuất/check-in sang Đã hủy hoặc Đã đổi
    IF EXISTS (
        SELECT 1 FROM inserted i
        JOIN deleted d ON i.MaVe = d.MaVe
        WHERE i.MaGhe IS NOT NULL
          AND d.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
          AND i.TrangThai IN (N'Đã hủy', N'Đã đổi')
    )
    BEGIN
        UPDATE cb
        SET cb.SoGheTrong = CASE WHEN cb.SoGheTrong + huy.SoLuong > mb.TongSoGhe THEN mb.TongSoGhe ELSE cb.SoGheTrong + huy.SoLuong END,
            cb.TrangThai = CASE WHEN cb.TrangThai = N'Hết vé' AND cb.NgayGioBay > GETDATE() THEN N'Đang mở bán' ELSE cb.TrangThai END
        FROM ChuyenBay cb
        JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
        JOIN (
            SELECT i.MaChuyenBay, COUNT(*) AS SoLuong
            FROM inserted i
            JOIN deleted d ON i.MaVe = d.MaVe
            WHERE i.MaGhe IS NOT NULL
              AND d.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
              AND i.TrangThai IN (N'Đã hủy', N'Đã đổi')
            GROUP BY i.MaChuyenBay
        ) huy ON cb.MaChuyenBay = huy.MaChuyenBay;
    END
END;
GO

CREATE TRIGGER trg_CapNhatTongTienPhieuDatCho
ON VeMayBay AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @BangMaDatCho TABLE (MaDatCho varchar(15) PRIMARY KEY);
    INSERT INTO @BangMaDatCho (MaDatCho)
    SELECT MaDatCho FROM inserted
    UNION
    SELECT MaDatCho FROM deleted;

    UPDATE p
    SET p.TongTienGoc = ISNULL((
            SELECT SUM(v.GiaVe) FROM VeMayBay v
            WHERE v.MaDatCho = p.MaDatCho AND v.TrangThai <> N'Đã hủy'
        ), 0)
    FROM PhieuDatCho p
    JOIN @BangMaDatCho b ON p.MaDatCho = b.MaDatCho;

    UPDATE p
    SET p.TongTienSauGiam = CASE
            WHEN p.TongTienGoc - p.TienGiam < 0 THEN 0
            ELSE p.TongTienGoc - p.TienGiam
        END
    FROM PhieuDatCho p
    JOIN @BangMaDatCho b ON p.MaDatCho = b.MaDatCho;
END;
GO

-- ====================================================================================================
-- PHẦN 5: VIEWS BÁO CÁO & TRA CỨU
-- ====================================================================================================

CREATE VIEW vw_DanhSachChuyenBayChiTiet AS
SELECT
    cb.MaChuyenBay,
    hhk.MaHang,
    hhk.TenHang AS TenHangHangKhong,
    hhk.Logo,
    mb.MaMayBay,
    mb.TenMayBay,
    tb.MaTuyenBay,
    sbDi.MaSanBay  AS MaSanBayDi,
    sbDi.TenSanBay AS TenSanBayDi,
    sbDi.ThanhPho  AS ThanhPhoDi,
    sbDi.QuocGia   AS QuocGiaDi,
    sbDen.MaSanBay  AS MaSanBayDen,
    sbDen.TenSanBay AS TenSanBayDen,
    sbDen.ThanhPho  AS ThanhPhoDen,
    sbDen.QuocGia   AS QuocGiaDen,
    CAST(CASE WHEN sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam' THEN 1 ELSE 0 END AS bit) AS LaTuyenQuocTe,
    sbDen.HinhAnhUrl AS HinhAnhDiemDen,
    ISNULL(sbDen.HinhAnhBannerUrl, sbDen.HinhAnhUrl) AS HinhAnhBannerUrl,
    sbDen.MoTaDiemDen,
    cb.NgayGioBay,
    cb.NgayGioDenDK,
    cb.ThoiGianBay,
    cb.GiaVeCoBan,
    CAST(cb.GiaVeCoBan + CASE WHEN sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam' THEN 550000 ELSE 220000 END AS money) AS GiaHienThiTu,
    cb.SoGheTrong,
    mb.TongSoGhe,
    cb.TrangThai AS TrangThaiChuyenBay,
    CAST(CASE WHEN EXISTS (SELECT 1 FROM ChiTietChuyenBay ct WHERE ct.MaChuyenBay = cb.MaChuyenBay) THEN 1 ELSE 0 END AS int) AS LaChuyenQuaCanh,
    ISNULL((
        SELECT TOP 1 sbTG.ThanhPho + N' (' + sbTG.MaSanBay + N' - dừng ' + CAST(ct.ThoiGianDung AS nvarchar(10)) + N' phút)'
        FROM ChiTietChuyenBay ct
        JOIN SanBay sbTG ON ct.MaSanBayTG = sbTG.MaSanBay
        WHERE ct.MaChuyenBay = cb.MaChuyenBay
        ORDER BY ct.ThuTuDung ASC
    ), N'Bay thẳng') AS ThongTinQuaCanh
FROM ChuyenBay cb
JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
JOIN HangHangKhong hhk ON mb.MaHang = hhk.MaHang;
GO

CREATE VIEW vw_LichSuDatVeKhachHang AS
SELECT
    pdc.MaDatCho,
    pdc.NgayDat,
    pdc.LoaiHanhTrinh,
    pdc.MaDatChoLienKet,
    pdc.LaHanhTrinhQuaCanh,
    pdc.NguoiLienHe,
    pdc.SDTLienHe,
    pdc.EmailLienHe,
    kh.MaKH,
    kh.TenKH,
    kh.SDT,
    kh.Email,
    pdc.TongTienGoc,
    pdc.TienGiam,
    pdc.TongTienSauGiam,
    pdc.HanThanhToan,
    CASE WHEN pdc.HanThanhToan > GETDATE() THEN DATEDIFF(SECOND, GETDATE(), pdc.HanThanhToan) ELSE 0 END AS RemainingSeconds,
    pdc.TrangThai AS TrangThaiDonHang,
    hd.MaHD,
    hd.PhuongThuc AS PhuongThucThanhToan,
    hd.NgayThanhToan,
    hd.MaGiaoDichNganHang,
    ISNULL(hd.DaXuatHoaDon, 0) AS DaXuatHoaDon,
    ISNULL((
        SELECT SUM(dv.ThanhTien)
        FROM ChiTietDichVuDatVe dv
        WHERE dv.MaDatCho = pdc.MaDatCho AND dv.TrangThaiThanhToan = N'Đã thanh toán'
    ), 0) AS TienDichVuThem,
    CASE
        WHEN hd.NgayThanhToan IS NOT NULL AND (
            DATEDIFF(MINUTE, hd.NgayThanhToan, GETDATE()) <= 4320
            OR EXISTS (
                SELECT 1 FROM ChiTietDichVuDatVe dv2
                WHERE dv2.MaDatCho = pdc.MaDatCho
                  AND dv2.TrangThaiThanhToan = N'Đã thanh toán'
                  AND dv2.NgayThanhToan IS NOT NULL
                  AND DATEDIFF(MINUTE, dv2.NgayThanhToan, GETDATE()) <= 4320
            )
        ) THEN 1 ELSE 0
    END AS DuocXuatHoaDon72h
FROM PhieuDatCho pdc
JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
LEFT JOIN HoaDon hd ON pdc.MaDatCho = hd.MaDatCho;
GO

CREATE VIEW vw_ChiTietVeVaHanhKhach AS
SELECT
    v.MaVe,
    v.MaDatCho,
    v.HuongBay,
    v.ThuTuChang,
    v.TenHanhKhach,
    v.NgaySinh,
    v.CCCD_HanhKhach,
    v.LoaiGiayTo,
    v.SoGiayTo,
    v.HanHoChieu,
    v.QuocTich,
    v.LoaiHanhKhach,
    v.NguoiLonDiCung,
    v.GiaCoBan,
    v.ThuePhi,
    v.PhiChonGhe,
    v.GiaVe,
    v.TrangThai AS TrangThaiVe,
    hg.MaHangGhe,
    hg.TenHangGhe,
    ISNULL(gn.MaGhe, 0) AS MaGhe,
    ISNULL(gn.SoGhe, 'INF') AS SoGhe,
    cb.MaChuyenBay,
    hhk.MaHang,
    hhk.TenHang AS HangBay,
    sbDi.MaSanBay   AS MaSanBayDi,
    sbDi.TenSanBay  AS SanBayDi,
    sbDi.ThanhPho   AS ThanhPhoDi,
    sbDi.QuocGia    AS QuocGiaDi,
    sbDen.MaSanBay  AS MaSanBayDen,
    sbDen.TenSanBay AS SanBayDen,
    sbDen.ThanhPho  AS ThanhPhoDen,
    sbDen.QuocGia   AS QuocGiaDen,
    sbDen.HinhAnhUrl AS HinhAnhDiemDen,
    cb.NgayGioBay,
    cb.NgayGioDenDK
FROM VeMayBay v
JOIN HangGhe hg ON v.MaHangGhe = hg.MaHangGhe
LEFT JOIN GheNgoi gn ON v.MaGhe = gn.MaGhe
JOIN ChuyenBay cb ON v.MaChuyenBay = cb.MaChuyenBay
JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
JOIN HangHangKhong hhk ON mb.MaHang = hhk.MaHang
JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay;
GO

CREATE VIEW vw_BaoCaoDoanhThuTheoHang AS
SELECT
    hhk.MaHang,
    hhk.TenHang,
    COUNT(DISTINCT v.MaVe) AS TongSoVeDaBan,
    ISNULL(SUM(v.GiaVe), 0) AS TongDoanhThuVe
FROM HangHangKhong hhk
LEFT JOIN MayBay mb ON hhk.MaHang = mb.MaHang
LEFT JOIN ChuyenBay cb ON mb.MaMayBay = cb.MaMayBay
LEFT JOIN VeMayBay v ON cb.MaChuyenBay = v.MaChuyenBay AND v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
GROUP BY hhk.MaHang, hhk.TenHang;
GO

CREATE VIEW vw_TopTuyenBayDatNhieuNhat AS
SELECT TOP 10
    tb.MaTuyenBay,
    sbDi.ThanhPho + N' -> ' + sbDen.ThanhPho AS ChangBay,
    COUNT(v.MaVe) AS SoLuongVeBan,
    SUM(v.GiaVe)  AS TongDoanhThu
FROM TuyenBay tb
JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
JOIN ChuyenBay cb ON tb.MaTuyenBay = cb.MaTuyenBay
JOIN VeMayBay v ON cb.MaChuyenBay = v.MaChuyenBay
WHERE v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')
GROUP BY tb.MaTuyenBay, sbDi.ThanhPho, sbDen.ThanhPho
ORDER BY SoLuongVeBan DESC;
GO

-- ====================================================================================================
-- PHẦN 6: STORED PROCEDURES NGHIỆP VỤ & CONCURRENCY
-- ====================================================================================================

-- 1. Tìm kiếm chuyến bay thẳng đa tiêu chí (đóng bán trước 60 phút)
CREATE PROCEDURE sp_TimKiemChuyenBay
    @MaSanBayDi  varchar(10)  = NULL,
    @MaSanBayDen varchar(10)  = NULL,
    @NgayBay     date         = NULL,
    @MaHangGhe   varchar(10)  = NULL,
    @GiaMax      money        = NULL,
    @MaHang      varchar(10)  = NULL,
    @SoHanhKhach int          = 1,
    @CheDoBay    varchar(20)  = 'ALL'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM vw_DanhSachChuyenBayChiTiet v
    WHERE v.TrangThaiChuyenBay = N'Đang mở bán'
      AND v.SoGheTrong >= @SoHanhKhach
      AND v.NgayGioBay > DATEADD(MINUTE, 60, GETDATE())
      AND (@MaSanBayDi IS NULL OR @MaSanBayDi = '' OR v.MaSanBayDi = @MaSanBayDi)
      AND (@MaSanBayDen IS NULL OR @MaSanBayDen = '' OR v.MaSanBayDen = @MaSanBayDen)
      AND (@NgayBay IS NULL OR CAST(v.NgayGioBay AS date) = @NgayBay)
      AND (@GiaMax IS NULL OR v.GiaVeCoBan <= @GiaMax)
      AND (@MaHang IS NULL OR @MaHang = '' OR v.MaHang = @MaHang)
      AND (@MaHangGhe IS NULL OR @MaHangGhe = '' OR dbo.fn_DemSoGheTrongTheoHang(v.MaChuyenBay, @MaHangGhe) >= @SoHanhKhach)
      AND (@CheDoBay = 'ALL' OR (@CheDoBay = 'THANG' AND v.LaChuyenQuaCanh = 0) OR (@CheDoBay = 'QUA_CANH' AND v.LaChuyenQuaCanh = 1))
    ORDER BY v.NgayGioBay ASC, v.GiaVeCoBan ASC;
END;
GO

-- 2. Tìm chuyến bay NỐI CHUYẾN / QUÁ CẢNH ghép 2 chặng thật (60p nội địa / 120p quốc tế -> 8 tiếng, tổng <= 24h)
CREATE PROCEDURE sp_TimChuyenQuaCanh
    @MaSanBayDi  varchar(10),
    @MaSanBayDen varchar(10),
    @NgayBay     date,
    @SoHanhKhach int = 1,
    @MaHang      varchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 30
        cb1.MaChuyenBay AS Chuyen1_Ma,
        hhk1.MaHang     AS Chuyen1_MaHang,
        hhk1.TenHang    AS Chuyen1_Hang,
        cb1.NgayGioBay  AS Chuyen1_GioDi,
        cb1.NgayGioDenDK AS Chuyen1_GioDen,
        cb1.ThoiGianBay AS Chuyen1_ThoiGianBay,
        cb1.GiaVeCoBan  AS Chuyen1_GiaCoBan,
        cb1.SoGheTrong  AS Chuyen1_SoGheTrong,

        sbHub.MaSanBay  AS Hub_MaSanBay,
        sbHub.TenSanBay AS Hub_TenSanBay,
        sbHub.ThanhPho  AS Hub_ThanhPho,
        DATEDIFF(MINUTE, cb1.NgayGioDenDK, cb2.NgayGioBay) AS ThoiGianNoiChuyenPhut,

        cb2.MaChuyenBay AS Chuyen2_Ma,
        hhk2.MaHang     AS Chuyen2_MaHang,
        hhk2.TenHang    AS Chuyen2_Hang,
        cb2.NgayGioBay  AS Chuyen2_GioDi,
        cb2.NgayGioDenDK AS Chuyen2_GioDen,
        cb2.ThoiGianBay AS Chuyen2_ThoiGianBay,
        cb2.GiaVeCoBan  AS Chuyen2_GiaCoBan,
        cb2.SoGheTrong  AS Chuyen2_SoGheTrong,

        sbDi.MaSanBay   AS MaSanBayDi,
        sbDi.ThanhPho   AS ThanhPhoDi,
        sbDi.QuocGia    AS QuocGiaDi,
        sbDen.MaSanBay  AS MaSanBayDen,
        sbDen.ThanhPho  AS ThanhPhoDen,
        sbDen.QuocGia   AS QuocGiaDen,
        sbDen.HinhAnhUrl AS HinhAnhDiemDen,
        CAST(CASE WHEN sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam' OR sbHub.QuocGia <> N'Việt Nam' THEN 1 ELSE 0 END AS bit) AS LaTuyenQuocTe,
        DATEDIFF(MINUTE, cb1.NgayGioBay, cb2.NgayGioDenDK) AS TongThoiGianHanhTrinhPhut,
        (cb1.GiaVeCoBan + cb2.GiaVeCoBan) AS TongGiaVeCoBan,
        CAST((cb1.GiaVeCoBan + cb2.GiaVeCoBan) + CASE WHEN sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam' THEN 700000 ELSE 300000 END AS money) AS TongGiaHienThiTu
    FROM ChuyenBay cb1
    JOIN TuyenBay tb1     ON cb1.MaTuyenBay = tb1.MaTuyenBay
    JOIN SanBay sbDi      ON tb1.MaSanBayDi = sbDi.MaSanBay
    JOIN SanBay sbHub     ON tb1.MaSanBayDen = sbHub.MaSanBay
    JOIN MayBay mb1       ON cb1.MaMayBay = mb1.MaMayBay
    JOIN HangHangKhong hhk1 ON mb1.MaHang = hhk1.MaHang

    JOIN TuyenBay tb2     ON tb2.MaSanBayDi = sbHub.MaSanBay AND tb2.MaSanBayDen = @MaSanBayDen
    JOIN ChuyenBay cb2    ON cb2.MaTuyenBay = tb2.MaTuyenBay
    JOIN SanBay sbDen     ON tb2.MaSanBayDen = sbDen.MaSanBay
    JOIN MayBay mb2       ON cb2.MaMayBay = mb2.MaMayBay
    JOIN HangHangKhong hhk2 ON mb2.MaHang = hhk2.MaHang
    WHERE tb1.MaSanBayDi = @MaSanBayDi
      AND sbHub.MaSanBay <> @MaSanBayDi
      AND sbHub.MaSanBay <> @MaSanBayDen
      AND CAST(cb1.NgayGioBay AS date) = @NgayBay
      AND cb1.NgayGioBay > DATEADD(MINUTE, 60, GETDATE())
      AND cb1.TrangThai = N'Đang mở bán'
      AND cb2.TrangThai = N'Đang mở bán'
      AND cb1.SoGheTrong >= @SoHanhKhach
      AND cb2.SoGheTrong >= @SoHanhKhach
      AND (@MaHang IS NULL OR @MaHang = '' OR hhk1.MaHang = @MaHang OR hhk2.MaHang = @MaHang)
      -- Thời gian nối chuyến tối thiểu 60p nội địa / 120p nếu có yếu tố quốc tế, tối đa 8 giờ (480p)
      AND DATEDIFF(MINUTE, cb1.NgayGioDenDK, cb2.NgayGioBay) BETWEEN
          CASE WHEN (sbDi.QuocGia <> N'Việt Nam' OR sbHub.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam') THEN 120 ELSE 60 END
          AND 480
      -- Tổng hành trình <= 24 tiếng
      AND DATEDIFF(MINUTE, cb1.NgayGioBay, cb2.NgayGioDenDK) <= 1440
    ORDER BY (cb1.GiaVeCoBan + cb2.GiaVeCoBan) ASC, TongThoiGianHanhTrinhPhut ASC;
END;
GO

-- 3. Lịch giá vé thấp nhất theo ngày trong tháng (Calendar Strip API)
CREATE PROCEDURE sp_GiaThapNhatTheoNgay
    @MaSanBayDi  varchar(10),
    @MaSanBayDen varchar(10),
    @Thang       int,
    @Nam         int,
    @SoNguoi     int = 1
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NgayDau date = DATEFROMPARTS(@Nam, @Thang, 1);
    DECLARE @NgayCuoi date = EOMONTH(@NgayDau);

    ;WITH DaysInMonth AS (
        SELECT @NgayDau AS Ngay
        UNION ALL
        SELECT DATEADD(DAY, 1, Ngay) FROM DaysInMonth WHERE Ngay < @NgayCuoi
    ),
    DirectMin AS (
        SELECT CAST(cb.NgayGioBay AS date) AS Ngay,
               MIN(cb.GiaVeCoBan) AS GiaMin,
               COUNT(*) AS SoChuyen
        FROM ChuyenBay cb
        JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
        WHERE tb.MaSanBayDi = @MaSanBayDi
          AND tb.MaSanBayDen = @MaSanBayDen
          AND cb.NgayGioBay >= @NgayDau
          AND cb.NgayGioBay < DATEADD(DAY, 1, @NgayCuoi)
          AND cb.NgayGioBay > DATEADD(MINUTE, 60, GETDATE())
          AND cb.TrangThai = N'Đang mở bán'
          AND cb.SoGheTrong >= @SoNguoi
        GROUP BY CAST(cb.NgayGioBay AS date)
    ),
    GlobalMin AS (
        SELECT MIN(GiaMin) AS BestPrice FROM DirectMin
    )
    SELECT
        d.Ngay,
        DAY(d.Ngay) AS NgayTrongThang,
        ISNULL(dm.SoChuyen, 0) AS SoChuyenBay,
        dm.GiaMin AS GiaThapNhat,
        CASE WHEN dm.GiaMin IS NOT NULL AND dm.GiaMin = gm.BestPrice THEN 1 ELSE 0 END AS LaNgayReNhat
    FROM DaysInMonth d
    LEFT JOIN DirectMin dm ON d.Ngay = dm.Ngay
    CROSS JOIN GlobalMin gm
    ORDER BY d.Ngay ASC
    OPTION (MAXRECURSION 35);
END;
GO

-- 4. Đặt vé Atomic 1 PNR (Hỗ trợ Một chiều, Khứ hồi, Nối chuyến, Khóa ghế 15p & Chống trùng giấy tờ)
CREATE PROCEDURE sp_DatVeMayBay_Batch
    @MaDatCho           varchar(15),
    @MaKH               varchar(10),
    @LoaiHanhTrinh      varchar(20)  = 'MOT_CHIEU',
    @MaKM               varchar(20)  = NULL,
    @NguoiLienHe        nvarchar(60) = NULL,
    @SDTLienHe          varchar(20)  = NULL,
    @EmailLienHe        varchar(100) = NULL,
    @GhiChu             nvarchar(255) = NULL,
    @DSVe               ChiTietDatVe_Type READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM @DSVe)
        BEGIN
            RAISERROR(N'Danh sách hành khách/vé đặt không được để trống!', 16, 1);
        END

        -- Khóa các chuyến bay liên quan theo thứ tự MaChuyenBay để tránh deadlock khi đặt khứ hồi/nối chuyến
        DECLARE @ChuyenKeys TABLE (MaChuyenBay varchar(20) PRIMARY KEY);
        INSERT INTO @ChuyenKeys SELECT DISTINCT MaChuyenBay FROM @DSVe;

        DECLARE @LockDummy int;
        SELECT @LockDummy = COUNT(*)
        FROM ChuyenBay cb WITH (UPDLOCK, HOLDLOCK)
        JOIN @ChuyenKeys ck ON cb.MaChuyenBay = ck.MaChuyenBay;

        -- 1. Kiểm tra chuyến bay hợp lệ và chưa đóng cửa (trước 60 phút)
        IF EXISTS (
            SELECT 1 FROM @ChuyenKeys ck
            LEFT JOIN ChuyenBay cb ON ck.MaChuyenBay = cb.MaChuyenBay
            WHERE cb.MaChuyenBay IS NULL
               OR cb.TrangThai <> N'Đang mở bán'
               OR cb.NgayGioBay <= DATEADD(MINUTE, 60, GETDATE())
        )
        BEGIN
            RAISERROR(N'Chuyến bay đã đóng bán (trước giờ bay 60 phút) hoặc không còn mở bán!', 16, 1);
        END

        -- 2. Kiểm tra đủ số ghế trống cho từng chặng (không tính Em bé ngồi cùng người lớn)
        IF EXISTS (
            SELECT 1
            FROM (SELECT MaChuyenBay, COUNT(*) AS SoGheCan FROM @DSVe WHERE MaGhe IS NOT NULL GROUP BY MaChuyenBay) req
            JOIN ChuyenBay cb ON req.MaChuyenBay = cb.MaChuyenBay
            WHERE cb.SoGheTrong < req.SoGheCan
        )
        BEGIN
            RAISERROR(N'Số ghế còn trống trên một chặng không đủ cho số lượng hành khách!', 16, 1);
        END

        -- 3. Kiểm tra tranh chấp ghế đồng thời bằng UPDLOCK, HOLDLOCK
        IF EXISTS (
            SELECT 1 FROM @DSVe ds
            JOIN VeMayBay v WITH (UPDLOCK, HOLDLOCK)
              ON v.MaChuyenBay = ds.MaChuyenBay AND v.MaGhe = ds.MaGhe
            WHERE ds.MaGhe IS NOT NULL
              AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        )
        BEGIN
            RAISERROR(N'Ghế bạn chọn vừa có hành khách khác giữ chỗ hoặc đặt trước! Vui lòng chọn ghế khác.', 16, 1);
        END

        -- 4. Kiểm tra trùng số giấy tờ hành khách trên cùng chuyến bay (không tính em bé dùng chung khai sinh/khác)
        IF EXISTS (
            SELECT 1 FROM @DSVe ds
            JOIN VeMayBay v WITH (HOLDLOCK)
              ON v.MaChuyenBay = ds.MaChuyenBay AND v.SoGiayTo = ds.SoGiayTo
            WHERE ds.SoGiayTo IS NOT NULL AND ds.SoGiayTo <> ''
              AND ds.LoaiHanhKhach <> N'Em bé'
              AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        )
        BEGIN
            RAISERROR(N'Số CCCD/Hộ chiếu của hành khách đã có vé hiệu lực trên cùng chuyến bay này!', 16, 1);
        END

        -- 5. Kiểm tra hạng mức hộ chiếu 6 tháng cho chặng có sân bay quốc tế
        IF EXISTS (
            SELECT 1
            FROM @DSVe ds
            JOIN ChuyenBay cb ON ds.MaChuyenBay = cb.MaChuyenBay
            JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
            JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
            JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
            WHERE (sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam')
              AND (ds.LoaiGiayTo <> N'Hộ chiếu' OR ds.HanHoChieu IS NULL OR ds.HanHoChieu < DATEADD(MONTH, 6, CAST(cb.NgayGioBay AS date)))
        )
        BEGIN
            RAISERROR(N'Chặng bay quốc tế yêu cầu Hộ chiếu còn hiệu lực tối thiểu 6 tháng kể từ ngày bay!', 16, 1);
        END

        -- 6. Kiểm tra logic khứ hồi: nếu có VE thì giờ đi chặng về >= giờ hạ cánh chặng đi cuối + 2 giờ
        DECLARE @MaxArrivalOut datetime, @MinDepartRet datetime;
        SELECT @MaxArrivalOut = MAX(cb.NgayGioDenDK)
        FROM @DSVe ds JOIN ChuyenBay cb ON ds.MaChuyenBay = cb.MaChuyenBay
        WHERE ds.HuongBay = 'DI';

        SELECT @MinDepartRet = MIN(cb.NgayGioBay)
        FROM @DSVe ds JOIN ChuyenBay cb ON ds.MaChuyenBay = cb.MaChuyenBay
        WHERE ds.HuongBay = 'VE';

        IF @MaxArrivalOut IS NOT NULL AND @MinDepartRet IS NOT NULL AND @MinDepartRet < DATEADD(HOUR, 2, @MaxArrivalOut)
        BEGIN
            RAISERROR(N'Chuyến lượt về phải khởi hành sau giờ hạ cánh lượt đi tối thiểu 2 tiếng!', 16, 1);
        END

        -- 7. Tính tổng tiền vé & áp dụng Khuyến mãi (chỉ giảm trên giá vé, không giảm dịch vụ thêm)
        DECLARE @TongTienGoc money = 0;
        SELECT @TongTienGoc = ISNULL(SUM(GiaVe), 0) FROM @DSVe;

        DECLARE @TienGiam money = 0;
        IF @MaKM IS NOT NULL AND LTRIM(RTRIM(@MaKM)) <> ''
        BEGIN
            SET @MaKM = UPPER(LTRIM(RTRIM(@MaKM)));
            DECLARE @PhanTram decimal(5,2), @GiamMax money, @ToiThieu money, @ConLai int;
            SELECT @PhanTram = PhanTramGiam, @GiamMax = GiamToiDa, @ToiThieu = DonHangToiThieu, @ConLai = SoLuong
            FROM KhuyenMai WITH (UPDLOCK)
            WHERE MaKM = @MaKM AND TrangThai = 1 AND CAST(GETDATE() AS date) BETWEEN NgayBatDau AND NgayKetThuc;

            IF @ConLai IS NOT NULL AND @ConLai > 0 AND @TongTienGoc >= @ToiThieu
            BEGIN
                SET @TienGiam = ROUND(@TongTienGoc * (@PhanTram / 100.0), -3);
                IF @GiamMax > 0 AND @TienGiam > @GiamMax SET @TienGiam = @GiamMax;
                UPDATE KhuyenMai SET SoLuong = SoLuong - 1 WHERE MaKM = @MaKM;
            END
            ELSE
            BEGIN
                SET @MaKM = NULL;
            END
        END
        ELSE
        BEGIN
            SET @MaKM = NULL;
        END

        DECLARE @TongSauGiam money = CASE WHEN @TongTienGoc - @TienGiam < 0 THEN 0 ELSE @TongTienGoc - @TienGiam END;

        -- 8. Thời hạn giữ chỗ đúng 15 phút (hoặc trước giờ bay 60p nếu bay cận giờ)
        DECLARE @MinFlightTime datetime = (SELECT MIN(cb.NgayGioBay) FROM @ChuyenKeys ck JOIN ChuyenBay cb ON ck.MaChuyenBay = cb.MaChuyenBay);
        DECLARE @HanTT datetime = DATEADD(MINUTE, 15, GETDATE());
        IF @HanTT > DATEADD(MINUTE, -60, @MinFlightTime)
            SET @HanTT = DATEADD(MINUTE, -60, @MinFlightTime);

        DECLARE @LaQuaCanh bit = CASE WHEN EXISTS (SELECT 1 FROM @DSVe WHERE ThuTuChang > 1) THEN 1 ELSE 0 END;

        INSERT INTO PhieuDatCho
        (
            MaDatCho, MaKH, NgayDat, LoaiHanhTrinh, MaDatChoLienKet, LaHanhTrinhQuaCanh,
            NguoiLienHe, SDTLienHe, EmailLienHe, TongTienGoc, MaKM, TienGiam, TongTienSauGiam,
            HanThanhToan, TrangThai, GhiChu
        )
        VALUES
        (
            @MaDatCho, @MaKH, GETDATE(), @LoaiHanhTrinh, NULL, @LaQuaCanh,
            @NguoiLienHe, @SDTLienHe, @EmailLienHe, @TongTienGoc, @MaKM, @TienGiam, @TongSauGiam,
            @HanTT, N'Chờ thanh toán', @GhiChu
        );

        -- Lưu các chặng bay trong hành trình của PNR
        INSERT INTO ChangBayDatCho (MaDatCho, HuongBay, ThuTuChang, MaChuyenBay, LaQuaCanh)
        SELECT DISTINCT @MaDatCho, HuongBay, ThuTuChang, MaChuyenBay, CASE WHEN ThuTuChang > 1 THEN 1 ELSE 0 END
        FROM @DSVe;

        -- Chèn toàn bộ vé máy bay thuộc PNR
        INSERT INTO VeMayBay
        (
            MaVe, MaDatCho, HuongBay, ThuTuChang, MaChuyenBay, MaGhe, MaHangGhe,
            TenHanhKhach, NgaySinh, CCCD_HanhKhach, LoaiGiayTo, SoGiayTo, HanHoChieu, QuocTich,
            LoaiHanhKhach, NguoiLonDiCung, GiaCoBan, ThuePhi, PhiChonGhe, GiaVe, TrangThai
        )
        SELECT
            @MaDatCho + '-' + RIGHT('00' + CAST(ROW_NUMBER() OVER (ORDER BY HuongBay, ThuTuChang, LoaiHanhKhach, TenHanhKhach) AS varchar(4)), 2),
            @MaDatCho, HuongBay, ThuTuChang, MaChuyenBay, MaGhe, MaHangGhe,
            TenHanhKhach, NgaySinh, ISNULL(NULLIF(CCCD_HanhKhach, ''), SoGiayTo), LoaiGiayTo, SoGiayTo, HanHoChieu, ISNULL(QuocTich, N'Việt Nam'),
            LoaiHanhKhach, NguoiLonDiCung, GiaCoBan, ThuePhi, 0, GiaVe, N'Đã đặt chỗ'
        FROM @DSVe;

        COMMIT TRANSACTION;
        SELECT @MaDatCho AS MaDatCho, @TongSauGiam AS TongTienThanhToan, @HanTT AS HanThanhToan,
               DATEDIFF(SECOND, GETDATE(), @HanTT) AS RemainingSeconds,
               N'Giữ chỗ thành công trong 15 phút!' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrNo int = ERROR_NUMBER();
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        IF @ErrNo IN (2601, 2627)
            SET @ErrMsg = N'Ghế hoặc số giấy tờ hành khách vừa bị đặt trước trong tích tắc. Vui lòng kiểm tra lại!';
        ELSE IF @ErrNo = 1205
            SET @ErrMsg = N'Hệ thống đang xử lý đồng thời nhiều giao dịch trên cùng chuyến bay (Deadlock). Vui lòng thử lại ngay!';
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 5. Thanh toán đơn vé & Xuất hóa đơn (tương thích ngược)
CREATE PROCEDURE sp_ThanhToanDonVe
    @MaHD           varchar(15),
    @MaDatCho       varchar(15),
    @MaNV           varchar(10)   = NULL,
    @PhuongThuc     nvarchar(50)  = N'VietQR',
    @MaGiaoDich     varchar(100)  = NULL,
    @SuppressResult bit           = 0
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @TongTien money, @TrangThaiDatCho nvarchar(30), @HanTT datetime;
        SELECT @TongTien = TongTienSauGiam, @TrangThaiDatCho = TrangThai, @HanTT = HanThanhToan
        FROM PhieuDatCho WITH (UPDLOCK, HOLDLOCK)
        WHERE MaDatCho = @MaDatCho;

        IF @TongTien IS NULL
            RAISERROR(N'Không tìm thấy thông tin đơn đặt vé!', 16, 1);

        IF @TrangThaiDatCho = N'Đã thanh toán'
        BEGIN
            COMMIT TRANSACTION;
            IF @SuppressResult = 0
                SELECT MaHD AS MaHoaDon, TongTien AS SoTienThanhToan, N'Giao dịch đã được thanh toán trước đó.' AS ThongBao
                FROM HoaDon WHERE MaDatCho = @MaDatCho;
            RETURN;
        END

        IF @TrangThaiDatCho NOT IN (N'Chờ thanh toán', N'Chờ duyệt thanh toán')
            RAISERROR(N'Đơn đặt chỗ đã hết hạn hoặc bị hủy, không thể thanh toán!', 16, 1);

        IF @TrangThaiDatCho = N'Chờ thanh toán' AND @HanTT < GETDATE()
            RAISERROR(N'Đơn giữ chỗ 15 phút đã hết hạn! Vui lòng đặt lại vé mới.', 16, 1);

        DECLARE @TienDV money = ISNULL((
            SELECT SUM(ThanhTien) FROM ChiTietDichVuDatVe
            WHERE MaDatCho = @MaDatCho AND TrangThaiThanhToan = N'Chờ thanh toán'
        ), 0);

        UPDATE ChiTietDichVuDatVe
        SET TrangThaiThanhToan = N'Đã thanh toán',
            PhuongThucTT = @PhuongThuc,
            MaGiaoDich = ISNULL(@MaGiaoDich, @MaHD),
            NgayThanhToan = GETDATE()
        WHERE MaDatCho = @MaDatCho AND TrangThaiThanhToan = N'Chờ thanh toán';

        DECLARE @TongAllDV money = ISNULL((
            SELECT SUM(ThanhTien) FROM ChiTietDichVuDatVe
            WHERE MaDatCho = @MaDatCho AND TrangThaiThanhToan = N'Đã thanh toán'
        ), 0);

        INSERT INTO HoaDon (MaHD, MaDatCho, MaNV, NgayLap, TienVe, TienDichVu, TongTien, PhuongThuc, MaGiaoDichNganHang, NgayThanhToan, TrangThai, GhiChu)
        VALUES (@MaHD, @MaDatCho, @MaNV, GETDATE(), @TongTien, @TongAllDV, @TongTien + @TongAllDV, @PhuongThuc, @MaGiaoDich, GETDATE(), N'Đã thanh toán', N'Thanh toán đơn đặt chỗ ' + @MaDatCho);

        UPDATE PhieuDatCho SET TrangThai = N'Đã thanh toán' WHERE MaDatCho = @MaDatCho;
        UPDATE VeMayBay SET TrangThai = N'Đã xuất vé' WHERE MaDatCho = @MaDatCho AND TrangThai = N'Đã đặt chỗ';

        IF @MaGiaoDich IS NOT NULL AND EXISTS (SELECT 1 FROM GiaoDichThanhToan WHERE MaGiaoDich = @MaGiaoDich)
        BEGIN
            UPDATE GiaoDichThanhToan
            SET TrangThai = N'Thành công', NgayCapNhat = GETDATE()
            WHERE MaGiaoDich = @MaGiaoDich;
        END

        COMMIT TRANSACTION;
        IF @SuppressResult = 0
            SELECT @MaHD AS MaHoaDon, (@TongTien + @TongAllDV) AS SoTienThanhToan, N'Thanh toán hoàn tất, vé đã được xuất điện tử!' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 6. Xác nhận Webhook/Callback thanh toán Idempotent (hỗ trợ cả VE_MAY_BAY và DICH_VU)
CREATE PROCEDURE sp_XacNhanThanhToanGiaoDich
    @MaGiaoDich varchar(100),
    @KetQua     varchar(20), -- SUCCESS | FAILED | CANCELLED
    @MaHDMoi    varchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @MaDatCho varchar(15), @LoaiGD varchar(20), @TrangThaiHienTai nvarchar(30), @PhuongThuc nvarchar(50), @SoTien money, @HanTT datetime;
        SELECT @MaDatCho = MaDatCho, @LoaiGD = LoaiGiaoDich, @TrangThaiHienTai = TrangThai,
               @PhuongThuc = PhuongThuc, @SoTien = SoTien, @HanTT = HanThanhToan
        FROM GiaoDichThanhToan WITH (UPDLOCK, HOLDLOCK)
        WHERE MaGiaoDich = @MaGiaoDich;

        IF @MaDatCho IS NULL
            RAISERROR(N'Không tìm thấy mã giao dịch thanh toán!', 16, 1);

        -- Idempotent: Nếu đã xử lý rồi thì trả về thành công không tạo trùng hóa đơn
        IF @TrangThaiHienTai IN (N'Thành công', N'Đã hủy', N'Thất bại', N'Hết hạn')
        BEGIN
            COMMIT TRANSACTION;
            SELECT @MaDatCho AS MaDatCho, @LoaiGD AS LoaiGiaoDich, @TrangThaiHienTai AS TrangThai, 1 AS IdempotentReplay;
            RETURN;
        END

        IF @KetQua = 'FAILED' OR @KetQua = 'CANCELLED'
        BEGIN
            DECLARE @NewFailState nvarchar(30) = CASE WHEN @KetQua = 'FAILED' THEN N'Thất bại' ELSE N'Đã hủy' END;
            UPDATE GiaoDichThanhToan SET TrangThai = @NewFailState, NgayCapNhat = GETDATE() WHERE MaGiaoDich = @MaGiaoDich;
            IF @LoaiGD = 'DICH_VU'
            BEGIN
                UPDATE ChiTietDichVuDatVe SET TrangThaiThanhToan = N'Đã hủy' WHERE MaGiaoDich = @MaGiaoDich AND TrangThaiThanhToan = N'Chờ thanh toán';
            END
            COMMIT TRANSACTION;
            SELECT @MaDatCho AS MaDatCho, @LoaiGD AS LoaiGiaoDich, @NewFailState AS TrangThai, 0 AS IdempotentReplay;
            RETURN;
        END

        -- Thành công (cho phép duyệt kể cả khi đã chuyển trạng thái Chờ duyệt)
        IF @TrangThaiHienTai = N'Chờ thanh toán' AND @HanTT < GETDATE()
            RAISERROR(N'Giao dịch đã hết hạn thanh toán!', 16, 1);

        IF @LoaiGD = 'VE_MAY_BAY'
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM HoaDon WHERE MaDatCho = @MaDatCho)
            BEGIN
                EXEC sp_ThanhToanDonVe @MaHD = @MaHDMoi, @MaDatCho = @MaDatCho, @MaNV = NULL, @PhuongThuc = @PhuongThuc, @MaGiaoDich = @MaGiaoDich, @SuppressResult = 1;
            END
            UPDATE GiaoDichThanhToan SET TrangThai = N'Thành công', NgayCapNhat = GETDATE() WHERE MaGiaoDich = @MaGiaoDich;
        END
        ELSE IF @LoaiGD = 'DICH_VU'
        BEGIN
            UPDATE ChiTietDichVuDatVe
            SET TrangThaiThanhToan = N'Đã thanh toán',
                PhuongThucTT = @PhuongThuc,
                NgayThanhToan = GETDATE()
            WHERE MaGiaoDich = @MaGiaoDich AND TrangThaiThanhToan = N'Chờ thanh toán';

            UPDATE GiaoDichThanhToan SET TrangThai = N'Thành công', NgayCapNhat = GETDATE() WHERE MaGiaoDich = @MaGiaoDich;

            -- Cộng bổ sung tiền dịch vụ vào HoaDon hiện hữu của PNR
            UPDATE HoaDon
            SET TienDichVu = ISNULL((SELECT SUM(ThanhTien) FROM ChiTietDichVuDatVe WHERE MaDatCho = @MaDatCho AND TrangThaiThanhToan = N'Đã thanh toán'), 0),
                TongTien = TienVe + ISNULL((SELECT SUM(ThanhTien) FROM ChiTietDichVuDatVe WHERE MaDatCho = @MaDatCho AND TrangThaiThanhToan = N'Đã thanh toán'), 0)
            WHERE MaDatCho = @MaDatCho;
        END

        COMMIT TRANSACTION;
        SELECT @MaDatCho AS MaDatCho, @LoaiGD AS LoaiGiaoDich, N'Thành công' AS TrangThai, 0 AS IdempotentReplay;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 6B. Khách hàng gửi ảnh xác nhận chuyển khoản (khóa giữ ghế chờ Admin/Thu ngân duyệt)
CREATE PROCEDURE sp_GuiMinhChungChuyenKhoan
    @MaGiaoDich varchar(100),
    @AnhUrl     nvarchar(300),
    @GhiChu     nvarchar(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @MaDatCho varchar(15), @LoaiGD varchar(20), @TrangThai nvarchar(30), @HanTT datetime;
        SELECT @MaDatCho = MaDatCho, @LoaiGD = LoaiGiaoDich, @TrangThai = TrangThai, @HanTT = HanThanhToan
        FROM GiaoDichThanhToan WITH (UPDLOCK, HOLDLOCK)
        WHERE MaGiaoDich = @MaGiaoDich;

        IF @MaDatCho IS NULL
            RAISERROR(N'Không tìm thấy giao dịch thanh toán!', 16, 1);

        IF @TrangThai = N'Thành công'
            RAISERROR(N'Giao dịch này đã được thanh toán thành công trước đó.', 16, 1);

        IF @TrangThai = N'Chờ thanh toán' AND @HanTT < GETDATE()
            RAISERROR(N'Đơn giữ chỗ đã quá hạn 15 phút. Vui lòng đặt lại chuyến mới.', 16, 1);

        UPDATE GiaoDichThanhToan
        SET PhuongThuc = N'Chuyển khoản ngân hàng (VietQR)',
            TrangThai = N'Chờ duyệt',
            AnhChuyenKhoanUrl = @AnhUrl,
            NgayGuiMinhChung = GETDATE(),
            LyDoTuChoi = NULL,
            GhiChu = ISNULL(@GhiChu, GhiChu),
            NgayCapNhat = GETDATE()
        WHERE MaGiaoDich = @MaGiaoDich;

        IF @LoaiGD = 'VE_MAY_BAY'
        BEGIN
            UPDATE PhieuDatCho
            SET TrangThai = N'Chờ duyệt thanh toán'
            WHERE MaDatCho = @MaDatCho AND TrangThai = N'Chờ thanh toán';
        END

        COMMIT TRANSACTION;
        SELECT @MaDatCho AS MaDatCho, @LoaiGD AS LoaiGiaoDich, N'Chờ duyệt' AS TrangThai;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 6C. Admin / Thu ngân duyệt hoặc từ chối ảnh xác nhận chuyển khoản
CREATE PROCEDURE sp_DuyetThanhToanChuyenKhoan
    @MaGiaoDich  varchar(100),
    @MaNV        varchar(10),
    @ChapNhan    bit,
    @LyDoTuChoi  nvarchar(255) = NULL,
    @MaHDMoi     varchar(15)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @MaDatCho varchar(15), @LoaiGD varchar(20), @TrangThai nvarchar(30);
    SELECT @MaDatCho = MaDatCho, @LoaiGD = LoaiGiaoDich, @TrangThai = TrangThai
    FROM GiaoDichThanhToan
    WHERE MaGiaoDich = @MaGiaoDich;

    IF @MaDatCho IS NULL
        RAISERROR(N'Không tìm thấy giao dịch chuyển khoản!', 16, 1);

    IF @TrangThai NOT IN (N'Chờ duyệt', N'Chờ thanh toán')
        RAISERROR(N'Giao dịch này đã được xử lý trước đó!', 16, 1);

    IF @ChapNhan = 1
    BEGIN
        EXEC sp_XacNhanThanhToanGiaoDich @MaGiaoDich = @MaGiaoDich, @KetQua = 'SUCCESS', @MaHDMoi = @MaHDMoi;
        UPDATE GiaoDichThanhToan
        SET MaNV_Duyet = @MaNV, NgayDuyet = GETDATE(), LyDoTuChoi = NULL, NgayCapNhat = GETDATE()
        WHERE MaGiaoDich = @MaGiaoDich;
    END
    ELSE
    BEGIN
        BEGIN TRANSACTION;
        DECLARE @Reason nvarchar(255) = ISNULL(NULLIF(LTRIM(RTRIM(@LyDoTuChoi)), N''), N'Ảnh xác nhận chưa rõ hoặc chưa nhận được tiền chuyển khoản.');
        UPDATE GiaoDichThanhToan
        SET TrangThai = N'Chờ thanh toán',
            MaNV_Duyet = @MaNV,
            NgayDuyet = GETDATE(),
            LyDoTuChoi = @Reason,
            HanThanhToan = DATEADD(MINUTE, 15, GETDATE()),
            NgayCapNhat = GETDATE()
        WHERE MaGiaoDich = @MaGiaoDich;

        IF @LoaiGD = 'VE_MAY_BAY'
        BEGIN
            UPDATE PhieuDatCho
            SET TrangThai = N'Chờ thanh toán',
                HanThanhToan = DATEADD(MINUTE, 15, GETDATE())
            WHERE MaDatCho = @MaDatCho AND TrangThai = N'Chờ duyệt thanh toán';
        END
        COMMIT TRANSACTION;
        SELECT @MaDatCho AS MaDatCho, @LoaiGD AS LoaiGiaoDich, N'Chờ thanh toán' AS TrangThai, 0 AS IdempotentReplay;
    END
END;
GO

-- 7. Kiểm tra quy định xuất hóa đơn VAT trong vòng 72 giờ kể từ thời điểm thanh toán
CREATE PROCEDURE sp_XuatHoaDonTrong72Gio
    @MaDatCho        varchar(15),
    @LoaiHoaDonVAT   nvarchar(20)  = N'Cá nhân',
    @TenCongTyVAT    nvarchar(150) = NULL,
    @MaSoThueVAT     varchar(20)   = NULL,
    @DiaChiCongTyVAT nvarchar(250) = NULL,
    @EmailNhanHoaDon varchar(100)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NgayThanhToanVe datetime, @TrangThaiHD nvarchar(30);
    SELECT @NgayThanhToanVe = NgayThanhToan, @TrangThaiHD = TrangThai
    FROM HoaDon WHERE MaDatCho = @MaDatCho;

    IF @NgayThanhToanVe IS NULL OR @TrangThaiHD <> N'Đã thanh toán'
        RAISERROR(N'Không thể xuất hóa đơn: Đơn đặt chỗ chưa được thanh toán!', 16, 1);

    DECLARE @LatestAddonPay datetime = (
        SELECT MAX(NgayThanhToan)
        FROM ChiTietDichVuDatVe
        WHERE MaDatCho = @MaDatCho AND TrangThaiThanhToan = N'Đã thanh toán'
    );

    DECLARE @MocThoiGian datetime = CASE
        WHEN @LatestAddonPay IS NOT NULL AND @LatestAddonPay > @NgayThanhToanVe THEN @LatestAddonPay
        ELSE @NgayThanhToanVe
    END;

    IF DATEDIFF(MINUTE, @MocThoiGian, GETDATE()) > 4320
        RAISERROR(N'Không thể xuất hóa đơn: Đã quá 72 giờ kể từ thời điểm thanh toán!', 16, 1);

    UPDATE HoaDon
    SET DaXuatHoaDon = 1,
        NgayYeuCauXuatVAT = ISNULL(NgayYeuCauXuatVAT, GETDATE()),
        LoaiHoaDonVAT = ISNULL(@LoaiHoaDonVAT, N'Cá nhân'),
        TenCongTyVAT = CASE WHEN @LoaiHoaDonVAT = N'Doanh nghiệp' THEN @TenCongTyVAT ELSE TenCongTyVAT END,
        MaSoThueVAT = CASE WHEN @LoaiHoaDonVAT = N'Doanh nghiệp' THEN @MaSoThueVAT ELSE MaSoThueVAT END,
        DiaChiCongTyVAT = CASE WHEN @LoaiHoaDonVAT = N'Doanh nghiệp' THEN @DiaChiCongTyVAT ELSE DiaChiCongTyVAT END,
        EmailNhanHoaDon = ISNULL(@EmailNhanHoaDon, EmailNhanHoaDon)
    WHERE MaDatCho = @MaDatCho;
END;
GO

-- 8. Đổi ghế sau khi đặt (trước Check-in)
CREATE PROCEDURE sp_DoiGheSauDatVe
    @MaVe     varchar(25),
    @MaGheMoi int
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @MaChuyenBay varchar(20), @TrangThaiVe nvarchar(30), @LoaiHK nvarchar(20);
        SELECT @MaChuyenBay = MaChuyenBay, @TrangThaiVe = TrangThai, @LoaiHK = LoaiHanhKhach
        FROM VeMayBay WITH (UPDLOCK, HOLDLOCK) WHERE MaVe = @MaVe;

        IF @MaChuyenBay IS NULL OR @TrangThaiVe <> N'Đã xuất vé'
            RAISERROR(N'Chỉ được đổi ghế khi vé đang ở trạng thái Đã xuất vé (trước khi Check-in)!', 16, 1);

        IF @LoaiHK = N'Em bé'
            RAISERROR(N'Em bé dưới 2 tuổi ngồi cùng người lớn, không chọn ghế riêng.', 16, 1);

        IF EXISTS (
            SELECT 1 FROM VeMayBay WITH (UPDLOCK, HOLDLOCK)
            WHERE MaChuyenBay = @MaChuyenBay AND MaGhe = @MaGheMoi
              AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        )
            RAISERROR(N'Ghế mới đã có hành khách khác chọn!', 16, 1);

        DECLARE @MaHangGheMoi varchar(10);
        SELECT @MaHangGheMoi = MaHangGhe FROM GheNgoi WHERE MaGhe = @MaGheMoi AND TrangThai = 1;
        IF @MaHangGheMoi IS NULL
            RAISERROR(N'Ghế mới không hợp lệ hoặc đang bảo trì!', 16, 1);

        UPDATE VeMayBay SET MaGhe = @MaGheMoi, MaHangGhe = @MaHangGheMoi WHERE MaVe = @MaVe;
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 9. Hủy vé & Hoàn tiền (Luật 72h)
CREATE PROCEDURE sp_XuLyHuyVeVaHoanTien
    @MaVe     varchar(25),
    @LyDo     nvarchar(255),
    @MaNVXuLy varchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @TrangThaiVe nvarchar(30);
        SELECT @TrangThaiVe = TrangThai FROM VeMayBay WITH (UPDLOCK) WHERE MaVe = @MaVe;

        IF @TrangThaiVe IS NULL OR @TrangThaiVe NOT IN (N'Đã đặt chỗ', N'Đã xuất vé')
            RAISERROR(N'Vé không đủ điều kiện để hủy (hoặc đã Check-in / đã hủy)!', 16, 1);

        DECLARE @GiaGoc money, @GioConLai decimal(10,2), @PhanTram decimal(5,2), @TienHoan money, @PhiPhat money;
        SELECT @GiaGoc = GiaVeGoc, @GioConLai = GioConLai, @PhanTram = PhanTramHoan, @TienHoan = SoTienHoan, @PhiPhat = PhiPhat
        FROM fn_TinhTienHoanTraHuyVe(@MaVe);

        UPDATE VeMayBay SET TrangThai = N'Đã hủy' WHERE MaVe = @MaVe;

        INSERT INTO YeuCauHuyDoiVe (MaVe, LoaiYeuCau, NgayYeuCau, LyDo, PhanTramHoan, SoTienGoc, PhiPhat, SoTienHoan, TrangThai, MaNVXuLy, NgayXuLy, GhiChu)
        VALUES (@MaVe, N'Hủy vé', GETDATE(), @LyDo, @PhanTram, @GiaGoc, @PhiPhat, @TienHoan, N'Đã hoàn tất', @MaNVXuLy, GETDATE(),
                N'Trước giờ bay ' + CAST(@GioConLai AS nvarchar(20)) + N' giờ. Hoàn ' + CAST(@PhanTram AS nvarchar(10)) + N'%');

        COMMIT TRANSACTION;
        SELECT @MaVe AS MaVeHuy, @GiaGoc AS GiaVeGoc, @PhanTram AS PhanTramHoan, @TienHoan AS SoTienHoanKhachNhan, @PhiPhat AS PhiPhatHuy;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 10. Đổi vé sang chuyến bay khác (Atomic UPDLOCK)
CREATE PROCEDURE sp_DoiVeMayBay
    @MaVeCu         varchar(25),
    @MaChuyenBayMoi varchar(20),
    @MaGheMoi       int,
    @MaHangGheMoi   varchar(10),
    @MaNVXuLy       varchar(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @GiaVeCu money, @MaDatCho varchar(15), @TenHK nvarchar(60), @NgaySinh date, @CCCD varchar(30),
                @LoaiGT nvarchar(30), @SoGT varchar(30), @HanHC date, @QuocTich nvarchar(50),
                @LoaiHK nvarchar(20), @HuongBay varchar(10), @ThuTuChang int;
        SELECT @GiaVeCu = GiaVe, @MaDatCho = MaDatCho, @TenHK = TenHanhKhach, @NgaySinh = NgaySinh,
               @CCCD = CCCD_HanhKhach, @LoaiGT = LoaiGiayTo, @SoGT = SoGiayTo, @HanHC = HanHoChieu,
               @QuocTich = QuocTich, @LoaiHK = LoaiHanhKhach, @HuongBay = HuongBay, @ThuTuChang = ThuTuChang
        FROM VeMayBay WITH (UPDLOCK, HOLDLOCK)
        WHERE MaVe = @MaVeCu AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé');

        IF @GiaVeCu IS NULL
            RAISERROR(N'Vé cũ không tồn tại hoặc không ở trạng thái hợp lệ để đổi!', 16, 1);

        IF NOT EXISTS (SELECT 1 FROM ChuyenBay WITH (UPDLOCK) WHERE MaChuyenBay = @MaChuyenBayMoi AND TrangThai = N'Đang mở bán' AND NgayGioBay > DATEADD(MINUTE, 60, GETDATE()))
            RAISERROR(N'Chuyến bay mới không mở bán hoặc đã quá giờ đóng cửa!', 16, 1);

        IF EXISTS (
            SELECT 1 FROM VeMayBay WITH (UPDLOCK, HOLDLOCK)
            WHERE MaChuyenBay = @MaChuyenBayMoi AND MaGhe = @MaGheMoi AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
        )
            RAISERROR(N'Ghế mới trên chuyến bay đã có người chọn!', 16, 1);

        DECLARE @GiaCoBanMoi money, @LaQuocTeMoi bit = 0;
        SELECT @GiaCoBanMoi = cb.GiaVeCoBan,
               @LaQuocTeMoi = CASE WHEN sbDi.QuocGia <> N'Việt Nam' OR sbDen.QuocGia <> N'Việt Nam' THEN 1 ELSE 0 END
        FROM ChuyenBay cb
        JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
        JOIN SanBay sbDi ON tb.MaSanBayDi = sbDi.MaSanBay
        JOIN SanBay sbDen ON tb.MaSanBayDen = sbDen.MaSanBay
        WHERE cb.MaChuyenBay = @MaChuyenBayMoi;

        DECLARE @ThueMoi money = CASE
            WHEN @LoaiHK = N'Em bé' THEN 0
            WHEN @LoaiHK = N'Trẻ em' THEN CASE WHEN @LaQuocTeMoi = 1 THEN 275000 ELSE 110000 END
            ELSE CASE WHEN @LaQuocTeMoi = 1 THEN 550000 ELSE 220000 END
        END;
        DECLARE @GiaVeMoi money = dbo.fn_TinhGiaVeThucTe(@GiaCoBanMoi, @MaHangGheMoi) + @ThueMoi;
        DECLARE @PhiChenhLech money = CASE WHEN @GiaVeMoi - @GiaVeCu > 0 THEN @GiaVeMoi - @GiaVeCu ELSE 0 END;
        DECLARE @PhiDoiVe money = CASE WHEN @MaHangGheMoi IN ('BUS', 'FIR') THEN 0 ELSE 350000 END;

        UPDATE VeMayBay SET TrangThai = N'Đã đổi' WHERE MaVe = @MaVeCu;

        DECLARE @MaVeMoi varchar(25) = LEFT(@MaVeCu, 18) + '-R' + CAST(ABS(CHECKSUM(NEWID())) % 90 + 10 AS varchar(2));
        INSERT INTO VeMayBay (MaVe, MaDatCho, HuongBay, ThuTuChang, MaChuyenBay, MaGhe, MaHangGhe, TenHanhKhach, NgaySinh, CCCD_HanhKhach, LoaiGiayTo, SoGiayTo, HanHoChieu, QuocTich, LoaiHanhKhach, GiaCoBan, ThuePhi, GiaVe, TrangThai)
        VALUES (@MaVeMoi, @MaDatCho, @HuongBay, @ThuTuChang, @MaChuyenBayMoi, @MaGheMoi, @MaHangGheMoi, @TenHK, @NgaySinh, @CCCD, @LoaiGT, @SoGT, @HanHC, @QuocTich, @LoaiHK, @GiaCoBanMoi, @ThueMoi, @GiaVeMoi, N'Đã xuất vé');

        INSERT INTO YeuCauHuyDoiVe (MaVe, LoaiYeuCau, NgayYeuCau, LyDo, MaChuyenBayMoi, MaGheMoi, PhiChenhLech, PhiPhat, TrangThai, MaNVXuLy, NgayXuLy)
        VALUES (@MaVeCu, N'Đổi vé', GETDATE(), N'Đổi sang chuyến bay ' + @MaChuyenBayMoi, @MaChuyenBayMoi, @MaGheMoi, @PhiChenhLech, @PhiDoiVe, N'Đã hoàn tất', @MaNVXuLy, GETDATE());

        COMMIT TRANSACTION;
        SELECT @MaVeMoi AS MaVeMoi, @PhiChenhLech AS TienChenhLechGia, @PhiDoiVe AS PhiDichVuDoi, (@PhiChenhLech + @PhiDoiVe) AS TongTienThanhToanThem;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

-- 11. Đăng ký & Đăng nhập Khách hàng (PBKDF2 Password Hash)
CREATE PROCEDURE sp_DangKyKhachHang
    @TenKH            nvarchar(60),
    @GioiTinh         nvarchar(10)  = N'Nam',
    @NgaySinh         date          = '2000-01-01',
    @SDT              varchar(15),
    @Email            varchar(100),
    @MatKhau          varchar(255),
    @DiaChi           nvarchar(200) = NULL,
    @LoaiKhachHang    nvarchar(30)  = N'Việt Nam',
    @CCCD             varchar(20)   = NULL,
    @HoChieu          varchar(30)   = NULL,
    @HanHoChieu       date          = NULL,
    @QuocTich         nvarchar(50)  = N'Việt Nam',
    @CoQuanCongTac    nvarchar(150) = NULL,
    @SoTK_DoanhNghiep varchar(30)   = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        IF EXISTS (SELECT 1 FROM KhachHang WHERE Email = @Email OR SDT = @SDT)
            RAISERROR(N'Email hoặc Số điện thoại này đã được đăng ký trong hệ thống!', 16, 1);

        IF @CCCD IS NOT NULL AND EXISTS (SELECT 1 FROM KhachHang WHERE CCCD = @CCCD)
            RAISERROR(N'Số CCCD / Giấy tờ này đã tồn tại trong hệ thống!', 16, 1);

        IF @HoChieu IS NOT NULL AND EXISTS (SELECT 1 FROM KhachHang WHERE HoChieu = @HoChieu)
            RAISERROR(N'Số Hộ chiếu này đã tồn tại trong hệ thống!', 16, 1);

        DECLARE @MaxNum int = ISNULL((SELECT MAX(TRY_CAST(SUBSTRING(MaKH, 3, 10) AS int)) FROM KhachHang), 0) + 1;
        DECLARE @MaKH varchar(10) = 'KH' + RIGHT('00000' + CAST(@MaxNum AS varchar(10)), 5);

        INSERT INTO KhachHang (MaKH, TenKH, GioiTinh, NgaySinh, SDT, Email, MatKhau, DiaChi, LoaiKhachHang, CCCD, HoChieu, HanHoChieu, QuocTich, CoQuanCongTac, SoTK_DoanhNghiep, TrangThaiKhoa, NgayDangKy)
        VALUES (@MaKH, @TenKH, @GioiTinh, @NgaySinh, @SDT, @Email, @MatKhau, @DiaChi, @LoaiKhachHang, @CCCD, @HoChieu, @HanHoChieu, @QuocTich, @CoQuanCongTac, @SoTK_DoanhNghiep, 0, GETDATE());

        COMMIT TRANSACTION;
        SELECT @MaKH AS MaKH, N'Đăng ký tài khoản thành công!' AS ThongBao;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        DECLARE @ErrMsg nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrMsg, 16, 1);
    END CATCH
END;
GO

CREATE PROCEDURE sp_DangNhapKhachHang (@TaiKhoan varchar(100))
AS
BEGIN
    SET NOCOUNT ON;
    SELECT kh.*
    FROM KhachHang kh
    WHERE (kh.Email = @TaiKhoan OR kh.SDT = @TaiKhoan) AND kh.TrangThaiKhoa = 0;
END;
GO

-- 12. Cursor giải phóng đơn quá hạn 15 phút & trả về danh sách ghế vừa nhả để bắn SignalR
CREATE PROCEDURE sp_TuDongHuyDonGiuChoHetHan_Cursor
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ReleasedSeats TABLE (MaChuyenBay varchar(20), MaGhe int, MaDatCho varchar(15));
    DECLARE @MaDatCho varchar(15);

    DECLARE cur_DonHetHan CURSOR LOCAL FAST_FORWARD FOR
        SELECT MaDatCho
        FROM PhieuDatCho WITH (UPDLOCK, READPAST)
        WHERE TrangThai = N'Chờ thanh toán' AND HanThanhToan < GETDATE();

    OPEN cur_DonHetHan;
    FETCH NEXT FROM cur_DonHetHan INTO @MaDatCho;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRANSACTION;
        IF EXISTS (SELECT 1 FROM PhieuDatCho WHERE MaDatCho = @MaDatCho AND TrangThai = N'Chờ thanh toán' AND HanThanhToan < GETDATE())
        BEGIN
            INSERT INTO @ReleasedSeats (MaChuyenBay, MaGhe, MaDatCho)
            SELECT v.MaChuyenBay, v.MaGhe, @MaDatCho
            FROM VeMayBay v
            WHERE v.MaDatCho = @MaDatCho AND v.MaGhe IS NOT NULL AND v.TrangThai = N'Đã đặt chỗ';

            UPDATE PhieuDatCho SET TrangThai = N'Hết hạn giữ chỗ' WHERE MaDatCho = @MaDatCho;
            UPDATE VeMayBay SET TrangThai = N'Đã hủy' WHERE MaDatCho = @MaDatCho AND TrangThai = N'Đã đặt chỗ';
            UPDATE GiaoDichThanhToan SET TrangThai = N'Hết hạn', NgayCapNhat = GETDATE()
            WHERE MaDatCho = @MaDatCho AND TrangThai = N'Chờ thanh toán';
        END
        COMMIT TRANSACTION;

        FETCH NEXT FROM cur_DonHetHan INTO @MaDatCho;
    END;

    CLOSE cur_DonHetHan;
    DEALLOCATE cur_DonHetHan;

    SELECT MaChuyenBay, MaGhe, MaDatCho FROM @ReleasedSeats;
END;
GO

-- ====================================================================================================
-- PHẦN 7: NẠP DỮ LIỆU QUY MÔ THỰC TẾ (23 SÂN BAY, 22 TÀU BAY, >1600 CHUYẾN, 35 KH, 105 ĐƠN ĐẶT)
-- ====================================================================================================

DECLARE @SeedPwd varchar(255) = 'PBKDF2$100000$U2t5Vk5BaXJTZWVkU2FsdA==$BjWNFuGa3Nz4jy9WtKiIWKuIp8nqlHvOCWe4VVo9TqY=';

-- 1. Hãng Hàng Không (5 hãng)
INSERT INTO HangHangKhong (MaHang, TenHang, QuocGia, Logo, TrangThai) VALUES
('VN', N'Vietnam Airlines',               N'Việt Nam', '/images/airlines/VN.svg', 1),
('VJ', N'Vietjet Air',                     N'Việt Nam', '/images/airlines/VJ.svg', 1),
('QH', N'Bamboo Airways',                  N'Việt Nam', '/images/airlines/QH.svg', 1),
('VU', N'Vietravel Airlines',              N'Việt Nam', '/images/airlines/VU.svg', 1),
('0V', N'VASCO (Hàng không Vùng)',         N'Việt Nam', '/images/airlines/0V.svg', 1);

-- 2. Sân Bay (23 sân bay: 20 nội địa + 3 quốc tế gần cho kiểm tra transit/hộ chiếu 6 tháng)
INSERT INTO SanBay (MaSanBay, TenSanBay, ThanhPho, QuocGia, KhuVuc, HinhAnhUrl, HinhAnhBannerUrl, MoTaDiemDen, TrangThai) VALUES
('SGN', N'Sân bay Quốc tế Tân Sơn Nhất', N'TP. Hồ Chí Minh',   N'Việt Nam', N'Miền Nam',   '/images/destinations/SGN.jpg', '/images/destinations/SGN.jpg', N'Trung tâm kinh tế sôi động bậc nhất Việt Nam.', 1),
('HAN', N'Sân bay Quốc tế Nội Bài',      N'Hà Nội',             N'Việt Nam', N'Miền Bắc',   '/images/destinations/HAN.jpg', '/images/destinations/HAN.jpg', N'Thủ đô nghìn năm văn hiến với Hồ Gươm & Phố cổ.', 1),
('DAD', N'Sân bay Quốc tế Đà Nẵng',      N'Đà Nẵng',            N'Việt Nam', N'Miền Trung', '/images/destinations/DAD.jpg', '/images/destinations/DAD.jpg', N'Thành phố đáng sống bên bờ sông Hàn và biển Mỹ Khê.', 1),
('CXR', N'Sân bay Quốc tế Cam Ranh',     N'Nha Trang',          N'Việt Nam', N'Miền Trung', '/images/destinations/CXR.jpg', '/images/destinations/CXR.jpg', N'Vịnh biển ngọc nắng ấm quanh năm tại Khánh Hòa.', 1),
('PQC', N'Sân bay Quốc tế Phú Quốc',     N'Phú Quốc',           N'Việt Nam', N'Miền Nam',   '/images/destinations/PQC.jpg', '/images/destinations/PQC.jpg', N'Đảo Ngọc thiên đường nghỉ dưỡng Tây Nam Bộ.', 1),
('HPH', N'Sân bay Quốc tế Cát Bi',       N'Hải Phòng',          N'Việt Nam', N'Miền Bắc',   '/images/destinations/HPH.jpg', '/images/destinations/HPH.jpg', N'Thành phố cảng cửa ngõ vịnh Bắc Bộ & Cát Bà.', 1),
('VCA', N'Sân bay Quốc tế Cần Thơ',      N'Cần Thơ',            N'Việt Nam', N'Miền Nam',   '/images/destinations/VCA.jpg', '/images/destinations/VCA.jpg', N'Thủ phủ Tây Đô nổi tiếng chợ nổi Cái Răng.', 1),
('VII', N'Sân bay Quốc tế Vinh',         N'Vinh',               N'Việt Nam', N'Miền Trung', '/images/destinations/VII.jpg', '/images/destinations/VII.jpg', N'Trung tâm Bắc Trung Bộ giàu truyền thống lịch sử.', 1),
('HUI', N'Sân bay Quốc tế Phú Bài',      N'Huế',                N'Việt Nam', N'Miền Trung', '/images/destinations/HUI.jpg', '/images/destinations/HUI.jpg', N'Cố đô mộng mơ bên dòng sông Hương trữ tình.', 1),
('UIH', N'Sân bay Phù Cát',              N'Quy Nhơn',           N'Việt Nam', N'Miền Trung', '/images/destinations/UIH.jpg', '/images/destinations/UIH.jpg', N'Biển xanh Kỳ Co - Eo Gió đầy nắng gió Bình Định.', 1),
('DLI', N'Sân bay Liên Khương',          N'Đà Lạt',             N'Việt Nam', N'Tây Nguyên', '/images/destinations/DLI.jpg', '/images/destinations/DLI.jpg', N'Thành phố ngàn hoa khí hậu ôn đới quanh năm.', 1),
('VCS', N'Sân bay Côn Đảo',              N'Côn Đảo',            N'Việt Nam', N'Miền Nam',   '/images/destinations/VCS.jpg', '/images/destinations/VCS.jpg', N'Quần đảo hoang sơ linh thiêng ngoài khơi Bà Rịa.', 1),
('DIN', N'Sân bay Điện Biên Phủ',        N'Điện Biên',          N'Việt Nam', N'Tây Bắc',    '/images/destinations/DIN.jpg', '/images/destinations/DIN.jpg', N'Vùng đất lịch sử hào hùng giữa núi rừng Tây Bắc.', 1),
('BMV', N'Sân bay Buôn Ma Thuột',        N'Buôn Ma Thuột',      N'Việt Nam', N'Tây Nguyên', '/images/destinations/BMV.jpg', '/images/destinations/BMV.jpg', N'Thủ phủ cà phê đại ngàn Đắk Lắk.', 1),
('THD', N'Sân bay Thọ Xuân',             N'Thanh Hóa',          N'Việt Nam', N'Miền Trung', '/images/destinations/THD.jpg', '/images/destinations/THD.jpg', N'Cửa ngõ Sầm Sơn & di sản Thành Nhà Hồ.', 1),
('VCL', N'Sân bay Chu Lai',              N'Quảng Nam',          N'Việt Nam', N'Miền Trung', '/images/destinations/VCL.jpg', '/images/destinations/VCL.jpg', N'Kết nối phố cổ Hội An và thánh địa Mỹ Sơn.', 1),
('TBB', N'Sân bay Tuy Hòa',              N'Phú Yên',            N'Việt Nam', N'Miền Trung', '/images/destinations/TBB.jpg', '/images/destinations/TBB.jpg', N'Xứ Nẫu hoa vàng trên cỏ xanh và Gành Đá Đĩa.', 1),
('VKG', N'Sân bay Rạch Giá',             N'Kiên Giang',         N'Việt Nam', N'Miền Nam',   '/images/destinations/VKG.jpg', '/images/destinations/VKG.jpg', N'Cửa ngõ đất liền đi Nam Du và Phú Quốc.', 1),
('CAH', N'Sân bay Cà Mau',               N'Cà Mau',             N'Việt Nam', N'Miền Nam',   '/images/destinations/CAH.jpg', '/images/destinations/CAH.jpg', N'Vùng đất mũi cực Nam thiêng liêng của Tổ quốc.', 1),
('PXU', N'Sân bay Pleiku',               N'Gia Lai',            N'Việt Nam', N'Tây Nguyên', '/images/destinations/PXU.jpg', '/images/destinations/PXU.jpg', N'Biển Hồ xanh ngát giữa cao nguyên đất đỏ.', 1),
('BKK', N'Sân bay Quốc tế Suvarnabhumi', N'Bangkok',            N'Thái Lan', N'Quốc tế',    '/images/destinations/BKK.jpg', '/images/destinations/BKK.jpg', N'Thủ đô sôi động với ẩm thực và văn hóa chùa vàng.', 1),
('SIN', N'Sân bay Quốc tế Changi',       N'Singapore',          N'Singapore',N'Quốc tế',    '/images/destinations/SIN.jpg', '/images/destinations/SIN.jpg', N'Đảo quốc hiện đại với Marina Bay và Jewel Changi.', 1),
('ICN', N'Sân bay Quốc tế Incheon',      N'Seoul',              N'Hàn Quốc', N'Quốc tế',    '/images/destinations/ICN.jpg', '/images/destinations/ICN.jpg', N'Thành phố kết hợp truyền thống và công nghệ K-Culture.', 1);

-- 3. Tuyến Bay (54 tuyến bay 2 chiều)
INSERT INTO TuyenBay (MaTuyenBay, MaSanBayDi, MaSanBayDen, KhoangCachKm) VALUES
('SGN-HAN', 'SGN', 'HAN', 1160), ('HAN-SGN', 'HAN', 'SGN', 1160),
('SGN-DAD', 'SGN', 'DAD', 605),  ('DAD-SGN', 'DAD', 'SGN', 605),
('HAN-DAD', 'HAN', 'DAD', 630),  ('DAD-HAN', 'DAD', 'HAN', 630),
('SGN-PQC', 'SGN', 'PQC', 300),  ('PQC-SGN', 'PQC', 'SGN', 300),
('HAN-PQC', 'HAN', 'PQC', 1230), ('PQC-HAN', 'PQC', 'HAN', 1230),
('SGN-CXR', 'SGN', 'CXR', 310),  ('CXR-SGN', 'CXR', 'SGN', 310),
('HAN-CXR', 'HAN', 'CXR', 1050), ('CXR-HAN', 'CXR', 'HAN', 1050),
('SGN-DLI', 'SGN', 'DLI', 220),  ('DLI-SGN', 'DLI', 'SGN', 220),
('HAN-DLI', 'HAN', 'DLI', 1080), ('DLI-HAN', 'DLI', 'HAN', 1080),
('SGN-HPH', 'SGN', 'HPH', 1110), ('HPH-SGN', 'HPH', 'SGN', 1110),
('SGN-VII', 'SGN', 'VII', 920),  ('VII-SGN', 'VII', 'SGN', 920),
('SGN-HUI', 'SGN', 'HUI', 640),  ('HUI-SGN', 'HUI', 'SGN', 640),
('HAN-HUI', 'HAN', 'HUI', 540),  ('HUI-HAN', 'HUI', 'HAN', 540),
('SGN-UIH', 'SGN', 'UIH', 450),  ('UIH-SGN', 'UIH', 'SGN', 450),
('HAN-UIH', 'HAN', 'UIH', 820),  ('UIH-HAN', 'UIH', 'HAN', 820),
('SGN-VCS', 'SGN', 'VCS', 230),  ('VCS-SGN', 'VCS', 'SGN', 230),
('SGN-VCA', 'SGN', 'VCA', 165),  ('VCA-SGN', 'VCA', 'SGN', 165),
('HAN-VCA', 'HAN', 'VCA', 1210), ('VCA-HAN', 'VCA', 'HAN', 1210),
('HAN-DIN', 'HAN', 'DIN', 290),  ('DIN-HAN', 'DIN', 'HAN', 290),
('SGN-BMV', 'SGN', 'BMV', 260),  ('BMV-SGN', 'BMV', 'SGN', 260),
('SGN-THD', 'SGN', 'THD', 1040), ('THD-SGN', 'THD', 'SGN', 1040),
('SGN-PXU', 'SGN', 'PXU', 390),  ('PXU-SGN', 'PXU', 'SGN', 390),
('SGN-TBB', 'SGN', 'TBB', 420),  ('TBB-SGN', 'TBB', 'SGN', 420),
('SGN-VCL', 'SGN', 'VCL', 550),  ('VCL-SGN', 'VCL', 'SGN', 550),
('SGN-SIN', 'SGN', 'SIN', 1090), ('SIN-SGN', 'SIN', 'SGN', 1090),
('SGN-BKK', 'SGN', 'BKK', 745),  ('BKK-SGN', 'BKK', 'SGN', 745),
('HAN-ICN', 'HAN', 'ICN', 2740), ('ICN-HAN', 'ICN', 'HAN', 2740);

-- 4. Hạng Ghế
INSERT INTO HangGhe (MaHangGhe, TenHangGhe, HeSoGia, MoTa) VALUES
('ECO', N'Hạng Phổ Thông',  1.00, N'Hành lý xách tay 7kg/12kg, chỗ ngồi tiêu chuẩn'),
('BUS', N'Hạng Thương Gia', 2.00, N'Phòng chờ VIP, ưu tiên làm thủ tục, hành lý ký gửi 30kg'),
('FIR', N'Hạng Nhất',       3.50, N'Khoang riêng cao cấp, ẩm thực thượng hạng, xe đưa đón');

-- 5. Máy Bay (22 tàu bay) + Sinh Sơ đồ Ghế tự động bằng vòng lặp SQL
INSERT INTO MayBay (MaMayBay, TenMayBay, MaHang, TongSoGhe, TrangThai) VALUES
('VN-A888', N'Boeing 787-9 Dreamliner', 'VN', 1, N'Sẵn sàng'),
('VN-A350', N'Airbus A350-900',         'VN', 1, N'Sẵn sàng'),
('VN-A321', N'Airbus A321neo',          'VN', 1, N'Sẵn sàng'),
('VN-A322', N'Airbus A321-200',         'VN', 1, N'Sẵn sàng'),
('VN-A323', N'Airbus A321neo',          'VN', 1, N'Sẵn sàng'),
('VN-A324', N'Airbus A321neo',          'VN', 1, N'Sẵn sàng'),
('VJ-A321', N'Airbus A321neo',          'VJ', 1, N'Sẵn sàng'),
('VJ-A322', N'Airbus A321-200',         'VJ', 1, N'Sẵn sàng'),
('VJ-A323', N'Airbus A320-200',         'VJ', 1, N'Sẵn sàng'),
('VJ-A324', N'Airbus A321neo',          'VJ', 1, N'Sẵn sàng'),
('VJ-A330', N'Airbus A330-300',         'VJ', 1, N'Sẵn sàng'),
('QH-A320', N'Airbus A320neo',          'QH', 1, N'Sẵn sàng'),
('QH-A321', N'Airbus A321neo',          'QH', 1, N'Sẵn sàng'),
('QH-A322', N'Airbus A320-200',         'QH', 1, N'Sẵn sàng'),
('QH-A787', N'Boeing 787-9 Dreamliner', 'QH', 1, N'Sẵn sàng'),
('VU-A321', N'Airbus A321-200',         'VU', 1, N'Sẵn sàng'),
('VU-A320', N'Airbus A320-200',         'VU', 1, N'Sẵn sàng'),
('VU-A322', N'Airbus A321neo',          'VU', 1, N'Sẵn sàng'),
('0V-A072', N'ATR 72-600 Côn Đảo',      '0V', 1, N'Sẵn sàng'),
('0V-A073', N'ATR 72-600 Vùng',         '0V', 1, N'Sẵn sàng'),
('VN-A325', N'Airbus A320neo',          'VN', 1, N'Sẵn sàng'),
('VJ-A325', N'Airbus A321neo',          'VJ', 1, N'Sẵn sàng');

DECLARE @MB varchar(15), @TenMB nvarchar(60);
DECLARE cur_MB CURSOR LOCAL FAST_FORWARD FOR SELECT MaMayBay, TenMayBay FROM MayBay;
OPEN cur_MB;
FETCH NEXT FROM cur_MB INTO @MB, @TenMB;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE @r int;
    -- Hàng 01-02: Thương gia (4 ghế/hàng: A, C, D, F)
    SET @r = 1;
    WHILE @r <= 2
    BEGIN
        INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, ViTri, GiaChonGhe, TrangThai) VALUES
        (@MB, RIGHT('0' + CAST(@r AS varchar), 2) + 'A', 'BUS', N'Cửa sổ', 0, 1),
        (@MB, RIGHT('0' + CAST(@r AS varchar), 2) + 'C', 'BUS', N'Lối đi', 0, 1),
        (@MB, RIGHT('0' + CAST(@r AS varchar), 2) + 'D', 'BUS', N'Lối đi', 0, 1),
        (@MB, RIGHT('0' + CAST(@r AS varchar), 2) + 'F', 'BUS', N'Cửa sổ', 0, 1);
        SET @r = @r + 1;
    END

    -- Hàng 10-19 (hoặc 10-18 cho ATR72): Phổ thông phân tầng giá chuẩn Vietjet Air thực tế
    -- H10 Đỏ (Ghế cao cấp ECO: 90k), H11-12 Tím (Hàng trước: 40k), H14 Xanh dương (Để chân rộng: 90k), H13/15-19 Xanh lá (Tiêu chuẩn: 30k cửa sổ/lối đi, 0đ giữa)
    SET @r = 10;
    DECLARE @MaxRow int = CASE WHEN @TenMB LIKE '%ATR%' THEN 18 ELSE 19 END;
    WHILE @r <= @MaxRow
    BEGIN
        DECLARE @FeeWin   money = CASE WHEN @r IN (10, 14) THEN 90000 WHEN @r BETWEEN 11 AND 12 THEN 40000 ELSE 30000 END;
        DECLARE @FeeAisle money = CASE WHEN @r IN (10, 14) THEN 90000 WHEN @r BETWEEN 11 AND 12 THEN 40000 ELSE 30000 END;
        DECLARE @FeeMid   money = CASE WHEN @r IN (10, 14) THEN 90000 WHEN @r BETWEEN 11 AND 12 THEN 40000 ELSE 0     END;

        IF @TenMB LIKE '%ATR%'
        BEGIN
            INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, ViTri, GiaChonGhe, TrangThai) VALUES
            (@MB, CAST(@r AS varchar) + 'A', 'ECO', N'Cửa sổ', @FeeWin, 1),
            (@MB, CAST(@r AS varchar) + 'C', 'ECO', N'Lối đi', @FeeAisle, 1),
            (@MB, CAST(@r AS varchar) + 'D', 'ECO', N'Lối đi', @FeeAisle, 1),
            (@MB, CAST(@r AS varchar) + 'F', 'ECO', N'Cửa sổ', @FeeWin, 1);
        END
        ELSE
        BEGIN
            INSERT INTO GheNgoi (MaMayBay, SoGhe, MaHangGhe, ViTri, GiaChonGhe, TrangThai) VALUES
            (@MB, CAST(@r AS varchar) + 'A', 'ECO', N'Cửa sổ', @FeeWin,   1),
            (@MB, CAST(@r AS varchar) + 'B', 'ECO', N'Giữa',   @FeeMid,   1),
            (@MB, CAST(@r AS varchar) + 'C', 'ECO', N'Lối đi', @FeeAisle, 1),
            (@MB, CAST(@r AS varchar) + 'D', 'ECO', N'Lối đi', @FeeAisle, 1),
            (@MB, CAST(@r AS varchar) + 'E', 'ECO', N'Giữa',   @FeeMid,   1),
            (@MB, CAST(@r AS varchar) + 'F', 'ECO', N'Cửa sổ', @FeeWin,   1);
        END
        SET @r = @r + 1;
    END

    UPDATE MayBay SET TongSoGhe = (SELECT COUNT(*) FROM GheNgoi WHERE MaMayBay = @MB AND TrangThai = 1) WHERE MaMayBay = @MB;
    FETCH NEXT FROM cur_MB INTO @MB, @TenMB;
END
CLOSE cur_MB;
DEALLOCATE cur_MB;
GO

-- 6. Sinh >= 1,600 Chuyến Bay thực tế phủ từ hôm nay -5 ngày đến +185 ngày (gồm cả T12/2026 & T01-T02/2027)
DECLARE @StartDate date = DATEADD(DAY, -5, CAST(GETDATE() AS date));
DECLARE @EndDate   date = DATEADD(DAY, 185, CAST(GETDATE() AS date));
DECLARE @CurDate   date = @StartDate;
DECLARE @DayIdx    int = 0;

WHILE @CurDate <= @EndDate
BEGIN
    DECLARE @DCode varchar(8) = CONVERT(varchar(6), @CurDate, 12); -- YYMMDD
    DECLARE @WeekendFactor money = CASE WHEN DATEPART(WEEKDAY, @CurDate) IN (1, 6, 7) THEN 180000 ELSE 0 END;
    DECLARE @PeakTetFactor money = CASE WHEN MONTH(@CurDate) IN (12, 1, 2) THEN 250000 ELSE 0 END;
    DECLARE @State nvarchar(30) = CASE WHEN @CurDate < CAST(GETDATE() AS date) THEN N'Đã cất cánh' ELSE N'Đang mở bán' END;

    -- Sáng / Chiều / Tối trên các trục chính SGN <-> HAN, SGN <-> DAD, HAN <-> DAD, PQC, CXR
    INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai)
    SELECT v.MaCB, v.MaTB, v.MaMB, v.GioBay, DATEADD(MINUTE, v.Phut, v.GioBay), v.Phut, v.Gia, mb.TongSoGhe, @State
    FROM (VALUES
        -- SGN <-> HAN (4 chuyến đi + 4 chuyến về mỗi ngày để có giá theo ngày & khứ hồi khớp giờ)
        ('VN120-' + @DCode, 'SGN-HAN', 'VN-A888', DATEADD(HOUR, 6,  CAST(@CurDate AS datetime)), 130, 1290000 + @WeekendFactor + @PeakTetFactor + ((@DayIdx % 4) * 50000)),
        ('VJ150-' + @DCode, 'SGN-HAN', 'VJ-A321', DATEADD(HOUR, 9,  CAST(@CurDate AS datetime)), 130,  790000 + @WeekendFactor + @PeakTetFactor + ((@DayIdx % 5) * 40000)),
        ('QH202-' + @DCode, 'SGN-HAN', 'QH-A787', DATEADD(HOUR, 14, CAST(@CurDate AS datetime)), 135, 1090000 + @WeekendFactor + @PeakTetFactor),
        ('VU302-' + @DCode, 'SGN-HAN', 'VU-A321', DATEADD(HOUR, 19, CAST(@CurDate AS datetime)), 135,  890000 + @WeekendFactor + ((@DayIdx % 3) * 60000)),
        ('VN121-' + @DCode, 'HAN-SGN', 'VN-A350', DATEADD(HOUR, 7,  CAST(@CurDate AS datetime)), 135, 1320000 + @WeekendFactor + @PeakTetFactor),
        ('VJ151-' + @DCode, 'HAN-SGN', 'VJ-A322', DATEADD(HOUR, 11, CAST(@CurDate AS datetime)), 135,  810000 + @WeekendFactor + @PeakTetFactor + ((@DayIdx % 5) * 40000)),
        ('QH203-' + @DCode, 'HAN-SGN', 'QH-A320', DATEADD(HOUR, 15, CAST(@CurDate AS datetime)), 135, 1120000 + @WeekendFactor),
        ('VU303-' + @DCode, 'HAN-SGN', 'VU-A320', DATEADD(HOUR, 20, CAST(@CurDate AS datetime)), 135,  850000 + ((@DayIdx % 4) * 50000)),
        -- SGN <-> DAD & HAN <-> DAD
        ('VJ201-' + @DCode, 'SGN-DAD', 'VJ-A323', DATEADD(HOUR, 8,  CAST(@CurDate AS datetime)),  80,  690000 + @WeekendFactor + ((@DayIdx % 4) * 35000)),
        ('VN214-' + @DCode, 'SGN-DAD', 'VN-A321', DATEADD(HOUR, 16, CAST(@CurDate AS datetime)),  80, 1050000 + @WeekendFactor + @PeakTetFactor),
        ('VJ202-' + @DCode, 'DAD-SGN', 'VJ-A323', DATEADD(HOUR, 11, CAST(@CurDate AS datetime)),  80,  710000 + @WeekendFactor + ((@DayIdx % 4) * 35000)),
        ('VN215-' + @DCode, 'DAD-SGN', 'VN-A321', DATEADD(HOUR, 19, CAST(@CurDate AS datetime)),  80, 1080000 + @WeekendFactor),
        ('VN310-' + @DCode, 'HAN-DAD', 'VN-A322', DATEADD(HOUR, 8,  CAST(@CurDate AS datetime)),  80,  990000 + @WeekendFactor),
        ('VJ312-' + @DCode, 'DAD-HAN', 'VJ-A324', DATEADD(HOUR, 14, CAST(@CurDate AS datetime)),  80,  720000 + @WeekendFactor),
        -- Du lịch + Hub nối chuyến (SGN <-> VCS, SGN <-> PQC, HAN <-> PQC, SGN <-> CXR, HPH <-> SGN, VII <-> HAN, HAN <-> DIN, VCA <-> SGN)
        ('VJ452-' + @DCode, 'SGN-PQC', 'VJ-A325', DATEADD(HOUR, 10, CAST(@CurDate AS datetime)),  65,  650000 + ((@DayIdx % 4) * 40000)),
        ('VN453-' + @DCode, 'PQC-SGN', 'VN-A323', DATEADD(HOUR, 16, CAST(@CurDate AS datetime)),  65,  890000 + @WeekendFactor),
        ('QH410-' + @DCode, 'HAN-PQC', 'QH-A321', DATEADD(HOUR, 11, CAST(@CurDate AS datetime)), 125, 1190000 + @PeakTetFactor),
        ('0V101-' + @DCode, 'SGN-VCS', '0V-A072', DATEADD(HOUR, 10, CAST(@CurDate AS datetime)),  55, 1650000 + @WeekendFactor),
        ('0V102-' + @DCode, 'VCS-SGN', '0V-A073', DATEADD(HOUR, 15, CAST(@CurDate AS datetime)),  55, 1650000 + @WeekendFactor),
        ('VN510-' + @DCode, 'SGN-CXR', 'VN-A324', DATEADD(HOUR, 12, CAST(@CurDate AS datetime)),  65,  820000 + ((@DayIdx % 3) * 45000)),
        ('VJ511-' + @DCode, 'CXR-SGN', 'VJ-A330', DATEADD(HOUR, 18, CAST(@CurDate AS datetime)),  65,  760000 + ((@DayIdx % 3) * 45000))
    ) v (MaCB, MaTB, MaMB, GioBay, Phut, Gia)
    JOIN MayBay mb ON v.MaMB = mb.MaMayBay;

    -- Cặp chuyến nối tuyến mỗi ngày (HPH->SGN + SGN->VCS, DIN->HAN + HAN->SGN, VII->HAN + HAN->PQC, VCA->SGN + SGN->DAD)
    IF @DayIdx % 2 = 0
    BEGIN
        INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai)
        SELECT v.MaCB, v.MaTB, v.MaMB, v.GioBay, DATEADD(MINUTE, v.Phut, v.GioBay), v.Phut, v.Gia, mb.TongSoGhe, @State
        FROM (VALUES
            ('VN601-' + @DCode, 'HPH-SGN', 'VN-A325', DATEADD(HOUR, 6,  CAST(@CurDate AS datetime)), 125, 1050000),
            ('VN720-' + @DCode, 'DIN-HAN', '0V-A072', DATEADD(HOUR, 8,  CAST(@CurDate AS datetime)),  60,  980000),
            ('VJ620-' + @DCode, 'SGN-DLI', 'VJ-A324', DATEADD(HOUR, 13, CAST(@CurDate AS datetime)),  55,  620000),
            ('VU410-' + @DCode, 'SGN-HUI', 'VU-A322', DATEADD(HOUR, 15, CAST(@CurDate AS datetime)),  80,  780000),
            ('VN901-' + @DCode, 'SGN-SIN', 'VN-A350', DATEADD(HOUR, 9,  CAST(@CurDate AS datetime)), 125, 2250000),
            ('VN902-' + @DCode, 'SIN-SGN', 'VN-A350', DATEADD(HOUR, 15, CAST(@CurDate AS datetime)), 125, 2350000),
            ('VJ910-' + @DCode, 'SGN-BKK', 'VJ-A321', DATEADD(HOUR, 11, CAST(@CurDate AS datetime)),  95, 1790000),
            ('QH950-' + @DCode, 'HAN-ICN', 'QH-A787', DATEADD(HOUR, 22, CAST(@CurDate AS datetime)), 270, 3950000)
        ) v (MaCB, MaTB, MaMB, GioBay, Phut, Gia)
        JOIN MayBay mb ON v.MaMB = mb.MaMayBay;
    END

    SET @CurDate = DATEADD(DAY, 1, @CurDate);
    SET @DayIdx = @DayIdx + 1;
END
GO

-- Chèn thêm các mã chuyến bay đặc trưng dễ nhớ ở ngày gần nhất (Hôm nay + 3 ngày) cho demo nhanh & Transit
DECLARE @DemoDate date = DATEADD(DAY, 3, CAST(GETDATE() AS date));
INSERT INTO ChuyenBay (MaChuyenBay, MaTuyenBay, MaMayBay, NgayGioBay, NgayGioDenDK, ThoiGianBay, GiaVeCoBan, SoGheTrong, TrangThai)
SELECT v.MaCB, v.MaTB, v.MaMB, v.GioBay, DATEADD(MINUTE, v.Phut, v.GioBay), v.Phut, v.Gia, mb.TongSoGhe, N'Đang mở bán'
FROM (VALUES
    ('VN120', 'SGN-HAN', 'VN-A888', DATEADD(HOUR, 8,  CAST(@DemoDate AS datetime)), 135, 1350000),
    ('VN121', 'HAN-SGN', 'VN-A888', DATEADD(HOUR, 14, CAST(@DemoDate AS datetime)), 135, 1380000),
    ('VJ201', 'SGN-DAD', 'VJ-A321', DATEADD(HOUR, 9,  CAST(@DemoDate AS datetime)),  80,  890000),
    ('VN214', 'DAD-SGN', 'VN-A321', DATEADD(HOUR, 17, CAST(@DemoDate AS datetime)),  80, 1150000),
    ('0V101', 'SGN-VCS', '0V-A072', DATEADD(HOUR, 10, CAST(@DemoDate AS datetime)),  55, 1750000),
    ('VJ452', 'SGN-PQC', 'VJ-A321', DATEADD(HOUR, 11, CAST(@DemoDate AS datetime)),  60,  790000),
    ('VN850', 'HPH-SGN', 'VN-A325', DATEADD(HOUR, 6,  CAST(@DemoDate AS datetime)), 125, 1150000),
    ('VN909', 'SGN-SIN', 'VN-A350', DATEADD(HOUR, 13, CAST(@DemoDate AS datetime)), 125, 2150000)
) v (MaCB, MaTB, MaMB, GioBay, Phut, Gia)
JOIN MayBay mb ON v.MaMB = mb.MaMayBay;

INSERT INTO ChiTietChuyenBay (MaChuyenBay, MaSanBayTG, ThoiGianDung, ThuTuDung, GhiChu) VALUES
('VN850', 'DAD', 45, 1, N'Dừng kỹ thuật 45 phút tại Đà Nẵng');
GO

-- 7. Dịch vụ bổ sung (10 gói dịch vụ theo biểu giá thực tế hàng không nội địa Việt Nam)
INSERT INTO DichVuBoSung (MaDV, TenDV, LoaiDV, DonGia, MoTa, GioiHanGioTruocBay, TrangThai) VALUES
('BG20',  N'Hành lý ký gửi 20kg',           N'Hành lý',   200000, N'Thêm 1 kiện hành lý ký gửi tối đa 20kg (đặt trước)', 3, 1),
('BG30',  N'Hành lý ký gửi 30kg',           N'Hành lý',   300000, N'Thêm 1 kiện hành lý ký gửi tối đa 30kg (đặt trước)', 3, 1),
('BG40',  N'Hành lý quá khổ / Thể thao 40kg',N'Hành lý',  400000, N'Tối đa 2 kiện không quá 32kg/kiện', 3, 1),
('ML01',  N'Suất ăn nóng Cơm Gà Hội An + Nước',N'Suất ăn',  75000, N'Suất ăn nóng Sky Café phục vụ trên chuyến bay', 3, 1),
('ML02',  N'Bánh mì truyền thống / Chay + Trà',N'Suất ăn',  55000, N'Thực đơn ăn nhẹ tiêu chuẩn + nước suối', 3, 1),
('ML03',  N'Combo Phở Bò Bầu Trời & Cà Phê',   N'Suất ăn', 105000, N'Hotmeal Combo kèm đồ uống pha chế', 3, 1),
('ST01',  N'Chọn chỗ ngồi Tiêu chuẩn',        N'Chọn ghế',   30000, N'Chọn trước vị trí ghế tiêu chuẩn yêu thích', 1, 1),
('ST02',  N'Ghế cao cấp / Để chân rộng',      N'Chọn ghế',   90000, N'Hàng đầu khoang hoặc hàng cửa thoát hiểm rộng chân', 1, 1),
('LG01',  N'Vé vào Phòng chờ Thương gia Lotus',N'Phòng chờ',380000, N'Nghỉ ngơi buffet & wifi tốc độ cao (miễn phí cho vé BUS)', 2, 1),
('PR01',  N'Dịch vụ Ưu tiên Check-in & Lên tàu',N'Ưu tiên', 120000, N'Làm thủ tục tại quầy SkyPriority riêng biệt', 2, 1);
GO

-- 8. Vai trò & Nhân viên (10 NV, mật khẩu PBKDF2-SHA256)
INSERT INTO VaiTro (RoleID, TenRole, MoTa) VALUES
(1, N'Ban Giám Đốc',      N'Toàn quyền quản trị hệ thống, lịch bay, khuyến mãi, nhân sự'),
(2, N'Nhân Viên Thu Ngân', N'Quản lý hóa đơn, đối soát thanh toán VietQR & hoàn tiền'),
(3, N'Nhân Viên Bán Vé',   N'Hỗ trợ tra cứu, giữ chỗ, bán vé tại quầy'),
(4, N'Nhân Viên CSKH',     N'Xử lý check-in, đổi vé, hủy vé và khiếu nại');

DECLARE @SeedPwd varchar(255) = 'PBKDF2$100000$U2t5Vk5BaXJTZWVkU2FsdA==$BjWNFuGa3Nz4jy9WtKiIWKuIp8nqlHvOCWe4VVo9TqY=';
INSERT INTO NhanVien (MaNV, TenNV, SDT, Email, ChucVu, Luong, TaiKhoan, MatKhau, RoleID, TrangThai) VALUES
('NV001', N'Nguyễn Văn Quản Trị', '0901000001', 'admin@airline.vn',     N'Trưởng phòng CNTT',  25000000, 'admin',     @SeedPwd, 1, 1),
('NV002', N'Lê Thị Thu Ngân',     '0901000002', 'thungan@airline.vn',   N'Kế toán trưởng',     18000000, 'thungan',   @SeedPwd, 2, 1),
('NV003', N'Trần Văn Bán Vé',     '0901000003', 'banve@airline.vn',     N'Tổ trưởng bán vé',   14000000, 'banve',     @SeedPwd, 3, 1),
('NV004', N'Phạm Thị CSKH',       '0901000004', 'cskh@airline.vn',      N'Chuyên viên CSKH',   12000000, 'cskh',      @SeedPwd, 4, 1),
('NV005', N'Hoàng Đức Minh',      '0901000005', 'minh.hd@airline.vn',   N'Nhân viên bán vé',   10000000, 'banve02',   @SeedPwd, 3, 1),
('NV006', N'Đỗ Mai Phương',       '0901000006', 'phuong.dm@airline.vn', N'Nhân viên thu ngân', 11000000, 'thungan02', @SeedPwd, 2, 1),
('NV007', N'Vũ Quốc Tuấn',        '0901000007', 'tuan.vq@airline.vn',   N'Nhân viên CSKH',     10500000, 'cskh02',    @SeedPwd, 4, 1),
('NV008', N'Bùi Thanh Trúc',      '0901000008', 'truc.bt@airline.vn',   N'Nhân viên bán vé',   10000000, 'banve03',   @SeedPwd, 3, 1),
('NV009', N'Đinh Hữu Tài',        '0901000009', 'tai.dh@airline.vn',    N'Quản trị CSDL',      22000000, 'dba',       @SeedPwd, 1, 1),
('NV010', N'Ngô Bảo Châu',        '0901000010', 'chau.nb@airline.vn',   N'Nhân viên CSKH',     10500000, 'cskh03',    @SeedPwd, 4, 1);

-- 9. Khách hàng (>= 35 KH: Việt Nam, Quốc tế có Hộ chiếu, Doanh nghiệp)
INSERT INTO KhachHang (MaKH, TenKH, GioiTinh, NgaySinh, SDT, Email, MatKhau, DiaChi, LoaiKhachHang, CCCD, HoChieu, HanHoChieu, QuocTich, CoQuanCongTac, SoTK_DoanhNghiep, TrangThaiKhoa) VALUES
('KH00001', N'Lê Duyên An',         N'Nam', '2004-05-15', '0911000001', 'duyenan@gmail.com',    @SeedPwd, N'123 Nguyễn Huệ, Q1, TP.HCM',       N'Việt Nam',     '079204000111', 'C9123456', '2030-05-15', N'Việt Nam', NULL, NULL, 0),
('KH00002', N'Lê Nguyễn Đăng Khoa', N'Nam', '2004-08-20', '0911000002', 'dangkhoa@gmail.com',   @SeedPwd, N'456 Lê Duẩn, Q3, TP.HCM',          N'Việt Nam',     '079204000222', NULL,       NULL,         N'Việt Nam', NULL, NULL, 0),
('KH00003', N'Lâm Ngọc Như',        N'Nữ',  '2004-11-10', '0911000003', 'ngocnhu@gmail.com',    @SeedPwd, N'789 CMT8, Tân Bình, TP.HCM',       N'Việt Nam',     '079304000333', NULL,       NULL,         N'Việt Nam', NULL, NULL, 0),
('KH00004', N'Trần Hoàng Nam',      N'Nam', '1995-02-14', '0911000004', 'hoangnam@gmail.com',   @SeedPwd, N'12 Tràng Tiền, Hoàn Kiếm, HN',     N'Việt Nam',     '001095000444', NULL,       NULL,         N'Việt Nam', NULL, NULL, 0),
('KH00005', N'Nguyễn Thúy Hằng',    N'Nữ',  '1998-07-22', '0911000005', 'thuyhang@gmail.com',   @SeedPwd, N'34 Bạch Đằng, Hải Châu, Đà Nẵng',  N'Việt Nam',     '048098000555', NULL,       NULL,         N'Việt Nam', NULL, NULL, 0),
('KH00006', N'Phan Thanh Tùng',     N'Nam', '1992-10-05', '0911000006', 'thanhtung@gmail.com',  @SeedPwd, N'56 Hùng Vương, Ninh Kiều, Cần Thơ',N'Việt Nam',     '092092000666', NULL,       NULL,         N'Việt Nam', NULL, NULL, 0),
('KH00007', N'Vũ Bích Phương',      N'Nữ',  '2000-12-18', '0911000007', 'bichphuong@gmail.com', @SeedPwd, N'88 Lạch Tray, Hải Phòng',          N'Việt Nam',     '031300000777', NULL,       NULL,         N'Việt Nam', NULL, NULL, 0),
('KH00008', N'John Smith',          N'Nam', '1988-03-25', '0911000008', 'johnsmith@gmail.com',  @SeedPwd, N'Rex Hotel, Q1, TP.HCM',            N'Nước ngoài',   NULL,           'A99887766','2029-10-01', N'Úc',       NULL, NULL, 0),
('KH00009', N'Michael Brown',       N'Nam', '1985-09-12', '0911000009', 'mbrown@yahoo.com',     @SeedPwd, N'Metropole, Hà Nội',                N'Nước ngoài',   NULL,           'US5544332','2028-12-15', N'Mỹ',       NULL, NULL, 0),
('KH00010', N'Kenji Sato',          N'Nam', '1990-06-30', '0911000010', 'kenji.sato@sony.jp',   @SeedPwd, N'Daewoo Hotel, Hà Nội',             N'Nước ngoài',   NULL,           'JP8877112','2029-06-30', N'Nhật Bản', NULL, NULL, 0),
('KH00011', N'Emma Watson',         N'Nữ',  '1996-04-15', '0911000011', 'emma.w@oxford.uk',     @SeedPwd, N'Vinpearl Resort Phú Quốc',         N'Nước ngoài',   NULL,           'UK1122334','2028-08-20', N'Anh',      NULL, NULL, 0),
('KH00012', N'Phạm Nhật Vượng',     N'Nam', '1968-08-05', '0911000012', 'corp.vin@vingroup.net',@SeedPwd, N'Vinhomes Riverside, Hà Nội',       N'Doanh nghiệp', '001068000888', NULL,       NULL,         N'Việt Nam', N'Tập đoàn Vingroup', '0101245486', 0),
('KH00013', N'Trương Gia Bình',     N'Nam', '1956-05-19', '0911000013', 'corp.fpt@fpt.com.vn',  @SeedPwd, N'FPT Tower, Cầu Giấy, Hà Nội',      N'Doanh nghiệp', '001056000999', NULL,       NULL,         N'Việt Nam', N'Tập đoàn FPT',      '0101248141', 0),
('KH00014', N'Mai Kiều Liên',       N'Nữ',  '1953-09-01', '0911000014', 'corp.vnm@vinamilk.com',@SeedPwd, N'10 Tân Trào, Q7, TP.HCM',          N'Doanh nghiệp', '079053000112', NULL,       NULL,         N'Việt Nam', N'Vinamilk',          '0300588569', 0),
('KH00015', N'Nguyễn Đăng Quang',   N'Nam', '1963-08-23', '0911000015', 'corp.msn@masan.vn',    @SeedPwd, N'Masan Tower, Q1, TP.HCM',          N'Doanh nghiệp', '079063000334', NULL,       NULL,         N'Việt Nam', N'Tập đoàn Masan',    '0303576603', 0);

DECLARE @k int = 16;
WHILE @k <= 35
BEGIN
    DECLARE @makh varchar(10) = 'KH' + RIGHT('00000' + CAST(@k AS varchar), 5);
    INSERT INTO KhachHang (MaKH, TenKH, GioiTinh, NgaySinh, SDT, Email, MatKhau, DiaChi, LoaiKhachHang, CCCD, QuocTich, TrangThaiKhoa)
    VALUES (
        @makh,
        N'Hành Khách Mẫu ' + CAST(@k AS nvarchar),
        CASE WHEN @k % 2 = 0 THEN N'Nam' ELSE N'Nữ' END,
        DATEADD(YEAR, -(20 + (@k % 25)), '2026-01-01'),
        '0911000' + RIGHT('000' + CAST(@k AS varchar), 3),
        'khach' + CAST(@k AS varchar) + '@skyvn.vn',
        @SeedPwd,
        N'Quận ' + CAST((@k % 12) + 1 AS nvarchar) + N', TP. Hồ Chí Minh',
        N'Việt Nam',
        '07920000' + RIGHT('0000' + CAST(@k AS varchar), 4),
        N'Việt Nam',
        0
    );
    SET @k = @k + 1;
END

-- 10. Mã Khuyến Mãi (10 mã)
INSERT INTO KhuyenMai (MaKM, TenKM, PhanTramGiam, GiamToiDa, DonHangToiThieu, NgayBatDau, NgayKetThuc, SoLuong, TrangThai) VALUES
('HE2026',      N'Ưu Đãi Du Lịch Khám Phá 2026',       10.00, 500000,  1000000, '2026-01-01', '2027-06-30', 500,  1),
('VIPBAY',      N'Đặc Quyền Hạng Thương Gia VIP',      15.00, 1000000, 2000000, '2026-01-01', '2027-12-31', 200,  1),
('NEWFLYER',    N'Chào Mừng Thành Viên Mới SkyVN',      5.00, 200000,   500000, '2026-01-01', '2027-12-31', 1000, 1),
('TET2027',     N'Tết Sum Vầy 2027 - Đặt Vé Sớm',      12.00, 800000,  2500000, '2026-10-01', '2027-03-31', 400,  1),
('NOEL2026',    N'Giáng Sinh An Lành Tháng 12/2026',   10.00, 450000,  1200000, '2026-11-15', '2026-12-31', 300,  1),
('XUANSUMVAY',  N'Du Xuân Đinh Mùi 2027',              15.00, 900000,  2000000, '2026-11-01', '2027-02-28', 350,  1),
('STUDENTFLY',  N'Tiếp Bước Sinh Viên Việt Nam',        8.00, 300000,   800000, '2026-08-01', '2027-05-31', 500,  1),
('BLACKFRIDAY', N'Siêu Sale Black Friday Hàng Không',  20.00, 1500000, 2500000, '2026-11-01', '2026-12-05', 150,  1),
('AUTUMN2026',  N'Khám Phá Mùa Đông Tây Bắc',           7.00, 400000,  1000000, '2026-09-01', '2027-01-31', 400,  1),
('CORPFIRST',   N'Chiết Khấu Khách Hàng Doanh Nghiệp', 18.00, 2000000, 4000000, '2026-01-01', '2027-12-31', 200,  1);
GO

-- 11. Nạp 105 Đơn Đặt Vé Mẫu đa trạng thái (gồm Một chiều, Khứ hồi 1 PNR, Nối chuyến, Hóa đơn trong 72h & quá 72h)
DECLARE @b int = 1;
WHILE @b <= 105
BEGIN
    DECLARE @pnr varchar(15) = 'PNR' + RIGHT('00000' + CAST(@b AS varchar), 5);
    DECLARE @kh varchar(10) = 'KH' + RIGHT('00000' + CAST(((@b - 1) % 35) + 1 AS varchar), 5);
    DECLARE @cbOut varchar(20) = CASE
        WHEN @b = 1 THEN 'VN120'
        WHEN @b = 2 THEN 'VJ201'
        WHEN @b = 3 THEN '0V101'
        WHEN @b = 4 THEN 'VN120'
        WHEN @b = 5 THEN 'VJ452'
        ELSE (
            SELECT TOP 1 cb.MaChuyenBay
            FROM ChuyenBay cb
            JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
            JOIN SanBay sb1 ON tb.MaSanBayDi = sb1.MaSanBay
            JOIN SanBay sb2 ON tb.MaSanBayDen = sb2.MaSanBay
            WHERE cb.TrangThai = N'Đang mở bán'
              AND cb.SoGheTrong > 12
              AND cb.NgayGioBay > DATEADD(DAY, 3, GETDATE())
              AND sb1.QuocGia = N'Việt Nam' AND sb2.QuocGia = N'Việt Nam'
            ORDER BY NEWID()
        )
    END;
    DECLARE @mbOut varchar(15), @giaOut money;
    SELECT @mbOut = MaMayBay, @giaOut = GiaVeCoBan FROM ChuyenBay WHERE MaChuyenBay = @cbOut;

    -- Tìm 1 ghế còn trống thực sự cho chuyến bay này
    DECLARE @gheOut int = (
        SELECT TOP 1 g.MaGhe FROM GheNgoi g
        WHERE g.MaMayBay = @mbOut AND g.MaHangGhe = 'ECO' AND g.TrangThai = 1
          AND NOT EXISTS (SELECT 1 FROM VeMayBay v WHERE v.MaChuyenBay = @cbOut AND v.MaGhe = g.MaGhe AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in'))
        ORDER BY g.MaGhe
    );

    IF @gheOut IS NOT NULL
    BEGIN
        DECLARE @tenHK nvarchar(60);
        SELECT @tenHK = TenKH + CASE WHEN @b > 35 THEN N' #' + CAST(@b AS nvarchar) ELSE N'' END FROM KhachHang WHERE MaKH = @kh;
        DECLARE @docHK varchar(30) = '079206' + RIGHT('000000' + CAST(@b AS varchar), 6);

        DECLARE @ds ChiTietDatVe_Type;
        DELETE FROM @ds;

        DECLARE @loaiHT varchar(20) = CASE WHEN @b % 5 = 0 THEN 'KHU_HOI' ELSE 'MOT_CHIEU' END;
        DECLARE @thueOut money = 150000;
        INSERT INTO @ds VALUES ('DI', 1, @cbOut, @gheOut, 'ECO', @tenHK, '1998-01-01', @docHK, N'CCCD', @docHK, NULL, N'Việt Nam', N'Người lớn', NULL, @giaOut, @thueOut, @giaOut + @thueOut);

        -- Khứ hồi 1 PNR cho mỗi đơn chia hết cho 5
        IF @loaiHT = 'KHU_HOI'
        BEGIN
            DECLARE @outArr datetime = (SELECT NgayGioDenDK FROM ChuyenBay WHERE MaChuyenBay = @cbOut);
            DECLARE @cbRet varchar(20) = (
                SELECT TOP 1 cb.MaChuyenBay
                FROM ChuyenBay cb
                JOIN TuyenBay tb ON cb.MaTuyenBay = tb.MaTuyenBay
                WHERE cb.TrangThai = N'Đang mở bán' AND cb.SoGheTrong > 12
                  AND cb.NgayGioBay >= DATEADD(HOUR, 6, @outArr)
                  AND tb.MaTuyenBay = 'HAN-SGN'
                ORDER BY cb.NgayGioBay ASC
            );
            DECLARE @mbRet varchar(15), @giaRet money;
            SELECT @mbRet = MaMayBay, @giaRet = GiaVeCoBan FROM ChuyenBay WHERE MaChuyenBay = @cbRet;
            DECLARE @gheRet int = (
                SELECT TOP 1 g.MaGhe FROM GheNgoi g
                WHERE g.MaMayBay = @mbRet AND g.MaHangGhe = 'ECO' AND g.TrangThai = 1
                  AND NOT EXISTS (SELECT 1 FROM VeMayBay v WHERE v.MaChuyenBay = @cbRet AND v.MaGhe = g.MaGhe AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in'))
                ORDER BY g.MaGhe
            );
            IF @gheRet IS NOT NULL
                INSERT INTO @ds VALUES ('VE', 1, @cbRet, @gheRet, 'ECO', @tenHK, '1998-01-01', @docHK, N'CCCD', @docHK, NULL, N'Việt Nam', N'Người lớn', NULL, @giaRet, 150000, @giaRet + 150000);
            ELSE
                SET @loaiHT = 'MOT_CHIEU';
        END

        BEGIN TRY
            DECLARE @kmUse varchar(20) = CASE WHEN @b % 3 = 0 THEN 'HE2026' ELSE NULL END;
            EXEC sp_DatVeMayBay_Batch
                @MaDatCho = @pnr,
                @MaKH = @kh,
                @LoaiHanhTrinh = @loaiHT,
                @MaKM = @kmUse,
                @NguoiLienHe = @tenHK,
                @SDTLienHe = '0911000001',
                @EmailLienHe = 'booking@skyvn.vn',
                @GhiChu = N'Dữ liệu đơn đặt mẫu',
                @DSVe = @ds;

            -- Phân bổ trạng thái thực tế:
            -- PNR00001..00080: Đã thanh toán (một số quá hạn 72h VAT, số còn lại trong 72h VAT)
            -- PNR00081..00090: Đã thanh toán + Đã Check-in / Hoặc PNR00004 Chờ thanh toán
            -- PNR00091..00100: Hết hạn giữ chỗ / Đã hủy
            IF @b = 4 OR (@b BETWEEN 86 AND 92)
            BEGIN
                -- Để PNR00004 giữ trạng thái Chờ thanh toán với thời hạn 15 phút để test
                UPDATE PhieuDatCho SET HanThanhToan = DATEADD(MINUTE, 15, GETDATE()) WHERE MaDatCho = @pnr;
            END
            ELSE IF @b BETWEEN 93 AND 105
            BEGIN
                UPDATE PhieuDatCho SET TrangThai = N'Hết hạn giữ chỗ', HanThanhToan = DATEADD(MINUTE, -30, GETDATE()) WHERE MaDatCho = @pnr;
                UPDATE VeMayBay SET TrangThai = N'Đã hủy' WHERE MaDatCho = @pnr;
            END
            ELSE
            BEGIN
                DECLARE @hd varchar(15) = 'HD' + RIGHT('00000' + CAST(@b AS varchar), 5);
                DECLARE @pt nvarchar(50) = CASE (@b % 5)
                    WHEN 0 THEN N'VietQR'
                    WHEN 1 THEN N'Ví MoMo'
                    WHEN 2 THEN N'Ví ZaloPay'
                    WHEN 3 THEN N'Thẻ ATM nội địa'
                    ELSE N'VISA/MasterCard'
                END;
                EXEC sp_ThanhToanDonVe @MaHD = @hd, @MaDatCho = @pnr, @MaNV = NULL, @PhuongThuc = @pt, @MaGiaoDich = 'SEED_TXN_';

                -- PNR chia hết cho 10 mô phỏng hóa đơn đã thanh toán quá 72 giờ (96h trước) để test chặn xuất VAT
                IF @b % 10 = 0 AND @b <> 10
                    UPDATE HoaDon SET NgayThanhToan = DATEADD(HOUR, -96, GETDATE()), NgayLap = DATEADD(HOUR, -96, GETDATE()) WHERE MaDatCho = @pnr;

                -- Một số vé đã check-in sẵn
                IF @b IN (1, 2, 12, 15, 22)
                    UPDATE VeMayBay SET TrangThai = N'Đã Check-in' WHERE MaDatCho = @pnr;
            END
        END TRY
        BEGIN CATCH
            PRINT N'Seed loop skip @b=' + CAST(@b AS varchar) + N': ' + ERROR_MESSAGE();
        END CATCH
    END
    SET @b = @b + 1;
END
GO

-- Đánh giá mẫu
INSERT INTO DanhGiaChuyenBay (MaKH, MaChuyenBay, SoSao, NoiDung) VALUES
('KH00001', 'VN120', 5, N'Chuyến bay đúng giờ, tiếp viên chuyên nghiệp.'),
('KH00003', 'VJ201', 4, N'Giá vé hợp lý, check-in online rất nhanh.'),
('KH00008', '0V101', 5, N'Chuyến bay ra Côn Đảo cảnh đẹp tuyệt vời.');
GO

-- ====================================================================================================
-- PHẦN 8: RBAC SQL LOGINS & USERS (BẢO MẬT MỨC CSDL)
-- ====================================================================================================
IF NOT EXISTS(SELECT name FROM sys.server_principals WHERE name='airline_admin')   CREATE LOGIN airline_admin   WITH PASSWORD='AdminPassword@2026', CHECK_POLICY=OFF;
IF NOT EXISTS(SELECT name FROM sys.server_principals WHERE name='airline_thungan') CREATE LOGIN airline_thungan WITH PASSWORD='ThuNganPass@2026',  CHECK_POLICY=OFF;
IF NOT EXISTS(SELECT name FROM sys.server_principals WHERE name='airline_banve')   CREATE LOGIN airline_banve   WITH PASSWORD='BanVePass@2026',    CHECK_POLICY=OFF;
IF NOT EXISTS(SELECT name FROM sys.server_principals WHERE name='airline_cskh')    CREATE LOGIN airline_cskh    WITH PASSWORD='CSKHPass@2026',     CHECK_POLICY=OFF;
GO

CREATE USER airline_admin   FOR LOGIN airline_admin;
CREATE USER airline_thungan FOR LOGIN airline_thungan;
CREATE USER airline_banve   FOR LOGIN airline_banve;
CREATE USER airline_cskh    FOR LOGIN airline_cskh;

CREATE ROLE db_admin_role;
CREATE ROLE db_thungan_role;
CREATE ROLE db_banve_role;
CREATE ROLE db_cskh_role;

GRANT CONTROL ON DATABASE::QL_DatVeMayBay TO db_admin_role;
GRANT SELECT, INSERT, UPDATE ON HoaDon TO db_thungan_role;
GRANT SELECT, UPDATE ON PhieuDatCho TO db_thungan_role;
GRANT SELECT ON VeMayBay TO db_thungan_role;
GRANT EXECUTE ON sp_ThanhToanDonVe TO db_thungan_role;
GRANT EXECUTE ON sp_XuatHoaDonTrong72Gio TO db_thungan_role;
GRANT SELECT ON ChuyenBay TO db_banve_role;
GRANT SELECT ON GheNgoi TO db_banve_role;
GRANT SELECT, INSERT, UPDATE ON PhieuDatCho TO db_banve_role;
GRANT SELECT, INSERT, UPDATE ON VeMayBay TO db_banve_role;
GRANT EXECUTE ON sp_TimKiemChuyenBay TO db_banve_role;
GRANT EXECUTE ON sp_TimChuyenQuaCanh TO db_banve_role;
GRANT EXECUTE ON sp_DatVeMayBay_Batch TO db_banve_role;
GRANT SELECT ON KhachHang TO db_cskh_role;
GRANT SELECT, UPDATE ON VeMayBay TO db_cskh_role;
GRANT SELECT, INSERT, UPDATE ON YeuCauHuyDoiVe TO db_cskh_role;
GRANT EXECUTE ON sp_XuLyHuyVeVaHoanTien TO db_cskh_role;
GRANT EXECUTE ON sp_DoiVeMayBay TO db_cskh_role;

ALTER ROLE db_admin_role   ADD MEMBER airline_admin;
ALTER ROLE db_thungan_role ADD MEMBER airline_thungan;
ALTER ROLE db_banve_role   ADD MEMBER airline_banve;
ALTER ROLE db_cskh_role    ADD MEMBER airline_cskh;
GO

-- ====================================================================================================
-- PHẦN 9: KIỂM ĐẾM DỮ LIỆU ĐẦU RA (VERIFICATION)
-- ====================================================================================================
SELECT 'HangHangKhong'     AS TenBang, COUNT(*) AS SoBanGhi FROM HangHangKhong
UNION ALL SELECT 'SanBay',            COUNT(*) FROM SanBay
UNION ALL SELECT 'TuyenBay',          COUNT(*) FROM TuyenBay
UNION ALL SELECT 'MayBay',            COUNT(*) FROM MayBay
UNION ALL SELECT 'HangGhe',           COUNT(*) FROM HangGhe
UNION ALL SELECT 'GheNgoi',           COUNT(*) FROM GheNgoi
UNION ALL SELECT 'ChuyenBay',         COUNT(*) FROM ChuyenBay
UNION ALL SELECT 'DichVuBoSung',      COUNT(*) FROM DichVuBoSung
UNION ALL SELECT 'NhanVien',          COUNT(*) FROM NhanVien
UNION ALL SELECT 'KhachHang',         COUNT(*) FROM KhachHang
UNION ALL SELECT 'KhuyenMai',         COUNT(*) FROM KhuyenMai
UNION ALL SELECT 'PhieuDatCho',       COUNT(*) FROM PhieuDatCho
UNION ALL SELECT 'ChangBayDatCho',    COUNT(*) FROM ChangBayDatCho
UNION ALL SELECT 'VeMayBay',          COUNT(*) FROM VeMayBay
UNION ALL SELECT 'HoaDon',            COUNT(*) FROM HoaDon
UNION ALL SELECT 'DanhGiaChuyenBay',  COUNT(*) FROM DanhGiaChuyenBay;
GO
