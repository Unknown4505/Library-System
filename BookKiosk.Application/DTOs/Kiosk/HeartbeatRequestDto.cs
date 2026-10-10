namespace BookKiosk.Application.DTOs.Kiosk;

public class HeartbeatRequestDto
{
    public int KioskId { get; set; }
    public int Status { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
