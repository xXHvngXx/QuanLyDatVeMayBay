using Microsoft.AspNetCore.SignalR;

namespace QL_DatVeMayBay.Hubs
{
    public class SeatHub : Hub
    {
        public async Task JoinFlightRoom(string maChuyenBay)
        {
            if (!string.IsNullOrWhiteSpace(maChuyenBay))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"FLIGHT_{maChuyenBay.Trim().ToUpperInvariant()}");
            }
        }

        public async Task JoinBookingPaymentRoom(string maDatCho)
        {
            if (!string.IsNullOrWhiteSpace(maDatCho))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"PAYMENT_{maDatCho.Trim().ToUpperInvariant()}");
            }
        }
    }
}
