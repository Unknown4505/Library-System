using System;

namespace BookKiosk.Kiosk.Services.Hardware
{
    public class MockBarcodeScannerService : IBarcodeScannerService
    {
        public event EventHandler<string> BarcodeScanned;

        public void PushBarcode(string barcode)
        {
            if (!string.IsNullOrWhiteSpace(barcode))
            {
                BarcodeScanned?.Invoke(this, barcode);
            }
        }
    }
}
