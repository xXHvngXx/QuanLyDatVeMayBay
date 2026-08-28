using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using QL_DatVeMayBay.BLL;
using QL_DatVeMayBay.DAL;
using QL_DatVeMayBay.DTO;

namespace QL_DatVeMayBay.GUI.ViewModels
{
    public class ChuyenBayViewModel : BaseViewModel
    {
        private readonly ChuyenBayBLL _chuyenBayBLL;
        private readonly SanBayBLL _sanBayBLL;

        #region Properties Binding Giao Diện

        private ObservableCollection<ChuyenBayViewDTO> _danhSachChuyenBay;
        public ObservableCollection<ChuyenBayViewDTO> DanhSachChuyenBay
        {
            get => _danhSachChuyenBay;
            set { _danhSachChuyenBay = value; OnPropertyChanged(); }
        }

        private List<SanBay> _danhSachSanBay;
        public List<SanBay> DanhSachSanBay
        {
            get => _danhSachSanBay;
            set { _danhSachSanBay = value; OnPropertyChanged(); }
        }

        // Điều kiện tìm kiếm
        private string _selectedSanBayDi;
        public string SelectedSanBayDi
        {
            get => _selectedSanBayDi;
            set { _selectedSanBayDi = value; OnPropertyChanged(); }
        }

        private string _selectedSanBayDen;
        public string SelectedSanBayDen
        {
            get => _selectedSanBayDen;
            set { _selectedSanBayDen = value; OnPropertyChanged(); }
        }

        private DateTime? _ngayBayChon;
        public DateTime? NgayBayChon
        {
            get => _ngayBayChon;
            set { _ngayBayChon = value; OnPropertyChanged(); }
        }

        // Dòng được chọn trên DataGrid
        private ChuyenBayViewDTO _selectedChuyenBay;
        public ChuyenBayViewDTO SelectedChuyenBay
        {
            get => _selectedChuyenBay;
            set { _selectedChuyenBay = value; OnPropertyChanged(); }
        }

        #endregion

        #region Commands

        public ICommand TimKiemCommand { get; set; }
        public ICommand RefreshCommand { get; set; }
        public ICommand HuyChuyenCommand { get; set; }

        #endregion

        public ChuyenBayViewModel()
        {
            _chuyenBayBLL = new ChuyenBayBLL();
            _sanBayBLL = new SanBayBLL();

            // Load dữ liệu ban đầu
            LoadDanhSachSanBay();
            LoadDanhSachChuyenBay();

            // Khởi tạo Command
            TimKiemCommand = new RelayCommand<object>((p) => true, (p) => ThucHienTimKiem());
            RefreshCommand = new RelayCommand<object>((p) => true, (p) => ThucHienLamMoi());
            HuyChuyenCommand = new RelayCommand<object>((p) => SelectedChuyenBay != null, (p) => ThucHienHuyChuyen());
        }

        private void LoadDanhSachSanBay()
        {
            DanhSachSanBay = _sanBayBLL.LayDanhSachSanBay();
        }

        private void LoadDanhSachChuyenBay()
        {
            var list = _chuyenBayBLL.LayDanhSachChuyenBay();
            DanhSachChuyenBay = new ObservableCollection<ChuyenBayViewDTO>(list);
        }

        private void ThucHienTimKiem()
        {
            var list = _chuyenBayBLL.TimKiemChuyenBay(SelectedSanBayDi, SelectedSanBayDen, NgayBayChon);
            DanhSachChuyenBay = new ObservableCollection<ChuyenBayViewDTO>(list);
        }

        private void ThucHienLamMoi()
        {
            SelectedSanBayDi = null;
            SelectedSanBayDen = null;
            NgayBayChon = null;
            SelectedChuyenBay = null;
            LoadDanhSachChuyenBay();
        }

        private void ThucHienHuyChuyen()
        {
            if (SelectedChuyenBay == null) return;

            var result = MessageBox.Show($"Bạn có chắc chắn muốn hủy chuyến bay {SelectedChuyenBay.MaChuyenBay}?",
                                         "Xác nhận hủy chuyến",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                if (_chuyenBayBLL.CapNhatTrangThai(SelectedChuyenBay.MaChuyenBay, "Đã hủy", out string err))
                {
                    MessageBox.Show("Đã hủy chuyến bay thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadDanhSachChuyenBay();
                }
                else
                {
                    MessageBox.Show(err, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}