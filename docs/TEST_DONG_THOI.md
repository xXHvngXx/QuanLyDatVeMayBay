# KIỂM THỬ ĐỒNG THỜI & CHỐNG TRTRANH CHẤP GHẾ (CONCURRENCY TESTING)

## 1. Mục tiêu kiểm thử
Chứng minh rằng khi **nhiều người dùng / luồng HTTP cùng đặt chung 1 ghế trên 1 chuyến bay tại cùng một thời điểm**, hệ thống:
1. Chỉ cho phép **đúng 1 giao dịch thành công** tạo PNR giữ chỗ 15 phút.
2. Tất cả các giao dịch cạnh tranh còn lại **bị từ chối an toàn** ở mức `SERIALIZABLE` + `WITH (UPDLOCK, HOLDLOCK)` + Filtered Unique Index `UX_VeMayBay_ActiveSeat` (mã lỗi SQL 2601/2627 hoặc deadlock 1205/RAISERROR 50000), **không sinh 2 vé cho 1 ghế** và **không làm âm hoặc sai lệch `ChuyenBay.SoGheTrong`**.
3. Ghế sau khi hủy hoặc hết hạn 15 phút sẽ được **mở lại cho người khác đặt**.

---

## 2. Cơ chế chống tranh chấp nhiều lớp trong SkyVN Air
1. **Lớp 1 — Khóa giao dịch CSDL (`sp_DatVeMayBay_Batch`)**:
   - `SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;`
   - Sắp xếp và khóa bản ghi `ChuyenBay WITH (UPDLOCK, HOLDLOCK)` theo thứ tự khóa tránh deadlock khi đặt khứ hồi / nối chuyến nhiều chặng.
   - Kiểm tra ghế đang hoạt động bằng `VeMayBay WITH (UPDLOCK, HOLDLOCK)`.
2. **Lớp 2 — Filtered Unique Index vật lý**:
   - `UX_VeMayBay_ActiveSeat` trên `(MaChuyenBay, MaGhe) WHERE MaGhe IS NOT NULL AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')`.
   - Ngăn tuyệt đối việc ghi trùng dù xảy ra điều kiện đua mức thấp, đồng thời vẫn cho phép đặt lại ghế từng bị hủy hoặc đổi.
3. **Lớp 3 — Realtime UI & Tự phục hồi (`SeatHub` + `SeatHoldCleanupService`)**:
   - Ngay khi transaction giữ ghế thành công, server bắn SignalR `SeatStatusChanged` tới room `FLIGHT_{MaChuyenBay}` khóa ghế tức thì trên các trình duyệt khác.
   - `SeatHoldCleanupService` chạy định kỳ 10 giây gọi `sp_TuDongHuyDonGiuChoHetHan_Cursor` nhả ghế quá 15 phút và bắn SignalR mở lại màu trắng.

---

## 3. Script PowerShell Kiểm Thử Song Song 5 Luồng Cùng Lúc

Chạy khi ứng dụng web đang chạy tại `http://localhost:5099`:

```powershell
# Kịch bản: 5 request POST đồng thời chọn cùng 1 ghế trên cùng chuyến bay (VD: VN120)
$baseUrl = "http://localhost:5099"
$flight = "VN120"

# 1. Lấy 1 ghế trống đầu tiên trên VN120
$seats = Invoke-RestMethod "$baseUrl/Booking/GetAvailableSeats?maChuyenBay=$flight"
$targetSeat = $seats[0].maGhe
Write-Host "Target SeatId = $targetSeat ($($seats[0].soGhe))"

# 2. Chạy song song 5 Jobs cùng gửi HoldSeatsAndProceed vào đúng 1 ghế $targetSeat
$jobs = 1..5 | ForEach-Object {
    $idx = $_
    Start-Job -ScriptBlock {
        param($url, $cb, $seat, $i)
        Add-Type -AssemblyName System.Net.Http
        $handler = New-Object System.Net.Http.HttpClientHandler
        $handler.UseCookies = $true
        $handler.AllowAutoRedirect = $false
        $client = New-Object System.Net.Http.HttpClient($handler)

        # Lấy AntiForgeryToken từ trang SelectSeats
        $html = $client.GetStringAsync("$url/Booking/SelectSeats?maChuyenBayDi=$cb&nguoiLon=1").Result
        $m = [regex]::Match($html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
        $token = $m.Groups[1].Value

        $form = New-Object System.Net.Http.FormUrlEncodedContent(@(
            [KeyValuePair[string,string]]::new("__RequestVerificationToken", $token),
            [KeyValuePair[string,string]]::new("maChuyenBayDi", $cb),
            [KeyValuePair[string,string]]::new("gheDiIds", "$seat"),
            [KeyValuePair[string,string]]::new("passengers[0].LoaiHanhKhach", "Người lớn"),
            [KeyValuePair[string,string]]::new("passengers[0].TenHanhKhach", "CONCURRENCY TEST $i"),
            [KeyValuePair[string,string]]::new("passengers[0].NgaySinh", "1995-01-01"),
            [KeyValuePair[string,string]]::new("passengers[0].QuocTich", "Việt Nam"),
            [KeyValuePair[string,string]]::new("passengers[0].LoaiGiayTo", "CCCD"),
            [KeyValuePair[string,string]]::new("passengers[0].SoGiayTo", ("07929900000" + $i)),
            [KeyValuePair[string,string]]::new("nguoiLienHe", "Tester $i"),
            [KeyValuePair[string,string]]::new("sdtLienHe", "090900000$i"),
            [KeyValuePair[string,string]]::new("emailLienHe", "test$i@skyvn.vn")
        ))

        $resp = $client.PostAsync("$url/Booking/HoldSeatsAndProceed", $form).Result
        [PSCustomObject]@{
            Worker = $i
            Location = $resp.Headers.Location.ToString()
            IsSuccessPaymentRedirect = ($resp.Headers.Location.ToString() -match "/Booking/Payment")
        }
    } -ArgumentList $baseUrl, $flight, $targetSeat, $idx
}

$results = $jobs | Wait-Job | Receive-Job
$jobs | Remove-Job
$results | Format-Table -AutoSize
```

## 4. Tiêu chí đạt chuẩn
- Trong 5 worker chạy đồng thời, `IsSuccessPaymentRedirect = True` **chỉ xuất hiện đúng 1 lần**.
- 4 worker còn lại bị điều hướng về `/Booking/SelectSeats` kèm `TempData["Error"]`.
- Truy vấn kiểm tra trong SQL Server (`SELECT COUNT(*) FROM VeMayBay WHERE MaChuyenBay = @cb AND MaGhe = @seat AND TrangThai IN (N'Đã đặt chỗ', N'Đã xuất vé', N'Đã Check-in')`) **luôn bằng đúng 1**.
