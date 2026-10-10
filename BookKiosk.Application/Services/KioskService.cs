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
        if (kiosk == null) throw new System.Collections.Generic.KeyNotFoundException("Kiosk không tồn tại.");

        if (!Enum.IsDefined(typeof(KioskStatus), request.Status))
            throw new ArgumentException("Trạng thái Kiosk không hợp lệ.");

        kiosk.LastPingAt = DateTime.UtcNow;
        kiosk.Status = (KioskStatus)request.Status;

        if (!string.IsNullOrEmpty(request.ErrorCode) || !string.IsNullOrEmpty(request.ErrorMessage))
        {
            var errorCode = request.ErrorCode ?? "UNKNOWN";
            var openIncident = await _kioskRepository.GetOpenIncidentAsync(kiosk.KioskId, errorCode);
            if (openIncident == null)
            {
                await _kioskRepository.AddIncidentAsync(new KioskIncident
                {
                    KioskId = kiosk.KioskId,
                    ErrorCode = errorCode,
                    Description = request.ErrorMessage ?? "Không có mô tả chi tiết",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
