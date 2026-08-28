using QL_DatVeMayBay.BLL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QL_DatVeMayBay.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ChuyenBayBLL _chuyenBayBLL;

        public MainWindow()
        {
            InitializeComponent();
            _chuyenBayBLL = new ChuyenBayBLL();

            LoadDanhSachChuyenBay();
        }

        private void LoadDanhSachChuyenBay()
        {
            try
            {
                dgvChuyenBay.ItemsSource = _chuyenBayBLL.LayDanhSachChuyenBay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tải dữ liệu: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnTimKiem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string maDi = string.IsNullOrWhiteSpace(txtMaSBDi.Text) ? null : txtMaSBDi.Text.Trim();
                string maDen = string.IsNullOrWhiteSpace(txtMaSBDen.Text) ? null : txtMaSBDen.Text.Trim();
                DateTime? ngayBay = dpNgayBay.SelectedDate;

                var ketQua = _chuyenBayBLL.TimKiemChuyenBay(maDi, maDen, ngayBay);

                dgvChuyenBay.ItemsSource = ketQua;

                if (ketQua == null || ketQua.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy chuyến bay phù hợp!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tìm kiếm: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            txtMaSBDi.Text = string.Empty;
            txtMaSBDen.Text = string.Empty;
            dpNgayBay.SelectedDate = null;

            LoadDanhSachChuyenBay();
        }
    }
}
