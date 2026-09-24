namespace BookKiosk.Kiosk.Services.Hardware;

public interface IBarcodeScanner
{
    // Sự kiện bắn ra khi quét được mã vạch
    event EventHandler<string> BarcodeScanned;
    
    void StartListening();
    void StopListening();
}
