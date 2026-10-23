using BookKiosk.Application.DTOs.Kiosk;
using System.Threading.Tasks;

namespace BookKiosk.Application.Interfaces.Services;

public interface IKioskAdvertisingService
{
    Task<KioskAdvertisingResult> GetAdvertisingAsync();
}

public sealed class KioskAdvertisingResult
{
    public KioskAdvertisingDto Data { get; init; } = new();
    public AdvertisingStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}

public enum AdvertisingStatus
{
    Success,
    PartialSuccess,
    ServiceUnavailable
}
