using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Hubs;
using System.Data;

namespace QL_DatVeMayBay.Services
{
    public class SeatHoldCleanupService : BackgroundService
    {
        private readonly string _connStr;
        private readonly IHubContext<SeatHub> _hubContext;
        private readonly ILogger<SeatHoldCleanupService> _logger;

        public SeatHoldCleanupService(
            IConfiguration config,
            IHubContext<SeatHub> hubContext,
            ILogger<SeatHoldCleanupService> logger)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ReleaseExpiredBookingsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi trong SeatHoldCleanupService khi quét đơn giữ chỗ quá hạn.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ReleaseExpiredBookingsAsync(CancellationToken ct)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync(ct);

            var released = new List<(string MaCB, int MaGhe, string MaDatCho)>();

            // SP thực hiện atomic UPDATE trong transaction và trả về đúng tập ghế vừa được nhả
            using (var spCmd = new SqlCommand("sp_TuDongHuyDonGiuChoHetHan_Cursor", conn))
            {
                spCmd.CommandType = CommandType.StoredProcedure;
                using var reader = await spCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    released.Add((reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
                }
            }

            if (released.Count == 0) return;

            _logger.LogInformation("Đã giải phóng {Count} ghế từ các đơn hết hạn 15 phút: {PNRs}",
                released.Count,
                string.Join(", ", released.Select(x => x.MaDatCho).Distinct()));

            foreach (var group in released.GroupBy(x => x.MaCB))
            {
                await _hubContext.Clients.Group($"FLIGHT_{group.Key}")
                    .SendAsync("SeatStatusChanged", new
                    {
                        maChuyenBay = group.Key,
                        seatIds = group.Select(x => x.MaGhe).ToList(),
                        isOccupied = false,
                        seatState = "available",
                        message = "Một số ghế hết hạn giữ chỗ 15 phút đã trống trở lại!"
                    }, ct);
            }

            foreach (var pnr in released.Select(x => x.MaDatCho).Distinct())
            {
                await _hubContext.Clients.Group($"PAYMENT_{pnr}")
                    .SendAsync("PaymentStatusUpdated", new
                    {
                        maDatCho = pnr,
                        status = "Hết hạn giữ chỗ",
                        message = "Phiên giữ chỗ 15 phút đã hết hạn."
                    }, ct);
            }
        }
    }
}
