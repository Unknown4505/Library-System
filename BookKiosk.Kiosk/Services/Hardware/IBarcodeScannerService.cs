using System;

namespace BookKiosk.Kiosk.Services.Hardware
{
    public interface IBarcodeScannerService
    {
        event EventHandler<string> BarcodeScanned;
        void PushBarcode(string barcode);
    }
}
