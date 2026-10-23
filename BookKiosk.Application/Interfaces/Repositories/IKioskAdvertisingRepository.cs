using BookKiosk.Application.DTOs.Kiosk;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BookKiosk.Application.Interfaces.Repositories;

public interface IKioskAdvertisingRepository
{
    Task<List<AdvertisingPromotionDto>> GetActivePromotionsAsync(DateTime utcNow);
    Task<List<AdvertisingNewArrivalDto>> GetNewArrivalsAsync(DateTime utcNow, int limit = 10);
    Task<List<AdvertisingBestSellerDto>> GetBestSellersAsync(DateTime utcNow, int limit = 10);
}
