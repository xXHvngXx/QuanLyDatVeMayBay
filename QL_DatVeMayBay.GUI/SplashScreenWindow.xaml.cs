using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace QL_DatVeMayBay.GUI
{
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();

            // Đồng bộ % trực tiếp theo tiến trình của ProgressBar
            pbLoading.ValueChanged += PbLoading_ValueChanged;

            StartSmoothLoading();
        }

        private void PbLoading_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int percent = (int)e.NewValue;
            lblPercent.Text = $"{percent}%";

            if (percent < 30)
                lblStatus.Text = "Đang kết nối Cơ sở dữ liệu SQL Server...";
            else if (percent < 65)
                lblStatus.Text = "Đang tải danh sách tuyến bay & sân bay...";
            else if (percent < 90)
                lblStatus.Text = "Đang kiểm tra cấu hình hệ thống...";
            else
                lblStatus.Text = "Hoàn tất! Đang chuẩn bị vào ứng dụng...";
        }

        private async void StartSmoothLoading()
        {
            int totalDurationMs = 3200; // 3.2 giây

            DoubleAnimation pbAnimation = new DoubleAnimation
            {
                From = 0,
                To = 100,
                Duration = TimeSpan.FromMilliseconds(totalDurationMs),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            pbLoading.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, pbAnimation);

            await Task.Delay(totalDurationMs + 300);

            MainWindow main = new MainWindow();
            main.Show();
            this.Close();
        }
    }
}