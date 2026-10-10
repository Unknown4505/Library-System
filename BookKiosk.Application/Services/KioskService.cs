using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public class KioskService : IKioskService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IKioskRepository _kioskRepository;

    public KioskService(IUnitOfWork unitOfWork, IKioskRepository kioskRepository)
    {
        _unitOfWork = unitOfWork;
        _kioskRepository = kioskRepository;
    }

    public async Task<bool> HandleHeartbeatAsync(HeartbeatRequestDto request)
    {
        var kiosk = await _kioskRepository.GetByIdAsync(request.KioskId);
        if (kiosk == null) throw new Exception("Kiosk không tồn tại.");

        kiosk.LastPingAt = DateTime.Now;
        
        if (Enum.TryParse<KioskStatus>(request.Status, true, out var status))
        {
            kiosk.Status = status;
        }

        if (!string.IsNullOrEmpty(request.ErrorCode) || !string.IsNullOrEmpty(request.ErrorMessage))
        {
            await _kioskRepository.AddIncidentAsync(new KioskIncident
            {
                KioskId = kiosk.KioskId,
                ErrorCode = request.ErrorCode ?? "UNKNOWN",
                Description = request.ErrorMessage ?? "Không có mô tả chi tiết",
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
