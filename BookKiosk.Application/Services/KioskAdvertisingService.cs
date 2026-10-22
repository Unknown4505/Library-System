using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Application.Interfaces.Repositories;
using BookKiosk.Application.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookKiosk.Application.Services;

public class KioskAdvertisingService : IKioskAdvertisingService
{
    private readonly IKioskAdvertisingRepository _repository;

    public KioskAdvertisingService(IKioskAdvertisingRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<KioskAdvertisingResult> GetAdvertisingAsync()
    {
        var utcNow = DateTime.UtcNow;
        var unavailable = new List<string>();

        List<AdvertisingPromotionDto> activePromotions;
        List<AdvertisingNewArrivalDto> newArrivals;
        List<AdvertisingBestSellerDto> bestSellers;

        // 1. Khối Khuyến mãi
        try
        {
            activePromotions = await _repository.GetActivePromotionsAsync(utcNow) ?? new List<AdvertisingPromotionDto>();
        }
        catch
        {
            unavailable.Add("activePromotions");
            activePromotions = new List<AdvertisingPromotionDto>();
        }

        // 2. Khối Sách mới về
        try
        {
            newArrivals = await _repository.GetNewArrivalsAsync(utcNow, 10) ?? new List<AdvertisingNewArrivalDto>();
        }
        catch
        {
            unavailable.Add("newArrivals");
            newArrivals = new List<AdvertisingNewArrivalDto>();
        }

        // 3. Khối Sách bán chạy
        try
        {
            bestSellers = await _repository.GetBestSellersAsync(utcNow, 10) ?? new List<AdvertisingBestSellerDto>();
        }
        catch
        {
            unavailable.Add("bestSellers");
            bestSellers = new List<AdvertisingBestSellerDto>();
        }

        var dto = new KioskAdvertisingDto
        {
            ActivePromotions = activePromotions,
            NewArrivals = newArrivals,
            BestSellers = bestSellers,
            UnavailableSections = unavailable
        };

        // Đánh giá tổng hợp kết quả
        if (unavailable.Count == 3)
        {
            return new KioskAdvertisingResult
            {
                Status = AdvertisingStatus.ServiceUnavailable,
                Message = "Nội dung quảng cáo tạm thời không khả dụng.",
                Data = null!
            };
        }

        if (unavailable.Count > 0)
        {
            return new KioskAdvertisingResult
            {
                Status = AdvertisingStatus.PartialSuccess,
                Message = "Một số nội dung quảng cáo tạm thời không khả dụng.",
                Data = dto
            };
        }

        var isEmpty = dto.ActivePromotions.Count == 0 && dto.NewArrivals.Count == 0 && dto.BestSellers.Count == 0;
        return new KioskAdvertisingResult
        {
            Status = AdvertisingStatus.Success,
            Message = isEmpty ? "Hiện chưa có nội dung quảng cáo phù hợp." : "Lấy dữ liệu quảng cáo thành công.",
            Data = dto
        };
    }
}
