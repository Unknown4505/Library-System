namespace BookKiosk.Application.DTOs.Kiosk;

public class HeartbeatRequestDto
{
    public int KioskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
