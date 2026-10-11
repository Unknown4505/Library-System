using BookKiosk.Application.DTOs.Kiosk;
using System.Threading.Tasks;

namespace BookKiosk.Application.Interfaces.Services;

public interface IKioskService
{
    Task<bool> HandleHeartbeatAsync(HeartbeatRequestDto request);
}
