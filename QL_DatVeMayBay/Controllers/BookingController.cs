using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using QL_DatVeMayBay.Hubs;
using QL_DatVeMayBay.Models;
using QL_DatVeMayBay.Services;
using System.Data;

namespace QL_DatVeMayBay.Controllers
{
    public class BookingController : Controller
    {
        private readonly string _connStr;
        private readonly IHubContext<SeatHub> _hubContext;
        private readonly IPaymentGateway _paymentGateway;

        public BookingController(
            IConfiguration config,
            IHubContext<SeatHub> hubContext,
            IPaymentGateway paymentGateway)
        {
            _connStr = config.GetConnectionString("DefaultConnection")!;
            _hubContext = hubContext;
            _paymentGateway = paymentGateway;
        }

        private string CurrentCustomerId()
        {
            if (HttpContext.Session.GetString("UserType") == "KH")
                return HttpContext.Session.GetString("UserId") ?? HttpContext.Session.GetString("MaKH") ?? "KH00001";
            return "KH00001";
        }

        // =========================================================================
        // 1A. QUY TRÌNH VIETJET BƯỚC 2: ĐIỀN THÔNG TIN HÀNH KHÁCH
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Passengers(
            string maChuyenBayDi,
            string hangGheDi = "ECO",
            string? maChuyenBayDi2 = null,
            string? maChuyenBayVe = null,
            string hangGheVe = "ECO",
            string? maChuyenBayVe2 = null,
            int nguoiLon = 1,
            int treEm = 0,
            int emBe = 0,
            string? maKM = null)
        {
            if (string.IsNullOrWhiteSpace(maChuyenBayDi))
                return RedirectToAction("Index", "Home");

            nguoiLon = Math.Clamp(nguoiLon, 1, 9);
            treEm = Math.Clamp(treEm, 0, Math.Max(0, 9 - nguoiLon));
            emBe = Math.Clamp(emBe, 0, Math.Min(nguoiLon, Math.Max(0, 9 - nguoiLon - treEm)));

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var loaded = await LoadItineraryLegsAsync(conn, maChuyenBayDi, hangGheDi, maChuyenBayDi2, maChuyenBayVe, hangGheVe, maChuyenBayVe2);
            if (loaded.Legs.Count == 0)
            {
                TempData["Error"] = "Không tìm thấy chuyến bay đã chọn.";
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Legs = loaded.Legs;
            ViewBag.IsInternational = loaded.IsIntl;
            ViewBag.TaxPerSeatedPax = loaded.TaxPerSeatedPax;
            ViewBag.EstimatedFareTotal = CalculateEstimatedFareTotal(loaded.Legs, nguoiLon, treEm, emBe);
            ViewBag.EstimatedTaxTotal = loaded.TaxPerSeatedPax * (nguoiLon + treEm);
            ViewBag.MaChuyenBayDi = maChuyenBayDi;
            ViewBag.HangGheDi = hangGheDi;
            ViewBag.MaChuyenBayDi2 = maChuyenBayDi2;
            ViewBag.MaChuyenBayVe = maChuyenBayVe;
            ViewBag.HangGheVe = hangGheVe;
            ViewBag.MaChuyenBayVe2 = maChuyenBayVe2;
            ViewBag.NguoiLon = nguoiLon;
            ViewBag.TreEm = treEm;
            ViewBag.EmBe = emBe;
            ViewBag.MaKM = maKM ?? "";
            ViewBag.DefaultContactName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.DefaultContactEmail = HttpContext.Session.GetString("UserEmail") ?? "";
            ViewBag.DefaultContactPhone = HttpContext.Session.GetString("UserSDT") ?? "";

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Passengers(
            string maChuyenBayDi,
            string hangGheDi,
            string? maChuyenBayDi2,
            string? maChuyenBayVe,
            string hangGheVe,
            string? maChuyenBayVe2,
            string? maKM,
            string nguoiLienHe,
            string sdtLienHe,
            string emailLienHe,
            List<PassengerInputModel> passengers)
        {
            int nl = passengers?.Count(p => p.LoaiHanhKhach == "Người lớn") ?? 0;
            int te = passengers?.Count(p => p.LoaiHanhKhach == "Trẻ em") ?? 0;
            int eb = passengers?.Count(p => p.LoaiHanhKhach == "Em bé") ?? 0;

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            var loaded = await LoadItineraryLegsAsync(conn, maChuyenBayDi, hangGheDi, maChuyenBayDi2, maChuyenBayVe, hangGheVe, maChuyenBayVe2);
            if (loaded.Legs.Count == 0)
            {
                TempData["Error"] = "Chuyến bay không hợp lệ.";
                return RedirectToAction("Index", "Home");
            }

            string? err = passengers == null
                ? "Vui lòng nhập thông tin hành khách."
                : PassengerDocumentValidator.ValidatePassengers(passengers, loaded.MinFlightDate, loaded.MaxFlightDate, loaded.IsIntl);

            if (err != null)
            {
                TempData["Error"] = err;
                return RedirectToAction("Passengers", new { maChuyenBayDi, hangGheDi, maChuyenBayDi2, maChuyenBayVe, hangGheVe, maChuyenBayVe2, nguoiLon = Math.Max(1, nl), treEm = te, emBe = eb, maKM });
            }

            // Kiểm tra trước khi sang Bước 3 xem CCCD/Hộ chiếu đã có vé hiệu lực trên chuyến bay chưa để tránh kẹt ở màn hình chọn ghế
            var legList = new[] { maChuyenBayDi, maChuyenBayDi2, maChuyenBayVe, maChuyenBayVe2 }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            foreach (var p in passengers!.Where(x => x.LoaiHanhKhach != "Em bé" && !string.IsNullOrWhiteSpace(x.SoGiayTo)))
            {
                string doc = p.SoGiayTo.Trim().ToUpperInvariant();
                foreach (var cbCode in legList)
                {
                    using var cmdDup = new SqlCommand(@"
                        SELECT TOP 1 MaDatCho, TrangThai
                        FROM VeMayBay
                        WHERE MaChuyenBay = @CB AND UPPER(SoGiayTo) = @Doc
                          AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in');", conn);
                    cmdDup.Parameters.AddWithValue("@CB", cbCode);
                    cmdDup.Parameters.AddWithValue("@Doc", doc);
                    using var rdDup = await cmdDup.ExecuteReaderAsync();
                    if (await rdDup.ReadAsync())
                    {
                        string existingPnr = rdDup.GetString(0);
                        string existingSt = rdDup.GetString(1);
                        TempData["Error"] = $"Hành khách {p.TenHanhKhach.Trim()} ({p.LoaiGiayTo}: {doc}) đã có vé '{existingSt}' (Mã PNR: {existingPnr}) trên chuyến {cbCode}. Vui lòng nhập số giấy tờ khác hoặc kiểm tra hồ sơ PNR {existingPnr}!";
                        return RedirectToAction("Passengers", new { maChuyenBayDi, hangGheDi, maChuyenBayDi2, maChuyenBayVe, hangGheVe, maChuyenBayVe2, nguoiLon = Math.Max(1, nl), treEm = te, emBe = eb, maKM });
                    }
                }
            }

            var draft = new BookingDraftModel
            {
                MaChuyenBayDi = maChuyenBayDi.Trim().ToUpperInvariant(),
                HangGheDi = string.IsNullOrWhiteSpace(hangGheDi) ? "ECO" : hangGheDi,
                MaChuyenBayDi2 = string.IsNullOrWhiteSpace(maChuyenBayDi2) ? null : maChuyenBayDi2.Trim().ToUpperInvariant(),
                MaChuyenBayVe = string.IsNullOrWhiteSpace(maChuyenBayVe) ? null : maChuyenBayVe.Trim().ToUpperInvariant(),
                HangGheVe = string.IsNullOrWhiteSpace(hangGheVe) ? "ECO" : hangGheVe,
                MaChuyenBayVe2 = string.IsNullOrWhiteSpace(maChuyenBayVe2) ? null : maChuyenBayVe2.Trim().ToUpperInvariant(),
                NguoiLon = nl,
                TreEm = te,
                EmBe = eb,
                MaKM = string.IsNullOrWhiteSpace(maKM) ? null : maKM.Trim().ToUpperInvariant(),
                NguoiLienHe = string.IsNullOrWhiteSpace(nguoiLienHe) ? passengers![0].TenHanhKhach.Trim() : nguoiLienHe.Trim(),
                SdtLienHe = string.IsNullOrWhiteSpace(sdtLienHe) ? "0900000000" : sdtLienHe.Trim(),
                EmailLienHe = string.IsNullOrWhiteSpace(emailLienHe) ? "guest@skyvn.vn" : emailLienHe.Trim(),
                Passengers = passengers!
            };

            HttpContext.Session.SetString("BookingDraft", System.Text.Json.JsonSerializer.Serialize(draft));
            return RedirectToAction("SelectServices");
        }

        // =========================================================================
        // 1B. QUY TRÌNH VIETJET BƯỚC 3: CHỌN CHỖ NGỒI, HÀNH LÝ/NỐI CHUYẾN, SUẤT ĂN
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> SelectServices()
        {
            string? json = HttpContext.Session.GetString("BookingDraft");
            if (string.IsNullOrWhiteSpace(json))
            {
                TempData["Error"] = "Phiên đặt vé đã hết hoặc chưa nhập thông tin hành khách.";
                return RedirectToAction("Index", "Home");
            }

            var draft = System.Text.Json.JsonSerializer.Deserialize<BookingDraftModel>(json)!;
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var loaded = await LoadItineraryLegsAsync(conn, draft.MaChuyenBayDi, draft.HangGheDi, draft.MaChuyenBayDi2, draft.MaChuyenBayVe, draft.HangGheVe, draft.MaChuyenBayVe2);
            foreach (var leg in loaded.Legs)
            {
                leg["Seats"] = await GetSeatsForFlightAsync(conn, leg["MaChuyenBay"].ToString()!);
            }

            var allServices = await GetAddonServicesAsync(conn);
            // Bỏ Dịch vụ Taxi / Dịch vụ theo hành khách / Mang theo thú cưng
            var baggageAndTransit = allServices.Where(s => s["LoaiDV"].ToString() is "Hành lý" or "Ưu tiên" or "Phòng chờ").ToList();
            var meals = allServices.Where(s => s["LoaiDV"].ToString() == "Suất ăn").ToList();

            ViewBag.Draft = draft;
            ViewBag.Legs = loaded.Legs;
            ViewBag.EstimatedFareTotal = CalculateEstimatedFareTotal(loaded.Legs, draft.NguoiLon, draft.TreEm, draft.EmBe);
            ViewBag.EstimatedTaxTotal = loaded.TaxPerSeatedPax * draft.NguoiLon + loaded.TaxPerChildPax * draft.TreEm;
            ViewBag.BaggageAndTransitServices = baggageAndTransit;
            ViewBag.MealServices = meals;
            ViewBag.SoGheCanChon = draft.NguoiLon + draft.TreEm;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmServicesAndHold(
            List<int>? gheDiIds,
            List<int>? gheDi2Ids,
            List<int>? gheVeIds,
            List<int>? gheVe2Ids,
            List<string>? selectedServiceIds)
        {
            string? json = HttpContext.Session.GetString("BookingDraft");
            if (string.IsNullOrWhiteSpace(json))
            {
                TempData["Error"] = "Phiên đặt vé đã hết. Vui lòng thực hiện lại từ đầu.";
                return RedirectToAction("Index", "Home");
            }

            var draft = System.Text.Json.JsonSerializer.Deserialize<BookingDraftModel>(json)!;
            int seatedCount = draft.NguoiLon + draft.TreEm;

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            gheDiIds = await ResolveSeatSelectionAsync(conn, draft.MaChuyenBayDi, draft.HangGheDi, gheDiIds, seatedCount);
            if (!string.IsNullOrWhiteSpace(draft.MaChuyenBayDi2))
                gheDi2Ids = await ResolveSeatSelectionAsync(conn, draft.MaChuyenBayDi2, draft.HangGheDi, gheDi2Ids, seatedCount);
            if (!string.IsNullOrWhiteSpace(draft.MaChuyenBayVe))
                gheVeIds = await ResolveSeatSelectionAsync(conn, draft.MaChuyenBayVe, draft.HangGheVe, gheVeIds, seatedCount);
            if (!string.IsNullOrWhiteSpace(draft.MaChuyenBayVe2))
                gheVe2Ids = await ResolveSeatSelectionAsync(conn, draft.MaChuyenBayVe2, draft.HangGheVe, gheVe2Ids, seatedCount);

            if (gheDiIds.Count != seatedCount ||
                (!string.IsNullOrWhiteSpace(draft.MaChuyenBayDi2) && gheDi2Ids!.Count != seatedCount) ||
                (!string.IsNullOrWhiteSpace(draft.MaChuyenBayVe) && gheVeIds!.Count != seatedCount) ||
                (!string.IsNullOrWhiteSpace(draft.MaChuyenBayVe2) && gheVe2Ids!.Count != seatedCount))
            {
                TempData["Error"] = "Một số chặng bay không còn đủ ghế trống cho hạng vé đã chọn. Vui lòng chọn lại!";
                return RedirectToAction("SelectServices");
            }

            return await ExecuteUnifiedHoldAndRedirectAsync(
                conn,
                draft.MaChuyenBayDi,
                draft.MaChuyenBayDi2,
                draft.MaChuyenBayVe,
                draft.MaChuyenBayVe2,
                gheDiIds,
                gheDi2Ids,
                gheVeIds,
                gheVe2Ids,
                draft.Passengers,
                draft.NguoiLienHe,
                draft.SdtLienHe,
                draft.EmailLienHe,
                selectedServiceIds,
                draft.MaKM,
                "SelectServices",
                null);
        }

        // =========================================================================
        // 1. CHỌN GHẾ & NHẬP HÀNH KHÁCH (GIỮ TƯƠNG THÍCH TRỰC TIẾP)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> SelectSeats(
            string maChuyenBayDi,
            string? maChuyenBayDi2 = null,
            string? maChuyenBayVe = null,
            string? maChuyenBayVe2 = null,
            int nguoiLon = 1,
            int treEm = 0,
            int emBe = 0,
            int? soHanhKhach = null)
        {
            if (string.IsNullOrWhiteSpace(maChuyenBayDi))
                return RedirectToAction("Index", "Home");

            if (soHanhKhach.HasValue && nguoiLon == 1 && treEm == 0 && emBe == 0)
                nguoiLon = Math.Clamp(soHanhKhach.Value, 1, 9);

            nguoiLon = Math.Clamp(nguoiLon, 1, 9);
            treEm = Math.Clamp(treEm, 0, Math.Max(0, 9 - nguoiLon));
            emBe = Math.Clamp(emBe, 0, Math.Min(nguoiLon, Math.Max(0, 9 - nguoiLon - treEm)));

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var legCodes = new List<(string HuongBay, int ThuTu, string MaCB, string Label)>
            {
                ("DI", 1, maChuyenBayDi.Trim().ToUpperInvariant(), "Lượt đi • Chặng 1")
            };
            if (!string.IsNullOrWhiteSpace(maChuyenBayDi2))
                legCodes.Add(("DI", 2, maChuyenBayDi2.Trim().ToUpperInvariant(), "Lượt đi • Chặng nối chuyến 2"));
            if (!string.IsNullOrWhiteSpace(maChuyenBayVe))
                legCodes.Add(("VE", 1, maChuyenBayVe.Trim().ToUpperInvariant(), "Lượt về • Chặng 1"));
            if (!string.IsNullOrWhiteSpace(maChuyenBayVe2))
                legCodes.Add(("VE", 2, maChuyenBayVe2.Trim().ToUpperInvariant(), "Lượt về • Chặng nối chuyến 2"));

            var legs = new List<Dictionary<string, object>>();
            bool isIntl = false;
            decimal taxPerSeatedPax = 0m;

            foreach (var l in legCodes)
            {
                var detail = await GetFlightDetailAsync(conn, l.MaCB);
                if (detail.Count == 0)
                {
                    TempData["Error"] = $"Không tìm thấy chuyến bay {l.MaCB}.";
                    return RedirectToAction("Index", "Home");
                }
                bool legIntl = detail.ContainsKey("LaTuyenQuocTe") && Convert.ToBoolean(detail["LaTuyenQuocTe"]);
                if (legIntl) isIntl = true;
                taxPerSeatedPax += FlightPricingHelper.AdultTax(legIntl);

                detail["HuongBay"] = l.HuongBay;
                detail["ThuTuChang"] = l.ThuTu;
                detail["LegLabel"] = l.Label;
                detail["Seats"] = await GetSeatsForFlightAsync(conn, l.MaCB);
                legs.Add(detail);
            }

            ViewBag.Legs = legs;
            ViewBag.IsInternational = isIntl;
            ViewBag.TaxPerSeatedPax = taxPerSeatedPax;
            ViewBag.TaxPerChildPax = legs.Sum(l => FlightPricingHelper.ChildTax(l.ContainsKey("LaTuyenQuocTe") && Convert.ToBoolean(l["LaTuyenQuocTe"])));
            ViewBag.ChuyenDi = legs[0];
            ViewBag.GheChuyenDi = legs[0]["Seats"];
            ViewBag.MaChuyenBayDi = maChuyenBayDi;
            ViewBag.MaChuyenBayDi2 = maChuyenBayDi2;
            ViewBag.MaChuyenBayVe = maChuyenBayVe;
            ViewBag.MaChuyenBayVe2 = maChuyenBayVe2;
            ViewBag.DichVuBoSung = await GetAddonServicesAsync(conn);
            ViewBag.NguoiLon = nguoiLon;
            ViewBag.TreEm = treEm;
            ViewBag.EmBe = emBe;
            ViewBag.SoGheCanChon = nguoiLon + treEm;
            ViewBag.TongHanhKhach = nguoiLon + treEm + emBe;
            ViewBag.SoHanhKhach = nguoiLon + treEm;
            ViewBag.DefaultContactName = HttpContext.Session.GetString("UserName") ?? "";
            ViewBag.DefaultContactEmail = HttpContext.Session.GetString("UserEmail") ?? "";
            ViewBag.DefaultContactPhone = HttpContext.Session.GetString("UserSDT") ?? "";

            return View();
        }

        // =========================================================================
        // 2. GIỮ GHẾ 15 PHÚT ATOMIC (1 PNR DUY NHẤT) + SIGNALR REALTIME
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HoldSeatsAndProceed(
            string maChuyenBayDi,
            string? maChuyenBayDi2,
            string? maChuyenBayVe,
            string? maChuyenBayVe2,
            List<int> gheDiIds,
            List<int>? gheDi2Ids,
            List<int>? gheVeIds,
            List<int>? gheVe2Ids,
            List<PassengerInputModel> passengers,
            string nguoiLienHe,
            string sdtLienHe,
            string emailLienHe,
            List<string>? selectedServiceIds,
            string? maKM)
        {
            int nl = passengers?.Count(p => p.LoaiHanhKhach == "Người lớn") ?? 0;
            int te = passengers?.Count(p => p.LoaiHanhKhach == "Trẻ em") ?? 0;
            int eb = passengers?.Count(p => p.LoaiHanhKhach == "Em bé") ?? 0;
            int seatedCount = nl + te;

            object routeBack = new
            {
                maChuyenBayDi,
                maChuyenBayDi2,
                maChuyenBayVe,
                maChuyenBayVe2,
                nguoiLon = Math.Max(1, nl),
                treEm = te,
                emBe = eb
            };

            if (passengers == null || passengers.Count == 0 || seatedCount < 1)
            {
                TempData["Error"] = "Thông tin hành khách không hợp lệ (cần tối thiểu 1 Người lớn).";
                return RedirectToAction("SelectSeats", routeBack);
            }

            if (gheDiIds == null || gheDiIds.Count != seatedCount)
            {
                TempData["Error"] = $"Vui lòng chọn đúng {seatedCount} ghế cho chặng lượt đi ({maChuyenBayDi}).";
                return RedirectToAction("SelectSeats", routeBack);
            }
            if (!string.IsNullOrWhiteSpace(maChuyenBayDi2) && (gheDi2Ids == null || gheDi2Ids.Count != seatedCount))
            {
                TempData["Error"] = $"Vui lòng chọn đúng {seatedCount} ghế cho chặng nối chuyến lượt đi ({maChuyenBayDi2}).";
                return RedirectToAction("SelectSeats", routeBack);
            }
            if (!string.IsNullOrWhiteSpace(maChuyenBayVe) && (gheVeIds == null || gheVeIds.Count != seatedCount))
            {
                TempData["Error"] = $"Vui lòng chọn đúng {seatedCount} ghế cho chặng lượt về ({maChuyenBayVe}).";
                return RedirectToAction("SelectSeats", routeBack);
            }
            if (!string.IsNullOrWhiteSpace(maChuyenBayVe2) && (gheVe2Ids == null || gheVe2Ids.Count != seatedCount))
            {
                TempData["Error"] = $"Vui lòng chọn đúng {seatedCount} ghế cho chặng nối chuyến lượt về ({maChuyenBayVe2}).";
                return RedirectToAction("SelectSeats", routeBack);
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            return await ExecuteUnifiedHoldAndRedirectAsync(
                conn,
                maChuyenBayDi,
                maChuyenBayDi2,
                maChuyenBayVe,
                maChuyenBayVe2,
                gheDiIds,
                gheDi2Ids,
                gheVeIds,
                gheVe2Ids,
                passengers,
                nguoiLienHe,
                sdtLienHe,
                emailLienHe,
                selectedServiceIds,
                maKM,
                "SelectSeats",
                routeBack);
        }

        private async Task<IActionResult> ExecuteUnifiedHoldAndRedirectAsync(
            SqlConnection conn,
            string maChuyenBayDi,
            string? maChuyenBayDi2,
            string? maChuyenBayVe,
            string? maChuyenBayVe2,
            List<int> gheDiIds,
            List<int>? gheDi2Ids,
            List<int>? gheVeIds,
            List<int>? gheVe2Ids,
            List<PassengerInputModel> passengers,
            string nguoiLienHe,
            string sdtLienHe,
            string emailLienHe,
            List<string>? selectedServiceIds,
            string? maKM,
            string failureAction,
            object? failureRoute)
        {
            var legInputs = new List<(string HuongBay, int ThuTu, string MaCB, List<int> SeatIds)>
            {
                ("DI", 1, maChuyenBayDi.Trim().ToUpperInvariant(), gheDiIds)
            };
            if (!string.IsNullOrWhiteSpace(maChuyenBayDi2))
                legInputs.Add(("DI", 2, maChuyenBayDi2.Trim().ToUpperInvariant(), gheDi2Ids!));
            if (!string.IsNullOrWhiteSpace(maChuyenBayVe))
                legInputs.Add(("VE", 1, maChuyenBayVe.Trim().ToUpperInvariant(), gheVeIds!));
            if (!string.IsNullOrWhiteSpace(maChuyenBayVe2))
                legInputs.Add(("VE", 2, maChuyenBayVe2.Trim().ToUpperInvariant(), gheVe2Ids!));

            var legDetails = new Dictionary<string, Dictionary<string, object>>();
            DateTime minFlightDate = DateTime.MaxValue;
            DateTime maxFlightDate = DateTime.MinValue;
            bool isIntl = false;

            foreach (var leg in legInputs)
            {
                var d = await GetFlightDetailAsync(conn, leg.MaCB);
                if (d.Count == 0)
                {
                    TempData["Error"] = $"Chuyến bay {leg.MaCB} không tồn tại.";
                    return RedirectToAction(failureAction, failureRoute);
                }
                DateTime depart = Convert.ToDateTime(d["NgayGioBay"]);
                if (depart < minFlightDate) minFlightDate = depart;
                if (depart > maxFlightDate) maxFlightDate = depart;
                if (d.ContainsKey("LaTuyenQuocTe") && Convert.ToBoolean(d["LaTuyenQuocTe"]))
                    isIntl = true;
                legDetails[leg.MaCB] = d;
            }

            string? docErr = PassengerDocumentValidator.ValidatePassengers(passengers, minFlightDate, maxFlightDate, isIntl);
            if (docErr != null)
            {
                TempData["Error"] = docErr;
                return RedirectToAction(failureAction, failureRoute);
            }

            var tvp = BuildUnifiedTicketTable();
            var adultNames = passengers.Where(p => p.LoaiHanhKhach == "Người lớn").Select(p => p.TenHanhKhach.Trim()).ToList();

            foreach (var leg in legInputs)
            {
                var info = legDetails[leg.MaCB];
                decimal giaCoBanChuyen = Convert.ToDecimal(info["GiaVeCoBan"]);
                bool legIntl = info.ContainsKey("LaTuyenQuocTe") && Convert.ToBoolean(info["LaTuyenQuocTe"]);
                decimal adultTax = FlightPricingHelper.AdultTax(legIntl);
                decimal childTax = FlightPricingHelper.ChildTax(legIntl);

                int seatCursor = 0;
                int infantCursor = 0;
                foreach (var pax in passengers)
                {
                    int? seatId = null;
                    string maHangGhe = "ECO";
                    decimal giaSauHang = giaCoBanChuyen;
                    decimal giaChonGhe = 0m;

                    if (pax.LoaiHanhKhach != "Em bé")
                    {
                        seatId = leg.SeatIds[seatCursor++];
                        const string qSeat = @"
                            SELECT g.MaHangGhe, dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, g.MaHangGhe) AS GiaSauHang, g.GiaChonGhe
                            FROM GheNgoi g CROSS JOIN ChuyenBay cb
                            WHERE g.MaGhe = @Ghe AND cb.MaChuyenBay = @CB;";
                        using var cmdSeat = new SqlCommand(qSeat, conn);
                        cmdSeat.Parameters.AddWithValue("@Ghe", seatId.Value);
                        cmdSeat.Parameters.AddWithValue("@CB", leg.MaCB);
                        using var rdSeat = await cmdSeat.ExecuteReaderAsync();
                        if (!await rdSeat.ReadAsync())
                        {
                            TempData["Error"] = "Ghế đã chọn không hợp lệ cho tàu bay này.";
                            return RedirectToAction(failureAction, failureRoute);
                        }
                        maHangGhe = rdSeat.GetString(0);
                        giaSauHang = rdSeat.GetDecimal(1);
                        giaChonGhe = rdSeat.GetDecimal(2);
                    }

                    decimal giaCoBanFinal = pax.LoaiHanhKhach switch
                    {
                        "Em bé" => FlightPricingHelper.InfantFare(giaCoBanChuyen),
                        "Trẻ em" => FlightPricingHelper.ChildFare(giaSauHang),
                        _ => giaSauHang
                    };
                    decimal thuePhiFinal = pax.LoaiHanhKhach switch
                    {
                        "Em bé" => 0m,
                        "Trẻ em" => childTax,
                        _ => adultTax
                    };
                    decimal tongVe = giaCoBanFinal + thuePhiFinal + (pax.LoaiHanhKhach == "Em bé" ? 0m : giaChonGhe);

                    string companion = pax.LoaiHanhKhach == "Em bé"
                        ? (!string.IsNullOrWhiteSpace(pax.NguoiLonDiCung) ? pax.NguoiLonDiCung.Trim() : adultNames[infantCursor++ % adultNames.Count])
                        : (pax.NguoiLonDiCung?.Trim() ?? string.Empty);

                    tvp.Rows.Add(
                        leg.HuongBay,
                        leg.ThuTu,
                        leg.MaCB,
                        seatId.HasValue ? seatId.Value : DBNull.Value,
                        maHangGhe,
                        pax.TenHanhKhach.Trim().ToUpperInvariant(),
                        pax.NgaySinh.Date,
                        pax.SoGiayTo.Trim().ToUpperInvariant(),
                        pax.LoaiGiayTo,
                        pax.SoGiayTo.Trim().ToUpperInvariant(),
                        pax.HanHoChieu.HasValue ? pax.HanHoChieu.Value.Date : DBNull.Value,
                        string.IsNullOrWhiteSpace(pax.QuocTich) ? "Việt Nam" : pax.QuocTich.Trim(),
                        pax.LoaiHanhKhach,
                        string.IsNullOrWhiteSpace(companion) ? DBNull.Value : companion,
                        giaCoBanFinal,
                        thuePhiFinal,
                        tongVe
                    );
                }
            }

            string maKH = CurrentCustomerId();
            string pnr = "PNR" + DateTime.Now.ToString("yyMMdd") + Random.Shared.Next(1000, 9999);
            if (pnr.Length > 15) pnr = pnr[..15];
            string loaiHanhTrinh = !string.IsNullOrWhiteSpace(maChuyenBayVe) ? "KHU_HOI" : "MOT_CHIEU";

            try
            {
                DateTime hanTT = DateTime.Now.AddMinutes(15);
                decimal tongThanhToanVe = 0m;

                using (var cmd = new SqlCommand("sp_DatVeMayBay_Batch", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@MaDatCho", pnr);
                    cmd.Parameters.AddWithValue("@MaKH", maKH);
                    cmd.Parameters.AddWithValue("@LoaiHanhTrinh", loaiHanhTrinh);
                    cmd.Parameters.AddWithValue("@MaKM", string.IsNullOrWhiteSpace(maKM) ? DBNull.Value : maKM.Trim().ToUpperInvariant());
                    cmd.Parameters.AddWithValue("@NguoiLienHe", string.IsNullOrWhiteSpace(nguoiLienHe) ? passengers[0].TenHanhKhach.Trim() : nguoiLienHe.Trim());
                    cmd.Parameters.AddWithValue("@SDTLienHe", string.IsNullOrWhiteSpace(sdtLienHe) ? "0900000000" : sdtLienHe.Trim());
                    cmd.Parameters.AddWithValue("@EmailLienHe", string.IsNullOrWhiteSpace(emailLienHe) ? "guest@skyvn.vn" : emailLienHe.Trim());
                    cmd.Parameters.AddWithValue("@GhiChu", $"Đặt vé {loaiHanhTrinh} ({legInputs.Count} chặng, {passengers.Count} khách)");
                    var pTvp = cmd.Parameters.AddWithValue("@DSVe", tvp);
                    pTvp.SqlDbType = SqlDbType.Structured;
                    pTvp.TypeName = "ChiTietDatVe_Type";

                    using var rd = await cmd.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        tongThanhToanVe = rd.GetDecimal(1);
                        hanTT = rd.GetDateTime(2);
                    }
                }

                decimal tienDichVu = 0m;
                if (selectedServiceIds != null && selectedServiceIds.Count > 0)
                    tienDichVu = await InsertAddonServicesAsync(conn, pnr, selectedServiceIds, "Chờ thanh toán", null);

                var txn = await _paymentGateway.CreateOrGetTransactionAsync(conn, pnr, "VE_MAY_BAY", "VietQR", tongThanhToanVe + tienDichVu, hanTT);

                if (tienDichVu > 0)
                {
                    using var cmdLink = new SqlCommand("UPDATE ChiTietDichVuDatVe SET MaGiaoDich = @GD WHERE MaDatCho = @Ma AND TrangThaiThanhToan = N'Chờ thanh toán';", conn);
                    cmdLink.Parameters.AddWithValue("@GD", txn.MaGiaoDich);
                    cmdLink.Parameters.AddWithValue("@Ma", pnr);
                    await cmdLink.ExecuteNonQueryAsync();
                }

                foreach (var leg in legInputs)
                {
                    await _hubContext.Clients.Group($"FLIGHT_{leg.MaCB}")
                        .SendAsync("SeatStatusChanged", new
                        {
                            maChuyenBay = leg.MaCB,
                            seatIds = leg.SeatIds,
                            isOccupied = true,
                            seatState = "held",
                            message = $"Ghế trên chuyến {leg.MaCB} vừa được giữ chỗ 15 phút bởi giao dịch {pnr}."
                        });
                }

                HttpContext.Session.Remove("BookingDraft");
                return RedirectToAction("Payment", new { maDatCho = pnr, maGD = txn.MaGiaoDich });
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Number switch
                {
                    1205 => "Hệ thống ghi nhận giao dịch đồng thời trên cùng ghế (Deadlock). Vui lòng thử lại ngay!",
                    2601 or 2627 => "Ghế hoặc số giấy tờ hành khách vừa bị người khác đặt trước trong tích tắc!",
                    _ => ex.Message
                };
                return RedirectToAction(failureAction, failureRoute);
            }
        }

        private async Task<(List<Dictionary<string, object>> Legs, bool IsIntl, decimal TaxPerSeatedPax, decimal TaxPerChildPax, DateTime MinFlightDate, DateTime MaxFlightDate)>
            LoadItineraryLegsAsync(SqlConnection conn, string maDi, string hangDi, string? maDi2, string? maVe, string hangVe, string? maVe2)
        {
            var defs = new List<(string Huong, int ThuTu, string MaCB, string Hang, string Label)>
            {
                ("DI", 1, maDi.Trim().ToUpperInvariant(), hangDi, "Chuyến đi • Chặng 1")
            };
            if (!string.IsNullOrWhiteSpace(maDi2))
                defs.Add(("DI", 2, maDi2.Trim().ToUpperInvariant(), hangDi, "Chuyến đi • Nối chuyến 2"));
            if (!string.IsNullOrWhiteSpace(maVe))
                defs.Add(("VE", 1, maVe.Trim().ToUpperInvariant(), hangVe, "Chuyến về • Chặng 1"));
            if (!string.IsNullOrWhiteSpace(maVe2))
                defs.Add(("VE", 2, maVe2.Trim().ToUpperInvariant(), hangVe, "Chuyến về • Nối chuyến 2"));

            var legs = new List<Dictionary<string, object>>();
            bool isIntl = false;
            decimal taxAdult = 0m, taxChild = 0m;
            DateTime minD = DateTime.MaxValue, maxD = DateTime.MinValue;

            foreach (var df in defs)
            {
                var detail = await GetFlightDetailAsync(conn, df.MaCB);
                if (detail.Count == 0) return (new List<Dictionary<string, object>>(), false, 0m, 0m, DateTime.MinValue, DateTime.MinValue);

                bool legIntl = detail.ContainsKey("LaTuyenQuocTe") && Convert.ToBoolean(detail["LaTuyenQuocTe"]);
                if (legIntl) isIntl = true;
                taxAdult += FlightPricingHelper.AdultTax(legIntl);
                taxChild += FlightPricingHelper.ChildTax(legIntl);

                DateTime dep = Convert.ToDateTime(detail["NgayGioBay"]);
                if (dep < minD) minD = dep;
                if (dep > maxD) maxD = dep;

                string hang = string.IsNullOrWhiteSpace(df.Hang) ? "ECO" : df.Hang.Trim().ToUpperInvariant();
                decimal giaHang = Convert.ToDecimal(detail["GiaVeCoBan"]) * (hang == "FIR" ? 3.5m : hang == "BUS" ? 2.0m : 1.0m);
                giaHang = Math.Round(giaHang / 1000m, 0) * 1000m;

                detail["HuongBay"] = df.Huong;
                detail["ThuTuChang"] = df.ThuTu;
                detail["SelectedHangGhe"] = hang;
                detail["GiaTheoHang"] = giaHang;
                detail["LegLabel"] = df.Label;
                legs.Add(detail);
            }
            return (legs, isIntl, taxAdult, taxChild, minD, maxD);
        }

        private static decimal CalculateEstimatedFareTotal(List<Dictionary<string, object>> legs, int nl, int te, int eb)
        {
            decimal sum = 0m;
            foreach (var l in legs)
            {
                decimal giaHang = Convert.ToDecimal(l["GiaTheoHang"]);
                decimal giaCoBan = Convert.ToDecimal(l["GiaVeCoBan"]);
                sum += giaHang * nl;
                sum += FlightPricingHelper.ChildFare(giaHang) * te;
                sum += FlightPricingHelper.InfantFare(giaCoBan) * eb;
            }
            return sum;
        }

        private static async Task<List<int>> ResolveSeatSelectionAsync(SqlConnection conn, string maCB, string preferredClass, List<int>? userSelected, int needed)
        {
            if (needed <= 0) return new List<int>();
            if (userSelected != null && userSelected.Count == needed) return userSelected;

            string cls = string.IsNullOrWhiteSpace(preferredClass) || preferredClass == "FIR" ? "BUS" : preferredClass;
            const string sql = @"
                SELECT TOP (@Take) g.MaGhe
                FROM GheNgoi g
                JOIN ChuyenBay cb ON g.MaMayBay = cb.MaMayBay
                WHERE cb.MaChuyenBay = @MaCB AND g.TrangThai = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM VeMayBay v
                      WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe
                        AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                  )
                ORDER BY CASE WHEN g.MaHangGhe = @Cls THEN 0 ELSE 1 END, g.GiaChonGhe ASC, g.SoGhe ASC;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Take", needed);
            cmd.Parameters.AddWithValue("@MaCB", maCB);
            cmd.Parameters.AddWithValue("@Cls", cls);
            var ids = new List<int>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) ids.Add(rd.GetInt32(0));
            return ids;
        }

        // =========================================================================
        // 3. MÀN HÌNH THANH TOÁN VIETQR SANDBOX (VÉ & DỊCH VỤ BỔ SUNG)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Payment(string maDatCho, string? maGD = null)
        {
            if (string.IsNullOrWhiteSpace(maDatCho))
                return RedirectToAction("Index", "Home");

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var don = await GetBookingHeaderDictAsync(conn, maDatCho.Trim().ToUpperInvariant());
            if (don == null) return NotFound();

            PaymentInitiationResult? txn = null;
            if (!string.IsNullOrWhiteSpace(maGD))
            {
                const string qTxn = @"
                    SELECT MaGiaoDich, MaDatCho, LoaiGiaoDich, SoTien, NoiDungCK, QrImageUrl, Signature, HanThanhToan, TrangThai,
                           AnhChuyenKhoanUrl, NgayGuiMinhChung, LyDoTuChoi
                    FROM GiaoDichThanhToan WHERE MaGiaoDich = @GD;";
                using var cmd = new SqlCommand(qTxn, conn);
                cmd.Parameters.AddWithValue("@GD", maGD);
                using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    ViewBag.TransactionStatus = rd.GetString(8);
                    ViewBag.ProofUrl = rd.IsDBNull(9) ? null : rd.GetString(9);
                    ViewBag.ProofTime = rd.IsDBNull(10) ? (DateTime?)null : rd.GetDateTime(10);
                    ViewBag.RejectReason = rd.IsDBNull(11) ? null : rd.GetString(11);
                    txn = new PaymentInitiationResult(
                        rd.GetString(0), rd.GetString(1), rd.GetString(2), rd.GetDecimal(3),
                        rd.IsDBNull(4) ? maDatCho : rd.GetString(4),
                        rd.IsDBNull(5) ? "" : rd.GetString(5),
                        rd.IsDBNull(6) ? "" : rd.GetString(6),
                        rd.GetDateTime(7));
                }
            }

            if (txn == null)
            {
                string trangThaiDon = don["TrangThaiDonHang"].ToString() ?? "";
                if (trangThaiDon != "Chờ thanh toán" && trangThaiDon != "Chờ duyệt thanh toán")
                {
                    TempData["Info"] = $"Đơn đặt chỗ {maDatCho} hiện ở trạng thái: {trangThaiDon}.";
                    return RedirectToAction("Lookup", new { maDatCho });
                }

                decimal tienVe = Convert.ToDecimal(don["TongTienSauGiam"]);
                decimal tienDvCho;
                using (var cmdDv = new SqlCommand("SELECT ISNULL(SUM(ThanhTien), 0) FROM ChiTietDichVuDatVe WHERE MaDatCho = @Ma AND TrangThaiThanhToan = N'Chờ thanh toán'", conn))
                {
                    cmdDv.Parameters.AddWithValue("@Ma", maDatCho);
                    tienDvCho = Convert.ToDecimal(await cmdDv.ExecuteScalarAsync());
                }

                txn = await _paymentGateway.CreateOrGetTransactionAsync(
                    conn,
                    maDatCho,
                    "VE_MAY_BAY",
                    "Chuyển khoản ngân hàng (VietQR)",
                    tienVe + tienDvCho,
                    Convert.ToDateTime(don["HanThanhToan"]));

                using var qMeta = new SqlCommand("SELECT TrangThai, AnhChuyenKhoanUrl, NgayGuiMinhChung, LyDoTuChoi FROM GiaoDichThanhToan WHERE MaGiaoDich = @GD;", conn);
                qMeta.Parameters.AddWithValue("@GD", txn.MaGiaoDich);
                using var rdMeta = await qMeta.ExecuteReaderAsync();
                if (await rdMeta.ReadAsync())
                {
                    ViewBag.TransactionStatus = rdMeta.GetString(0);
                    ViewBag.ProofUrl = rdMeta.IsDBNull(1) ? null : rdMeta.GetString(1);
                    ViewBag.ProofTime = rdMeta.IsDBNull(2) ? (DateTime?)null : rdMeta.GetDateTime(2);
                    ViewBag.RejectReason = rdMeta.IsDBNull(3) ? null : rdMeta.GetString(3);
                }
            }

            // Lấy chi tiết vé trong PNR
            var dtVe = new DataTable();
            using (var da = new SqlDataAdapter("SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @Ma ORDER BY HuongBay, ThuTuChang", conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                da.Fill(dtVe);
            }

            string curTxnStatus = ViewBag.TransactionStatus?.ToString() ?? "Chờ thanh toán";
            int remainingSec = curTxnStatus == "Chờ duyệt"
                ? Math.Max(600, (int)(txn.HanThanhToan - DateTime.Now).TotalSeconds)
                : Math.Max(0, (int)(txn.HanThanhToan - DateTime.Now).TotalSeconds);
            ViewBag.DonDi = don;
            ViewBag.Txn = txn;
            ViewBag.RemainingSeconds = remainingSec;
            ViewBag.TongThanhToan = txn.SoTien;
            ViewBag.DanhSachVe = dtVe;
            ViewBag.TransactionStatus = curTxnStatus;
            return View();
        }

        // =========================================================================
        // 4. VÉ CỦA TÔI & MUA DỊCH VỤ BỔ SUNG SAU KHI ĐẶT VÉ (BẮT BUỘC THANH TOÁN VIETQR)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> MyBookings()
        {
            string maKH = CurrentCustomerId();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            const string sql = @"
                SELECT v.*
                FROM vw_LichSuDatVeKhachHang v
                WHERE v.MaKH = @MaKH
                ORDER BY v.NgayDat DESC;";

            var list = new List<Dictionary<string, object>>();
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@MaKH", maKH);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var row = new Dictionary<string, object>();
                    for (int i = 0; i < rd.FieldCount; i++)
                        row[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
                    list.Add(row);
                }
            }

            ViewBag.Bookings = list;
            ViewBag.DichVuBoSung = await GetAddonServicesAsync(conn);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyAddonAfterBooking(string maDatCho, List<string> serviceIds, string? returnUrl)
        {
            if (serviceIds == null || serviceIds.Count == 0)
            {
                TempData["Error"] = "Vui lòng chọn ít nhất 1 dịch vụ bổ sung.";
                return returnUrl == "Lookup" ? RedirectToAction("Lookup", new { maDatCho }) : RedirectToAction("MyBookings");
            }

            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            // Kiểm tra trạng thái đơn và quy tắc >= 3 giờ trước chuyến bay gần nhất (với hành lý/suất ăn)
            DateTime? earliestFlight = null;
            string pnrStatus = "";
            using (var cmdCheck = new SqlCommand(@"
                SELECT p.TrangThai, MIN(cb.NgayGioBay) AS EarliestFlight
                FROM PhieuDatCho p
                JOIN VeMayBay v ON p.MaDatCho = v.MaDatCho
                JOIN ChuyenBay cb ON v.MaChuyenBay = cb.MaChuyenBay
                WHERE p.MaDatCho = @Ma AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                GROUP BY p.TrangThai;", conn))
            {
                cmdCheck.Parameters.AddWithValue("@Ma", maDatCho);
                using var rd = await cmdCheck.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    pnrStatus = rd.GetString(0);
                    earliestFlight = rd.IsDBNull(1) ? null : rd.GetDateTime(1);
                }
            }

            if (pnrStatus != "Đã thanh toán")
            {
                TempData["Error"] = "Chỉ được mua thêm dịch vụ cho đơn đặt chỗ đã thanh toán vé.";
                return returnUrl == "Lookup" ? RedirectToAction("Lookup", new { maDatCho }) : RedirectToAction("MyBookings");
            }

            if (earliestFlight.HasValue && (earliestFlight.Value - DateTime.Now).TotalHours < 3.0)
            {
                TempData["Error"] = "Đã dưới 3 giờ trước giờ khởi hành — không thể mua thêm hành lý / suất ăn trực tuyến.";
                return returnUrl == "Lookup" ? RedirectToAction("Lookup", new { maDatCho }) : RedirectToAction("MyBookings");
            }

            // Tạo chi tiết dịch vụ trạng thái Chờ thanh toán + Giao dịch DICH_VU và chuyển qua màn hình VietQR
            string maGD = $"ADDON_{maDatCho}_{DateTime.Now:yyyyMMddHHmmss}_{Random.Shared.Next(100, 999)}";
            decimal totalAddon = await InsertAddonServicesAsync(conn, maDatCho, serviceIds, "Chờ thanh toán", maGD);
            if (totalAddon <= 0)
            {
                TempData["Error"] = "Không tính được chi phí dịch vụ hợp lệ.";
                return RedirectToAction("MyBookings");
            }

            DateTime hanAddon = DateTime.Now.AddMinutes(15);
            if (earliestFlight.HasValue && hanAddon > earliestFlight.Value.AddHours(-1))
                hanAddon = earliestFlight.Value.AddHours(-1);

            await _paymentGateway.CreateOrGetTransactionAsync(conn, maDatCho, "DICH_VU", "VietQR", totalAddon, hanAddon, maGD);
            TempData["Info"] = $"Đã tạo giao dịch thanh toán dịch vụ bổ sung {totalAddon:N0} VNĐ. Vui lòng quét VietQR để hoàn tất!";
            return RedirectToAction("Payment", new { maDatCho, maGD });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeSeatAfterBooking(string maDatCho, string maVe, int maGheMoi)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                string maCB = "";
                int oldSeat = 0;
                using (var qOld = new SqlCommand("SELECT MaChuyenBay, ISNULL(MaGhe, 0) FROM VeMayBay WHERE MaVe = @Ve", conn))
                {
                    qOld.Parameters.AddWithValue("@Ve", maVe);
                    using var rd = await qOld.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        maCB = rd.GetString(0);
                        oldSeat = rd.GetInt32(1);
                    }
                }

                using var cmd = new SqlCommand("sp_DoiGheSauDatVe", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaVe", maVe);
                cmd.Parameters.AddWithValue("@MaGheMoi", maGheMoi);
                await cmd.ExecuteNonQueryAsync();

                if (!string.IsNullOrEmpty(maCB))
                {
                    if (oldSeat > 0)
                        await _hubContext.Clients.Group($"FLIGHT_{maCB}").SendAsync("SeatStatusChanged", new { maChuyenBay = maCB, seatIds = new[] { oldSeat }, isOccupied = false, seatState = "available" });
                    await _hubContext.Clients.Group($"FLIGHT_{maCB}").SendAsync("SeatStatusChanged", new { maChuyenBay = maCB, seatIds = new[] { maGheMoi }, isOccupied = true, seatState = "sold" });
                }

                TempData["Success"] = $"Đã đổi sang ghế mới cho vé {maVe}!";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Lookup", new { maDatCho });
        }

        // =========================================================================
        // 5. HÓA ĐƠN ĐIỆN TỬ VAT & BỘ BIỂU MẪU IN PDF (BM1 - BM5)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Invoice(string maDatCho)
        {
            if (string.IsNullOrWhiteSpace(maDatCho)) return NotFound();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            bool alreadyIssued = false;
            string savedLoai = "Cá nhân";
            string? savedTen = null, savedMst = null, savedDiaChi = null, savedEmail = null;
            using (var q = new SqlCommand("SELECT DaXuatHoaDon, LoaiHoaDonVAT, TenCongTyVAT, MaSoThueVAT, DiaChiCongTyVAT, EmailNhanHoaDon FROM HoaDon WHERE MaDatCho = @Ma", conn))
            {
                q.Parameters.AddWithValue("@Ma", maDatCho);
                using var rd = await q.ExecuteReaderAsync();
                if (!await rd.ReadAsync()) return NotFound();
                alreadyIssued = !rd.IsDBNull(0) && Convert.ToBoolean(rd.GetValue(0));
                if (!rd.IsDBNull(1)) savedLoai = rd.GetString(1);
                if (!rd.IsDBNull(2)) savedTen = rd.GetString(2);
                if (!rd.IsDBNull(3)) savedMst = rd.GetString(3);
                if (!rd.IsDBNull(4)) savedDiaChi = rd.GetString(4);
                if (!rd.IsDBNull(5)) savedEmail = rd.GetString(5);
            }

            if (alreadyIssued)
                return LoadInvoiceViewData(conn, maDatCho);

            return await BuildInvoiceViewAsync(maDatCho, savedLoai, savedTen, savedMst, savedDiaChi, savedEmail);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestVatInvoice(
            string maDatCho,
            string loaiHoaDonVAT,
            string? tenCongTyVAT,
            string? maSoThueVAT,
            string? diaChiCongTyVAT,
            string? emailNhanHoaDon)
        {
            if (loaiHoaDonVAT == "Doanh nghiệp")
            {
                if (string.IsNullOrWhiteSpace(tenCongTyVAT) || !VietnameseCurrencyHelper.IsValidTaxCode(maSoThueVAT))
                {
                    TempData["Error"] = "Xuất hóa đơn doanh nghiệp bắt buộc nhập Tên công ty và Mã số thuế hợp lệ (10 hoặc 13 chữ số).";
                    return RedirectToAction("Lookup", new { maDatCho });
                }
            }
            return await BuildInvoiceViewAsync(maDatCho, loaiHoaDonVAT, tenCongTyVAT, maSoThueVAT, diaChiCongTyVAT, emailNhanHoaDon);
        }

        private async Task<IActionResult> BuildInvoiceViewAsync(
            string maDatCho, string loaiHoaDonVAT, string? tenCongTyVAT, string? maSoThueVAT, string? diaChiCongTyVAT, string? emailNhanHoaDon)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                using (var cmdCheck = new SqlCommand("sp_XuatHoaDonTrong72Gio", conn))
                {
                    cmdCheck.CommandType = CommandType.StoredProcedure;
                    cmdCheck.Parameters.AddWithValue("@MaDatCho", maDatCho);
                    cmdCheck.Parameters.AddWithValue("@LoaiHoaDonVAT", loaiHoaDonVAT);
                    cmdCheck.Parameters.AddWithValue("@TenCongTyVAT", string.IsNullOrWhiteSpace(tenCongTyVAT) ? DBNull.Value : tenCongTyVAT.Trim());
                    cmdCheck.Parameters.AddWithValue("@MaSoThueVAT", string.IsNullOrWhiteSpace(maSoThueVAT) ? DBNull.Value : maSoThueVAT.Trim());
                    cmdCheck.Parameters.AddWithValue("@DiaChiCongTyVAT", string.IsNullOrWhiteSpace(diaChiCongTyVAT) ? DBNull.Value : diaChiCongTyVAT.Trim());
                    cmdCheck.Parameters.AddWithValue("@EmailNhanHoaDon", string.IsNullOrWhiteSpace(emailNhanHoaDon) ? DBNull.Value : emailNhanHoaDon.Trim());
                    await cmdCheck.ExecuteNonQueryAsync();
                }

                return LoadInvoiceViewData(conn, maDatCho);
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Lookup", new { maDatCho });
            }
        }

        private IActionResult LoadInvoiceViewData(SqlConnection conn, string maDatCho)
        {
            const string sqlHD = @"
                SELECT hd.*, ISNULL(hd.MaGiaoDichNganHang, 'TXN_ONLINE') AS MaGiaoDich,
                       pdc.TongTienGoc, pdc.MaKM, pdc.TienGiam, pdc.TongTienSauGiam,
                       km.PhanTramGiam,
                       CASE WHEN DATEDIFF(HOUR, hd.NgayThanhToan, GETDATE()) <= 72 THEN 1 ELSE 0 END AS Within72h,
                       kh.TenKH, kh.CCCD, kh.SDT, kh.Email, kh.DiaChi
                FROM HoaDon hd
                JOIN PhieuDatCho pdc ON hd.MaDatCho = pdc.MaDatCho
                LEFT JOIN KhuyenMai km ON pdc.MaKM = km.MaKM
                JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
                WHERE hd.MaDatCho = @Ma;";
            var dtHD = new DataTable();
            using (var da = new SqlDataAdapter(sqlHD, conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                da.Fill(dtHD);
            }
            if (dtHD.Rows.Count == 0) return NotFound();
            ViewBag.HoaDon = dtHD.Rows[0];

            var dtVe = new DataTable();
            using (var daVe = new SqlDataAdapter("SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @Ma ORDER BY HuongBay, ThuTuChang", conn))
            {
                daVe.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                daVe.Fill(dtVe);
            }
            ViewBag.DanhSachVe = dtVe;

            var dtDV = new DataTable();
            using (var daDV = new SqlDataAdapter(@"
                SELECT dv.TenDV, dv.LoaiDV, ct.SoLuong, ct.ThanhTien, ct.MaGiaoDich
                FROM ChiTietDichVuDatVe ct
                JOIN DichVuBoSung dv ON ct.MaDV = dv.MaDV
                WHERE ct.MaDatCho = @Ma AND ct.TrangThaiThanhToan = N'Đã thanh toán';", conn))
            {
                daDV.SelectCommand.Parameters.AddWithValue("@Ma", maDatCho);
                daDV.Fill(dtDV);
            }
            ViewBag.DanhSachDichVu = dtDV;

            return View("Invoice");
        }

        // BIỂU MẪU 1 (HÌNH 1.2): PHIẾU XÁC NHẬN ĐẶT CHỖ & LỊCH TRÌNH BAY
        [HttpGet]
        public async Task<IActionResult> ItineraryForm(string maDatCho)
        {
            if (string.IsNullOrWhiteSpace(maDatCho)) return NotFound();
            string pnr = maDatCho.Trim().ToUpperInvariant();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var dtDon = new DataTable();
            const string sqlDon = @"
                SELECT v.*, pdc.MaKM, pdc.GhiChu,
                       kh.CCCD, kh.LoaiKhachHang, kh.DiaChi
                FROM vw_LichSuDatVeKhachHang v
                JOIN PhieuDatCho pdc ON v.MaDatCho = pdc.MaDatCho
                JOIN KhachHang kh ON v.MaKH = kh.MaKH
                WHERE v.MaDatCho = @Ma;";
            using (var da = new SqlDataAdapter(sqlDon, conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@Ma", pnr);
                da.Fill(dtDon);
            }
            if (dtDon.Rows.Count == 0) return NotFound();
            ViewBag.DonDatCho = dtDon.Rows[0];

            var dtVe = new DataTable();
            const string sqlVe = @"
                SELECT v.*, mb.MaMayBay, mb.TenMayBay
                FROM vw_ChiTietVeVaHanhKhach v
                JOIN ChuyenBay cb ON v.MaChuyenBay = cb.MaChuyenBay
                JOIN MayBay mb ON cb.MaMayBay = mb.MaMayBay
                WHERE v.MaDatCho = @Ma
                ORDER BY v.HuongBay, v.ThuTuChang, v.MaVe;";
            using (var daVe = new SqlDataAdapter(sqlVe, conn))
            {
                daVe.SelectCommand.Parameters.AddWithValue("@Ma", pnr);
                daVe.Fill(dtVe);
            }
            ViewBag.DanhSachVe = dtVe;

            var dtDV = new DataTable();
            using (var daDV = new SqlDataAdapter(@"
                SELECT dv.TenDV, dv.LoaiDV, ct.SoLuong, ct.ThanhTien, ct.TrangThaiThanhToan
                FROM ChiTietDichVuDatVe ct
                JOIN DichVuBoSung dv ON ct.MaDV = dv.MaDV
                WHERE ct.MaDatCho = @Ma;", conn))
            {
                daDV.SelectCommand.Parameters.AddWithValue("@Ma", pnr);
                daDV.Fill(dtDV);
            }
            ViewBag.DanhSachDichVu = dtDV;

            return View("ItineraryForm");
        }

        // BIỂU MẪU 4 (HÌNH 1.5): PHIẾU YÊU CẦU HỦY VÉ & HOÀN TIỀN (QUY ĐỊNH 72H)
        [HttpGet]
        public async Task<IActionResult> CancellationForm(string? maVe, int? maYeuCau)
        {
            if (string.IsNullOrWhiteSpace(maVe) && !maYeuCau.HasValue) return NotFound();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            const string sql = @"
                SELECT TOP 1
                       yc.MaYeuCau, yc.MaVe, yc.LoaiYeuCau, yc.NgayYeuCau, yc.LyDo,
                       yc.PhanTramHoan, yc.SoTienGoc, yc.PhiPhat, yc.SoTienHoan,
                       yc.TrangThai AS TrangThaiYeuCau, yc.NgayXuLy, yc.GhiChu,
                       yc.MaNVXuLy, ISNULL(nv.TenNV, N'Hệ thống Tự động (Web Online)') AS TenNVXuLy,
                       v.MaDatCho, v.TenHanhKhach, v.SoGiayTo, v.LoaiGiayTo,
                       v.MaChuyenBay, v.NgayGioBay, v.MaSanBayDi, v.MaSanBayDen, v.ThanhPhoDi, v.ThanhPhoDen,
                       ISNULL(v.SoGhe, '---') AS SoGhe, v.TenHangGhe,
                       DATEDIFF(HOUR, yc.NgayYeuCau, v.NgayGioBay) AS GioTruocBay,
                       ISNULL(hd.PhuongThuc, N'VietQR / Thanh toán trực tuyến') AS PhuongThucHoan
                FROM YeuCauHuyDoiVe yc
                JOIN vw_ChiTietVeVaHanhKhach v ON yc.MaVe = v.MaVe
                LEFT JOIN NhanVien nv ON yc.MaNVXuLy = nv.MaNV
                LEFT JOIN HoaDon hd ON v.MaDatCho = hd.MaDatCho
                WHERE yc.LoaiYeuCau = N'Hủy vé'
                  AND ((@MaYC IS NOT NULL AND yc.MaYeuCau = @MaYC)
                       OR (@MaVe IS NOT NULL AND yc.MaVe = @MaVe))
                ORDER BY yc.MaYeuCau DESC;";

            var dt = new DataTable();
            using (var da = new SqlDataAdapter(sql, conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@MaYC", maYeuCau.HasValue ? maYeuCau.Value : DBNull.Value);
                da.SelectCommand.Parameters.AddWithValue("@MaVe", string.IsNullOrWhiteSpace(maVe) ? DBNull.Value : maVe.Trim());
                da.Fill(dt);
            }

            if (dt.Rows.Count == 0)
            {
                TempData["Error"] = "Không tìm thấy phiếu yêu cầu hủy vé cho vé này.";
                return RedirectToAction("Lookup");
            }

            ViewBag.PhieuHuy = dt.Rows[0];
            return View("CancellationForm");
        }

        // BIỂU MẪU 5 (HÌNH 1.6): PHIẾU YÊU CẦU ĐỔI VÉ / DỜI LỊCH BAY
        [HttpGet]
        public async Task<IActionResult> ExchangeForm(string? maVeCu, int? maYeuCau)
        {
            if (string.IsNullOrWhiteSpace(maVeCu) && !maYeuCau.HasValue) return NotFound();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            const string sql = @"
                SELECT TOP 1
                       yc.MaYeuCau, yc.MaVe AS MaVeCu, yc.LoaiYeuCau, yc.NgayYeuCau, yc.NgayXuLy, yc.LyDo,
                       yc.SoTienGoc AS GiaVeCu, yc.PhiPhat AS TongThuThem, yc.PhiChenhLech, yc.TrangThai AS TrangThaiYeuCau,
                       yc.MaNVXuLy, ISNULL(nv.TenNV, N'Hệ thống Tự động / CSKH') AS TenNVXuLy,
                       vOld.MaDatCho, vOld.TenHanhKhach, vOld.MaChuyenBay AS MaCBCu, vOld.NgayGioBay AS GioBayCu,
                       vOld.MaSanBayDi AS SanBayDiCu, vOld.MaSanBayDen AS SanBayDenCu,
                       ISNULL(vOld.SoGhe, '---') AS SoGheCu,
                       ISNULL(yc.MaChuyenBayMoi, '') AS MaCBMoi,
                       cbNew.NgayGioBay AS GioBayMoi,
                       tbNew.MaSanBayDi AS SanBayDiMoi, tbNew.MaSanBayDen AS SanBayDenMoi,
                       ISNULL(gNew.SoGhe, '---') AS SoGheMoi,
                       ISNULL(vNew.MaVe, yc.MaVe + '-RE') AS MaVeMoi,
                       ISNULL(vNew.GiaVe, yc.SoTienGoc + yc.PhiChenhLech) AS GiaVeMoi,
                       kh.Email AS EmailKhach
                FROM YeuCauHuyDoiVe yc
                JOIN vw_ChiTietVeVaHanhKhach vOld ON yc.MaVe = vOld.MaVe
                JOIN PhieuDatCho pdc ON vOld.MaDatCho = pdc.MaDatCho
                JOIN KhachHang kh ON pdc.MaKH = kh.MaKH
                LEFT JOIN NhanVien nv ON yc.MaNVXuLy = nv.MaNV
                LEFT JOIN ChuyenBay cbNew ON yc.MaChuyenBayMoi = cbNew.MaChuyenBay
                LEFT JOIN TuyenBay tbNew ON cbNew.MaTuyenBay = tbNew.MaTuyenBay
                LEFT JOIN GheNgoi gNew ON yc.MaGheMoi = gNew.MaGhe
                OUTER APPLY (
                    SELECT TOP 1 vn.MaVe, vn.GiaVe
                    FROM VeMayBay vn
                    WHERE vn.MaDatCho = vOld.MaDatCho
                      AND vn.MaChuyenBay = yc.MaChuyenBayMoi
                      AND vn.MaGhe = yc.MaGheMoi
                      AND vn.MaVe <> yc.MaVe
                    ORDER BY vn.MaVe DESC
                ) vNew
                WHERE yc.LoaiYeuCau = N'Đổi vé'
                  AND ((@MaYC IS NOT NULL AND yc.MaYeuCau = @MaYC)
                       OR (@MaVe IS NOT NULL AND yc.MaVe = @MaVe))
                ORDER BY yc.MaYeuCau DESC;";

            var dt = new DataTable();
            using (var da = new SqlDataAdapter(sql, conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@MaYC", maYeuCau.HasValue ? maYeuCau.Value : DBNull.Value);
                da.SelectCommand.Parameters.AddWithValue("@MaVe", string.IsNullOrWhiteSpace(maVeCu) ? DBNull.Value : maVeCu.Trim());
                da.Fill(dt);
            }

            if (dt.Rows.Count == 0)
            {
                TempData["Error"] = "Không tìm thấy phiếu xác nhận đổi vé cho vé này.";
                return RedirectToAction("Lookup");
            }

            ViewBag.PhieuDoi = dt.Rows[0];
            return View("ExchangeForm");
        }

        // =========================================================================
        // 6. TRA CỨU PNR / ĐỔI / HỦY / ĐÁNH GIÁ
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Lookup(string? maDatCho, string? hoTenHoacSdt = null)
        {
            if (string.IsNullOrWhiteSpace(maDatCho)) return View();

            string pnr = maDatCho.Trim().ToUpperInvariant();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            var dtDon = new DataTable();
            const string sqlDon = @"
                SELECT v.*
                FROM vw_LichSuDatVeKhachHang v
                WHERE v.MaDatCho = @Ma
                  AND (@Verify IS NULL OR @Verify = '' OR v.SDT = @Verify OR v.SDTLienHe = @Verify OR UPPER(v.TenKH) LIKE N'%' + UPPER(@Verify) + N'%');";
            using (var da = new SqlDataAdapter(sqlDon, conn))
            {
                da.SelectCommand.Parameters.AddWithValue("@Ma", pnr);
                da.SelectCommand.Parameters.AddWithValue("@Verify", string.IsNullOrWhiteSpace(hoTenHoacSdt) ? DBNull.Value : hoTenHoacSdt.Trim());
                da.Fill(dtDon);
            }

            if (dtDon.Rows.Count == 0)
            {
                ViewBag.NotFound = $"Không tìm thấy hồ sơ đặt chỗ khớp với mã PNR: {pnr}";
                return View();
            }

            ViewBag.DonDatCho = dtDon.Rows[0];

            var dtVe = new DataTable();
            using (var daVe = new SqlDataAdapter("SELECT * FROM vw_ChiTietVeVaHanhKhach WHERE MaDatCho = @Ma ORDER BY HuongBay, ThuTuChang", conn))
            {
                daVe.SelectCommand.Parameters.AddWithValue("@Ma", pnr);
                daVe.Fill(dtVe);
            }
            ViewBag.DanhSachVe = dtVe;

            var refundMap = new Dictionary<string, DataRow>();
            foreach (DataRow v in dtVe.Rows)
            {
                string maVe = v["MaVe"].ToString()!;
                var dtRef = new DataTable();
                using var daRef = new SqlDataAdapter("SELECT * FROM dbo.fn_TinhTienHoanTraHuyVe(@MaVe)", conn);
                daRef.SelectCommand.Parameters.AddWithValue("@MaVe", maVe);
                daRef.Fill(dtRef);
                if (dtRef.Rows.Count > 0) refundMap[maVe] = dtRef.Rows[0];
            }
            ViewBag.RefundPreview = refundMap;

            var dtCB = new DataTable();
            using (var daCB = new SqlDataAdapter("SELECT TOP 80 * FROM vw_DanhSachChuyenBayChiTiet WHERE TrangThaiChuyenBay = N'Đang mở bán' AND SoGheTrong > 0 AND NgayGioBay > DATEADD(MINUTE, 60, GETDATE()) ORDER BY NgayGioBay ASC", conn))
            {
                daCB.Fill(dtCB);
            }
            ViewBag.ChuyenBayKhac = dtCB;
            ViewBag.DichVuBoSung = await GetAddonServicesAsync(conn);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAvailableSeats(string maChuyenBay)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            const string sql = @"
                SELECT g.MaGhe, g.SoGhe, g.MaHangGhe, hg.TenHangGhe,
                       dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, g.MaHangGhe) AS GiaThucTe
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                JOIN ChuyenBay cb ON g.MaMayBay = cb.MaMayBay
                WHERE cb.MaChuyenBay = @MaCB AND g.TrangThai = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM VeMayBay v
                      WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe
                        AND v.TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')
                  )
                ORDER BY g.SoGhe;";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MaCB", maChuyenBay);
            var list = new List<object>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                list.Add(new
                {
                    maGhe = rd.GetInt32(0),
                    soGhe = rd.GetString(1),
                    maHangGhe = rd.GetString(2),
                    tenHangGhe = rd.GetString(3),
                    giaThucTe = rd.GetDecimal(4)
                });
            }
            return Json(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetSeatMapStatus(string maChuyenBay)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            var seats = await GetSeatsForFlightAsync(conn, maChuyenBay);
            return Json(seats);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTicket(string maDatCho, string maVe, string lyDo)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                string maCB = "";
                int oldSeat = 0;
                using (var q = new SqlCommand("SELECT MaChuyenBay, ISNULL(MaGhe, 0) FROM VeMayBay WHERE MaVe = @V", conn))
                {
                    q.Parameters.AddWithValue("@V", maVe);
                    using var rd = await q.ExecuteReaderAsync();
                    if (await rd.ReadAsync())
                    {
                        maCB = rd.GetString(0);
                        oldSeat = rd.GetInt32(1);
                    }
                }

                using var cmd = new SqlCommand("sp_XuLyHuyVeVaHoanTien", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaVe", maVe);
                cmd.Parameters.AddWithValue("@LyDo", lyDo);
                cmd.Parameters.AddWithValue("@MaNVXuLy", DBNull.Value);
                await cmd.ExecuteNonQueryAsync();

                if (!string.IsNullOrEmpty(maCB) && oldSeat > 0)
                {
                    await _hubContext.Clients.Group($"FLIGHT_{maCB}")
                        .SendAsync("SeatStatusChanged", new { maChuyenBay = maCB, seatIds = new[] { oldSeat }, isOccupied = false, seatState = "available", message = "Ghế vừa được mở bán lại sau khi có vé hủy." });
                }

                TempData["Success"] = $"Đã hủy vé {maVe} và tự động hoàn tiền theo Quy định 72h & nhả ghế!";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Lookup", new { maDatCho });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RescheduleTicket(string maDatCho, string maVeCu, string maChuyenBayMoi, int maGheMoi, string maHangGheMoi)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            try
            {
                using var cmd = new SqlCommand("sp_DoiVeMayBay", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@MaVeCu", maVeCu);
                cmd.Parameters.AddWithValue("@MaChuyenBayMoi", maChuyenBayMoi);
                cmd.Parameters.AddWithValue("@MaGheMoi", maGheMoi);
                cmd.Parameters.AddWithValue("@MaHangGheMoi", maHangGheMoi);
                cmd.Parameters.AddWithValue("@MaNVXuLy", DBNull.Value);
                await cmd.ExecuteNonQueryAsync();
                TempData["Success"] = $"Đổi vé {maVeCu} sang chuyến {maChuyenBayMoi} thành công!";
            }
            catch (SqlException ex)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Lookup", new { maDatCho });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(string maDatCho, string maChuyenBay, int soSao, string binhLuan)
        {
            string maKH = CurrentCustomerId();
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();
            using var cmd = new SqlCommand("INSERT INTO DanhGiaChuyenBay (MaKH, MaChuyenBay, SoSao, NoiDung) VALUES (@MaKH, @MaCB, @Sao, @ND)", conn);
            cmd.Parameters.AddWithValue("@MaKH", maKH);
            cmd.Parameters.AddWithValue("@MaCB", maChuyenBay);
            cmd.Parameters.AddWithValue("@Sao", Math.Clamp(soSao, 1, 5));
            cmd.Parameters.AddWithValue("@ND", binhLuan);
            await cmd.ExecuteNonQueryAsync();
            TempData["Success"] = "Cảm ơn bạn đã gửi đánh giá chuyến bay!";
            return RedirectToAction("Lookup", new { maDatCho });
        }

        // ================= HELPER METHODS =================
        private static DataTable BuildUnifiedTicketTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("HuongBay", typeof(string));
            dt.Columns.Add("ThuTuChang", typeof(int));
            dt.Columns.Add("MaChuyenBay", typeof(string));
            dt.Columns.Add("MaGhe", typeof(int));
            dt.Columns.Add("MaHangGhe", typeof(string));
            dt.Columns.Add("TenHanhKhach", typeof(string));
            dt.Columns.Add("NgaySinh", typeof(DateTime));
            dt.Columns.Add("CCCD_HanhKhach", typeof(string));
            dt.Columns.Add("LoaiGiayTo", typeof(string));
            dt.Columns.Add("SoGiayTo", typeof(string));
            dt.Columns.Add("HanHoChieu", typeof(DateTime));
            dt.Columns.Add("QuocTich", typeof(string));
            dt.Columns.Add("LoaiHanhKhach", typeof(string));
            dt.Columns.Add("NguoiLonDiCung", typeof(string));
            dt.Columns.Add("GiaCoBan", typeof(decimal));
            dt.Columns.Add("ThuePhi", typeof(decimal));
            dt.Columns.Add("GiaVe", typeof(decimal));
            return dt;
        }

        private async Task<Dictionary<string, object>> GetFlightDetailAsync(SqlConnection conn, string maCB)
        {
            using var cmd = new SqlCommand("SELECT * FROM vw_DanhSachChuyenBayChiTiet WHERE MaChuyenBay = @Ma", conn);
            cmd.Parameters.AddWithValue("@Ma", maCB);
            var row = new Dictionary<string, object>();
            using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
                for (int i = 0; i < rd.FieldCount; i++) row[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
            return row;
        }

        private async Task<List<Dictionary<string, object>>> GetSeatsForFlightAsync(SqlConnection conn, string maCB)
        {
            const string sql = @"
                SELECT g.MaGhe, g.SoGhe, g.MaHangGhe, hg.TenHangGhe, hg.HeSoGia, g.ViTri, g.GiaChonGhe,
                       dbo.fn_TinhGiaVeThucTe(cb.GiaVeCoBan, g.MaHangGhe) AS GiaVeSauHang,
                       CASE
                           WHEN g.TrangThai = 0 THEN 'broken'
                           WHEN EXISTS (SELECT 1 FROM VeMayBay v WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe AND v.TrangThai IN (N'Đã xuất vé', N'Đã Check-in')) THEN 'sold'
                           WHEN EXISTS (SELECT 1 FROM VeMayBay v WHERE v.MaChuyenBay = @MaCB AND v.MaGhe = g.MaGhe AND v.TrangThai = N'Đã đặt chỗ') THEN 'held'
                           ELSE 'available'
                       END AS SeatState
                FROM GheNgoi g
                JOIN HangGhe hg ON g.MaHangGhe = hg.MaHangGhe
                JOIN ChuyenBay cb ON g.MaMayBay = cb.MaMayBay
                WHERE cb.MaChuyenBay = @MaCB
                ORDER BY g.SoGhe ASC;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@MaCB", maCB);
            var list = new List<Dictionary<string, object>>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var r = new Dictionary<string, object>();
                for (int i = 0; i < rd.FieldCount; i++) r[rd.GetName(i)] = rd.GetValue(i);
                r["DaBiDat"] = r["SeatState"].ToString() != "available" ? 1 : 0;
                list.Add(r);
            }
            return list;
        }

        private async Task<List<Dictionary<string, object>>> GetAddonServicesAsync(SqlConnection conn)
        {
            using var cmd = new SqlCommand("SELECT * FROM DichVuBoSung WHERE TrangThai = 1 ORDER BY LoaiDV, DonGia", conn);
            var list = new List<Dictionary<string, object>>();
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                var r = new Dictionary<string, object>();
                for (int i = 0; i < rd.FieldCount; i++) r[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
                list.Add(r);
            }
            return list;
        }

        private async Task<decimal> InsertAddonServicesAsync(SqlConnection conn, string maDatCho, List<string> serviceIds, string trangThai, string? maGD)
        {
            decimal total = 0m;
            foreach (var maDV in serviceIds.Distinct())
            {
                const string sql = @"
                    INSERT INTO ChiTietDichVuDatVe (MaDatCho, MaDV, SoLuong, DonGia, ThanhTien, TrangThaiThanhToan, MaGiaoDich, NgayThanhToan)
                    OUTPUT INSERTED.ThanhTien
                    SELECT @MaDatCho, MaDV, 1, DonGia, DonGia, @TT, @GD, CASE WHEN @TT = N'Đã thanh toán' THEN GETDATE() ELSE NULL END
                    FROM DichVuBoSung WHERE MaDV = @MaDV AND TrangThai = 1;";
                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@MaDatCho", maDatCho);
                cmd.Parameters.AddWithValue("@MaDV", maDV);
                cmd.Parameters.AddWithValue("@TT", trangThai);
                cmd.Parameters.AddWithValue("@GD", (object?)maGD ?? DBNull.Value);
                object? res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value)
                    total += Convert.ToDecimal(res);
            }
            return total;
        }

        private async Task<Dictionary<string, object>?> GetBookingHeaderDictAsync(SqlConnection conn, string maDatCho)
        {
            using var cmd = new SqlCommand("SELECT * FROM vw_LichSuDatVeKhachHang WHERE MaDatCho = @Ma", conn);
            cmd.Parameters.AddWithValue("@Ma", maDatCho);
            using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;
            var r = new Dictionary<string, object>();
            for (int i = 0; i < rd.FieldCount; i++) r[rd.GetName(i)] = rd.IsDBNull(i) ? "" : rd.GetValue(i);
            return r;
        }
    }
}
