using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BookKiosk.Application.DTOs.Common;
using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Domain.Enums;
using BookKiosk.Kiosk.Services.Api;

namespace BookKiosk.Kiosk.Services
{
    public class AdvertisingResult
    {
        public bool IsSuccess { get; set; }
        public bool IsFallback { get; set; }
        public string? ErrorMessage { get; set; }
        public List<AdvertisingPromotionDto> ValidPromotions { get; set; } = new();
        public List<AdvertisingNewArrivalDto> NewArrivals { get; set; } = new();
        public List<AdvertisingBestSellerDto> BestSellers { get; set; } = new();
        public List<string> UnavailableSections { get; set; } = new();

        public bool HasPromotions => ValidPromotions.Count > 0;
        public bool HasNewArrivals => NewArrivals.Count > 0;
        public bool HasBestSellers => BestSellers.Count > 0;
        public bool HasContent => HasPromotions || HasNewArrivals || HasBestSellers;
    }

    /// <summary>
    /// Quản lý dữ liệu quảng cáo Idle theo mô hình Singleton:
    /// - In-memory cache tối đa 5 phút, hết hạn sớm theo promotion gần nhất.
    /// - Lọc bỏ promotion hết hạn hoặc PromotionType != OrderDiscount.
    /// - Quản lý tối đa 1 auto-retry sau >= 30 giây trong mỗi lượt vào IdlePage.
    /// - Thread-safe (SemaphoreSlim) và hỗ trợ giả lập đồng hồ (TimeProvider/delegate).
    /// </summary>
    public class AdvertisingService
    {
        private readonly IBookKioskApiClient _apiClient;
        private readonly SemaphoreSlim _lock = new(1, 1);

        // In-memory cache
        private KioskAdvertisingDto? _cachedDto;
        private DateTime _cacheExpiresAtUtc = DateTime.MinValue;

        // Quản lý retry và cooldown
        private DateTime _lastFetchAttemptUtc = DateTime.MinValue;
        private bool _hasRetriedThisSession = false;
        private bool _lastFetchWasError = false;

        // Injectable clock provider cho testing giả lập thời gian không cần chờ thật
        public Func<DateTime> UtcNowProvider { get; set; } = () => DateTime.UtcNow;

        public AdvertisingService(IBookKioskApiClient apiClient)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        /// <summary>
        /// Gọi mỗi khi Kiosk bắt đầu một phiên vào IdlePage mới để reset quota 1 lần retry tự động.
        /// </summary>
        public void StartNewIdleSession()
        {
            _hasRetriedThisSession = false;
        }

        public async Task<AdvertisingResult> GetAdvertisingDataAsync(CancellationToken cancellationToken = default)
        {
            DateTime now = UtcNowProvider();

            await _lock.WaitAsync(cancellationToken);
            try
            {
                // 1. Kiểm tra cache còn hạn hay không
                if (_cachedDto != null && now < _cacheExpiresAtUtc)
                {
                    var cachedResult = BuildResultFromDto(_cachedDto, now);
                    if (cachedResult.HasContent)
                    {
                        return cachedResult;
                    }
                }

                // 2. Kiểm tra quy tắc retry / cooldown nếu lần trước bị lỗi
                if (_lastFetchWasError)
                {
                    var timeSinceLastAttempt = now - _lastFetchAttemptUtc;

                    if (_hasRetriedThisSession || timeSinceLastAttempt < TimeSpan.FromSeconds(30))
                    {
                        // Đã retry lượt này rồi hoặc chưa đủ 30 giây cooldown -> Fallback ngay, không request storm
                        return new AdvertisingResult
                        {
                            IsSuccess = false,
                            IsFallback = true,
                            ErrorMessage = "Đang trong thời gian chờ phục hồi dữ liệu quảng cáo."
                        };
                    }

                    // Đủ điều kiện thử lại 1 lần duy nhất cho phiên Idle này
                    _hasRetriedThisSession = true;
                }

                _lastFetchAttemptUtc = now;

                // 3. Gọi API lấy dữ liệu mới
                ApiResponseDto<KioskAdvertisingDto>? response;
                try
                {
                    response = await _apiClient.GetAdvertisingAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _lastFetchWasError = true;
                    return new AdvertisingResult
                    {
                        IsSuccess = false,
                        IsFallback = true,
                        ErrorMessage = $"Lỗi kết nối khi tải quảng cáo: {ex.Message}"
                    };
                }

                // 4. Phân tích kết quả từ API wrapper
                if (response == null || !response.Success || response.Data == null)
                {
                    _lastFetchWasError = true;
                    return new AdvertisingResult
                    {
                        IsSuccess = false,
                        IsFallback = true,
                        ErrorMessage = response?.Message ?? "Không thể lấy dữ liệu quảng cáo từ máy chủ."
                    };
                }

                // Thành công (bao gồm cả SUCCESS và PARTIAL_SUCCESS)
                _lastFetchWasError = false;
                _cachedDto = response.Data;

                // Tính toán thời gian hết hạn cache: Mặc định tối đa 5 phút
                DateTime defaultExpiry = now.AddMinutes(5);
                DateTime calculatedExpiry = defaultExpiry;

                // Kiểm tra promotion: Hết hạn sớm theo promotion có EndDate gần nhất
                if (_cachedDto.ActivePromotions != null && _cachedDto.ActivePromotions.Count > 0)
                {
                    var validPromos = _cachedDto.ActivePromotions
                        .Where(p => p.PromotionType == PromotionType.OrderDiscount && p.EndDate > now)
                        .ToList();

                    if (validPromos.Count > 0)
                    {
                        DateTime earliestPromoEnd = validPromos.Min(p => p.EndDate);
                        if (earliestPromoEnd < calculatedExpiry)
                        {
                            calculatedExpiry = earliestPromoEnd;
                        }
                    }
                }

                _cacheExpiresAtUtc = calculatedExpiry;

                var result = BuildResultFromDto(_cachedDto, now);
                result.UnavailableSections = response.Data.UnavailableSections ?? new List<string>();
                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        private AdvertisingResult BuildResultFromDto(KioskAdvertisingDto dto, DateTime now)
        {
            var result = new AdvertisingResult
            {
                IsSuccess = true,
                IsFallback = false,
                UnavailableSections = dto.UnavailableSections ?? new List<string>()
            };

            // Lọc khuyến mãi: Chỉ OrderDiscount (Type 1), còn hiệu lực theo UTC
            if (dto.ActivePromotions != null)
            {
                result.ValidPromotions = dto.ActivePromotions
                    .Where(p => p.PromotionType == PromotionType.OrderDiscount
                                && p.StartDate <= now
                                && p.EndDate > now
                                && p.OrderDiscount != null)
                    .ToList();
            }

            // Sách mới về
            if (dto.NewArrivals != null)
            {
                result.NewArrivals = dto.NewArrivals
                    .Where(b => b.AvailableStock > 0)
                    .Take(10)
                    .ToList();
            }

            // Sách bán chạy
            if (dto.BestSellers != null)
            {
                result.BestSellers = dto.BestSellers
                    .Where(b => b.AvailableStock > 0)
                    .Take(10)
                    .ToList();
            }

            // Nếu cả 3 nhóm đều không có mục nào sau khi lọc -> đánh dấu fallback
            if (!result.HasContent)
            {
                result.IsFallback = true;
            }

            return result;
        }

        /// <summary>
        /// Xóa cache (hữu ích cho testing và bảo trì)
        /// </summary>
        public void InvalidateCache()
        {
            _cachedDto = null;
            _cacheExpiresAtUtc = DateTime.MinValue;
            _lastFetchWasError = false;
            _hasRetriedThisSession = false;
        }
    }
}
