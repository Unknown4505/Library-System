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
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public List<AdvertisingPromotionDto> ValidPromotions { get; set; } = new();
        public List<AdvertisingNewArrivalDto> NewArrivals { get; set; } = new();
        public List<AdvertisingBestSellerDto> BestSellers { get; set; } = new();
        public List<string> UnavailableSections { get; set; } = new();

        public bool HasPromotions => ValidPromotions.Count > 0;
        public bool HasNewArrivals => NewArrivals.Count > 0;
        public bool HasBestSellers => BestSellers.Count > 0;
        public bool HasContent => HasPromotions || HasNewArrivals || HasBestSellers;

        public bool IsTransientError => !IsSuccess && (ErrorCode == "TIMEOUT_ERROR" || ErrorCode == "NETWORK_ERROR" || ErrorCode == "SERVICE_UNAVAILABLE" || ErrorCode == "SERVER_ERROR");
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
                // 1. Kiểm tra cache còn hạn hay không (Success có data hoặc rỗng đều là cache hit hợp lệ)
                if (_cachedDto != null && now < _cacheExpiresAtUtc)
                {
                    return BuildResultFromDto(_cachedDto, now);
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
                            ErrorCode = "COOLDOWN_ACTIVE",
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
                    _cachedDto = null;
                    _cacheExpiresAtUtc = DateTime.MinValue;
                    return new AdvertisingResult
                    {
                        IsSuccess = false,
                        IsFallback = true,
                        ErrorCode = "NETWORK_ERROR",
                        ErrorMessage = $"Lỗi kết nối khi tải quảng cáo: {ex.Message}"
                    };
                }

                // 4. Phân tích kết quả từ API wrapper
                if (response == null || !response.Success || response.Data == null)
                {
                    _lastFetchWasError = true;
                    _cachedDto = null;
                    _cacheExpiresAtUtc = DateTime.MinValue;
                    return new AdvertisingResult
                    {
                        IsSuccess = false,
                        IsFallback = true,
                        ErrorCode = response?.Code ?? "SERVER_ERROR",
                        ErrorMessage = response?.Message ?? "Không thể lấy dữ liệu quảng cáo từ máy chủ."
                    };
                }

                // Quyết định bắt buộc: PARTIAL_SUCCESS KHÔNG cache vì các nhóm lỗi có thể hồi phục nhanh
                bool isPartial = string.Equals(response.Code, "PARTIAL_SUCCESS", StringComparison.OrdinalIgnoreCase)
                    || (response.Data.UnavailableSections != null && response.Data.UnavailableSections.Count > 0);

                if (!isPartial)
                {
                    _lastFetchWasError = false;
                    _cachedDto = response.Data;

                    // Tính toán thời gian hết hạn cache: Mặc định tối đa 5 phút
                    DateTime defaultExpiry = now.AddMinutes(5);
                    DateTime calculatedExpiry = defaultExpiry;

                    // Hết hạn sớm theo promotion có EndDate gần nhất (chỉ áp dụng khi có promotion hợp lệ)
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
                }
                else
                {
                    // PARTIAL_SUCCESS: thành công nhưng có nhóm lỗi -> KHÔNG cache
                    _lastFetchWasError = false;
                    _cachedDto = null;
                    _cacheExpiresAtUtc = DateTime.MinValue;
                }

                var result = BuildResultFromDto(response.Data, now);
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
