using BookKiosk.Domain.Enums;

namespace BookKiosk.Application.DTOs.Kiosk;

/// <summary>
/// Shared response data for the Kiosk advertising endpoint.
/// Querying, filtering, ordering and fallback behavior belong to the API/UI layers.
/// </summary>
public sealed class KioskAdvertisingDto
{
    public List<AdvertisingPromotionDto> ActivePromotions { get; init; } = new();
    public List<AdvertisingNewArrivalDto> NewArrivals { get; init; } = new();
    public List<AdvertisingBestSellerDto> BestSellers { get; init; } = new();
    public List<string> UnavailableSections { get; init; } = new();
}

public sealed class AdvertisingPromotionDto
{
    public int PromotionId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public PromotionType PromotionType { get; init; }
    public AdvertisingOrderDiscountDto? OrderDiscount { get; init; }
    public List<AdvertisingProductDiscountDto> ProductDiscounts { get; init; } = new();
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
}

public sealed class AdvertisingOrderDiscountDto
{
    public decimal MinOrderValue { get; init; }
    public decimal DiscountAmount { get; init; }
}

/// <summary>Book fields safe for display in Kiosk advertising.</summary>
public class AdvertisingBookDto
{
    public int BookId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public decimal SellingPrice { get; init; }
    public int AvailableStock { get; init; }
}

public sealed class AdvertisingProductDiscountDto : AdvertisingBookDto
{
    public decimal DiscountAmount { get; init; }
}

public sealed class AdvertisingNewArrivalDto : AdvertisingBookDto
{
    public DateTime LastImportedAt { get; init; }
}

public sealed class AdvertisingBestSellerDto : AdvertisingBookDto
{
    public int TotalSold { get; init; }
}
