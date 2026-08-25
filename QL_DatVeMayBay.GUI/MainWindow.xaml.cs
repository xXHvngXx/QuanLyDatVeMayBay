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
        private ChuyenBayBLL chuyenBayBLL = new ChuyenBayBLL();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            dgvChuyenBay.ItemsSource = chuyenBayBLL.LayDanhSachChuyenBay();
        }

        private void btnTimKiem_Click(object sender, RoutedEventArgs e)
        {
            string maDi = txtMaSBDi.Text.Trim();
            string maDen = txtMaSBDen.Text.Trim();
            DateTime? ngayBay = dpNgayBay.SelectedDate;

            dgvChuyenBay.ItemsSource = chuyenBayBLL.TimKiemChuyenBay(maDi, maDen, ngayBay);
        }

        private void btnLamMoi_Click(object sender, RoutedEventArgs e)
        {
            txtMaSBDi.Clear();
            txtMaSBDen.Clear();
            dpNgayBay.SelectedDate = null;
            LoadData();
        }
    }
}
