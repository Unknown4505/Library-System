using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using BookKiosk.Application.DTOs.Kiosk;
using BookKiosk.Domain.Enums;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Kiosk.Services;

namespace BookKiosk.Kiosk.ViewModels
{
    public enum IdleSlideGroup
    {
        Promotions,
        NewArrivals,
        BestSellers
    }

    public class AdvertisingBookDisplayModel
    {
        public int BookId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal SellingPrice { get; set; }
        public string PriceText => $"{SellingPrice:N0} đ";
        public int AvailableStock { get; set; }
        public string Subtitle { get; set; } = string.Empty;
        public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    }

    public class AdvertisingPromotionDisplayModel
    {
        public int PromotionId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal MinOrderValue { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountText => $"Giảm {DiscountAmount:N0} đ cho đơn từ {MinOrderValue:N0} đ";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PeriodText => $"Hiệu lực: {StartDate:dd/MM/yyyy} - {EndDate:dd/MM/yyyy}";
    }

    public class IdleViewModel : BaseViewModel
    {
        private readonly NavigationService _navigationService;
        private readonly AdvertisingService _advertisingService;
        private readonly IConfiguration? _configuration;

        private DispatcherTimer? _slideshowTimer;
        private DispatcherTimer? _promotionExpiryTimer;
        private CancellationTokenSource? _cts;
        private int _slideshowIntervalSeconds = 8;
        private readonly string _apiBaseUrl = "";
        private bool _isInitialized = false;
        private bool _hasScheduledRetryInSession = false;
        public Func<TimeSpan, CancellationToken, Task>? DelayProvider { get; set; }

        private readonly List<IdleSlideGroup> _activeGroups = new();
        private int _currentGroupIndex = 0;

        private IdleSlideGroup _currentGroup = IdleSlideGroup.Promotions;
        public IdleSlideGroup CurrentGroup
        {
            get => _currentGroup;
            private set
            {
                if (_currentGroup != value)
                {
                    _currentGroup = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsShowingPromotions));
                    OnPropertyChanged(nameof(IsShowingNewArrivals));
                    OnPropertyChanged(nameof(IsShowingBestSellers));
                    OnPropertyChanged(nameof(GroupBadgeText));
                }
            }
        }

        private bool _isFallback = false;
        public bool IsFallback
        {
            get => _isFallback;
            private set
            {
                if (_isFallback != value)
                {
                    _isFallback = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasContent));
                }
            }
        }

        public bool HasContent => !_isFallback && (_promotions.Count > 0 || _newArrivals.Count > 0 || _bestSellers.Count > 0);

        public bool IsShowingPromotions => !_isFallback && CurrentGroup == IdleSlideGroup.Promotions && _promotions.Count > 0;
        public bool IsShowingNewArrivals => !_isFallback && CurrentGroup == IdleSlideGroup.NewArrivals && _newArrivals.Count > 0;
        public bool IsShowingBestSellers => !_isFallback && CurrentGroup == IdleSlideGroup.BestSellers && _bestSellers.Count > 0;

        public string GroupBadgeText => CurrentGroup switch
        {
            IdleSlideGroup.Promotions => "ƯU ĐÃI NỔI BẬT",
            IdleSlideGroup.NewArrivals => "SÁCH MỚI VỀ",
            IdleSlideGroup.BestSellers => "SÁCH BÁN CHẠY",
            _ => "XEM SÁCH HAY"
        };

        private readonly ObservableCollection<AdvertisingPromotionDisplayModel> _promotions = new();
        public ObservableCollection<AdvertisingPromotionDisplayModel> Promotions => _promotions;

        private readonly ObservableCollection<AdvertisingBookDisplayModel> _newArrivals = new();
        public ObservableCollection<AdvertisingBookDisplayModel> NewArrivals => _newArrivals;

        private readonly ObservableCollection<AdvertisingBookDisplayModel> _bestSellers = new();
        public ObservableCollection<AdvertisingBookDisplayModel> BestSellers => _bestSellers;

        public ICommand StartCommand { get; }

        public IdleViewModel(NavigationService navigationService, AdvertisingService advertisingService, IConfiguration? configuration = null)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _advertisingService = advertisingService ?? throw new ArgumentNullException(nameof(advertisingService));
            _configuration = configuration;

            // Đọc cấu hình thời gian chuyển slide (mặc định 8s nếu thiếu hoặc <= 0)
            int configuredInterval = 8;
            if (_configuration != null)
            {
                var val = _configuration["KioskSettings:SlideshowIntervalSeconds"];
                if (int.TryParse(val, out var parsed) && parsed > 0)
                {
                    configuredInterval = parsed;
                }
                _apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "";
            }
            _slideshowIntervalSeconds = configuredInterval;

            StartCommand = new RelayCommand(_ => ExecuteStart());
        }

        public override void Initialize(object? parameter = null)
        {
            if (_isInitialized) return;
            _isInitialized = true;
            _hasScheduledRetryInSession = false;

            base.Initialize(parameter ?? string.Empty);

            // Bắt đầu một phiên Idle mới
            _advertisingService.StartNewIdleSession();

            // Dọn dẹp session cũ nếu có
            CleanupSession();

            _cts = new CancellationTokenSource();
            _ = LoadAdvertisingAsync(_cts.Token);
        }

        private async Task LoadAdvertisingAsync(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _advertisingService.GetAdvertisingDataAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested || !_isInitialized) return;

                if (result.IsFallback || !result.HasContent)
                {
                    ApplyFallbackState();

                    // Lập lịch auto-retry 1 lần nếu là lỗi tạm thời (timeout, network, 503, 5xx)
                    // Tuyệt đối KHÔNG retry với 401/403, empty-success, partial
                    if (result.IsTransientError && !_hasScheduledRetryInSession)
                    {
                        ScheduleAutoRetry(cancellationToken);
                    }
                    return;
                }

                ApplySuccessfulAdvertisingResult(result);
            }
            catch (OperationCanceledException)
            {
                // Bị hủy khi người dùng chạm màn hình hoặc rời trang -> nuốt im lặng
            }
            catch
            {
                ApplyFallbackState();
                if (!_hasScheduledRetryInSession && !cancellationToken.IsCancellationRequested && _isInitialized)
                {
                    ScheduleAutoRetry(cancellationToken);
                }
            }
        }

        private void ApplySuccessfulAdvertisingResult(AdvertisingResult result)
        {
            // Cập nhật danh sách hiển thị
            _promotions.Clear();
            foreach (var p in result.ValidPromotions)
            {
                _promotions.Add(new AdvertisingPromotionDisplayModel
                {
                    PromotionId = p.PromotionId,
                    Name = p.Name,
                    Description = p.Description,
                    MinOrderValue = p.OrderDiscount?.MinOrderValue ?? 0,
                    DiscountAmount = p.OrderDiscount?.DiscountAmount ?? 0,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate
                });
            }

            _newArrivals.Clear();
            foreach (var b in result.NewArrivals.Take(6))
            {
                _newArrivals.Add(new AdvertisingBookDisplayModel
                {
                    BookId = b.BookId,
                    Title = b.Title,
                    ImageUrl = FormatImageUrl(b.ImageUrl),
                    SellingPrice = b.SellingPrice,
                    AvailableStock = b.AvailableStock,
                    Subtitle = $"Nhập ngày {b.LastImportedAt:dd/MM/yyyy}"
                });
            }

            _bestSellers.Clear();
            foreach (var b in result.BestSellers.Take(6))
            {
                _bestSellers.Add(new AdvertisingBookDisplayModel
                {
                    BookId = b.BookId,
                    Title = b.Title,
                    ImageUrl = FormatImageUrl(b.ImageUrl),
                    SellingPrice = b.SellingPrice,
                    AvailableStock = b.AvailableStock,
                    Subtitle = $"Đã bán: {b.TotalSold} cuốn"
                });
            }

            // Xác định các nhóm có dữ liệu
            _activeGroups.Clear();
            if (_promotions.Count > 0) _activeGroups.Add(IdleSlideGroup.Promotions);
            if (_newArrivals.Count > 0) _activeGroups.Add(IdleSlideGroup.NewArrivals);
            if (_bestSellers.Count > 0) _activeGroups.Add(IdleSlideGroup.BestSellers);

            // Thanh lọc ngay nếu có khuyến mãi hết hạn tại thời điểm nạp
            PurgeExpiredPromotions();

            if (_activeGroups.Count == 0)
            {
                ApplyFallbackState();
                return;
            }

            IsFallback = false;
            _currentGroupIndex = 0;
            CurrentGroup = _activeGroups[0];
            RaiseSlideStateChanged();

            StartSlideshowTimer();
        }

        private void ScheduleAutoRetry(CancellationToken cancellationToken)
        {
            if (_hasScheduledRetryInSession) return;
            _hasScheduledRetryInSession = true;

            _ = Task.Run(async () =>
            {
                try
                {
                    var delayTask = DelayProvider != null
                        ? DelayProvider(TimeSpan.FromSeconds(30), cancellationToken)
                        : Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                    await delayTask;

                    if (cancellationToken.IsCancellationRequested || !_isInitialized) return;

                    var retryResult = await _advertisingService.GetAdvertisingDataAsync(cancellationToken);

                    if (cancellationToken.IsCancellationRequested || !_isInitialized) return;

                    if (retryResult.IsSuccess && retryResult.HasContent)
                    {
                        Action updateUi = () =>
                        {
                            if (cancellationToken.IsCancellationRequested || !_isInitialized) return;
                            ApplySuccessfulAdvertisingResult(retryResult);
                        };

                        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(updateUi);
                        }
                        else
                        {
                            updateUi();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Bị hủy khi người dùng chạm màn hình hoặc rời trang -> nuốt im lặng
                }
                catch
                {
                    // Lỗi khi retry -> giữ nguyên fallback state
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Loại bỏ các khuyến mãi đã quá hạn khỏi bộ sưu tập hiển thị mà không cần gọi lại API.
        /// Tự động cập nhật lại activeGroups và trạng thái Fallback nếu tất cả nhóm đều trống.
        /// </summary>
        private void PurgeExpiredPromotions()
        {
            Action purgeAction = () =>
            {
                DateTime now = _advertisingService.UtcNowProvider();

                // Lọc bỏ promotion hết hạn (EndDate <= now hoặc StartDate > now)
                var expiredPromos = _promotions.Where(p => p.EndDate <= now || p.StartDate > now).ToList();
                if (expiredPromos.Count > 0)
                {
                    foreach (var p in expiredPromos)
                    {
                        _promotions.Remove(p);
                    }
                }

                // Cập nhật lại activeGroups an toàn nếu nhóm Promotions không còn phần tử nào
                if (_promotions.Count == 0 && _activeGroups.Contains(IdleSlideGroup.Promotions))
                {
                    RemoveActiveGroupSafely(IdleSlideGroup.Promotions);
                }

                // Lập lịch cho đợt hết hạn tiếp theo của promotion còn lại (nếu có)
                ScheduleNextPromotionExpiryTimer();

                RaiseSlideStateChanged();
            };

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(purgeAction);
            }
            else
            {
                purgeAction();
            }
        }

        /// <summary>
        /// Xóa an toàn một nhóm khỏi _activeGroups và đồng bộ lại _currentGroupIndex để tránh lặp hoặc nhảy cóc slide.
        /// </summary>
        private void RemoveActiveGroupSafely(IdleSlideGroup groupToRemove)
        {
            int removedIndex = _activeGroups.IndexOf(groupToRemove);
            if (removedIndex == -1) return;

            _activeGroups.RemoveAt(removedIndex);

            // Kịch bản A: Nếu không còn nhóm nào có dữ liệu -> Chuyển sang Fallback 'Chạm để bắt đầu'
            if (_activeGroups.Count == 0)
            {
                ApplyFallbackState();
                return;
            }

            // Kịch bản B: Nhóm bị xóa nằm TRƯỚC nhóm đang hiển thị
            // Do các nhóm phía sau dồn lên trước 1 vị trí, cần giảm _currentGroupIndex để giữ nguyên slide hiện tại
            if (removedIndex < _currentGroupIndex)
            {
                _currentGroupIndex--;
            }
            // Kịch bản C: Chính nhóm đang hiển thị bị gỡ bỏ
            else if (removedIndex == _currentGroupIndex)
            {
                if (_currentGroupIndex >= _activeGroups.Count)
                {
                    _currentGroupIndex = 0;
                }
                CurrentGroup = _activeGroups[_currentGroupIndex];
            }
            // Kịch bản D: removedIndex > _currentGroupIndex -> giữ nguyên _currentGroupIndex

            // Chuẩn hóa index an toàn phòng ngừa biên
            if (_currentGroupIndex >= _activeGroups.Count)
            {
                _currentGroupIndex = 0;
                CurrentGroup = _activeGroups[0];
            }

            // Nếu chỉ còn 1 nhóm duy nhất không phải Promotions thì dừng timer luân chuyển
            if (_activeGroups.Count <= 1 && !_activeGroups.Contains(IdleSlideGroup.Promotions))
            {
                StopSlideshowTimer();
            }
        }

        /// <summary>
        /// Lên lịch cho Timer độc lập kích hoạt chính xác tại thời điểm EndDate gần nhất của khuyến mãi.
        /// </summary>
        private void ScheduleNextPromotionExpiryTimer()
        {
            StopPromotionExpiryTimer();

            if (_promotions.Count == 0) return;

            DateTime now = _advertisingService.UtcNowProvider();

            // Tìm thời điểm hết hạn sớm nhất của các promotion còn hiệu lực
            var activePromos = _promotions.Where(p => p.EndDate > now && p.StartDate <= now).ToList();
            if (activePromos.Count == 0)
            {
                return;
            }

            DateTime earliestExpiry = activePromos.Min(p => p.EndDate);
            TimeSpan delay = earliestExpiry - now;

            if (delay <= TimeSpan.Zero)
            {
                PurgeExpiredPromotions();
                return;
            }

            if (delay.TotalDays > 1)
            {
                delay = TimeSpan.FromHours(24);
            }

            Action startTimer = () =>
            {
                StopPromotionExpiryTimer();

                _promotionExpiryTimer = new DispatcherTimer
                {
                    Interval = delay
                };
                _promotionExpiryTimer.Tick += PromotionExpiryTimer_Tick;
                _promotionExpiryTimer.Start();
            };

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(startTimer);
            }
            else
            {
                startTimer();
            }
        }

        private void PromotionExpiryTimer_Tick(object? sender, EventArgs e)
        {
            StopPromotionExpiryTimer();
            PurgeExpiredPromotions();
        }

        private void StopPromotionExpiryTimer()
        {
            if (_promotionExpiryTimer != null)
            {
                _promotionExpiryTimer.Stop();
                _promotionExpiryTimer.Tick -= PromotionExpiryTimer_Tick;
                _promotionExpiryTimer = null;
            }
        }

        private void RaiseSlideStateChanged()
        {
            Action notify = () =>
            {
                OnPropertyChanged(nameof(HasContent));
                OnPropertyChanged(nameof(CurrentGroup));
                OnPropertyChanged(nameof(IsShowingPromotions));
                OnPropertyChanged(nameof(IsShowingNewArrivals));
                OnPropertyChanged(nameof(IsShowingBestSellers));
                OnPropertyChanged(nameof(GroupBadgeText));
            };

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(notify);
            }
            else
            {
                notify();
            }
        }

        private string? FormatImageUrl(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return null;

            if (rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return rawUrl;
            }

            if (!string.IsNullOrWhiteSpace(_apiBaseUrl))
            {
                return _apiBaseUrl.TrimEnd('/') + "/" + rawUrl.TrimStart('/');
            }

            return rawUrl;
        }

        private void ApplyFallbackState()
        {
            StopSlideshowTimer();
            StopPromotionExpiryTimer();
            _promotions.Clear();
            _newArrivals.Clear();
            _bestSellers.Clear();
            _activeGroups.Clear();
            IsFallback = true;
            RaiseSlideStateChanged();
        }

        private void StartSlideshowTimer()
        {
            StopSlideshowTimer();

            // Nếu không có nhóm nào hoặc chỉ có 1 nhóm tĩnh (không có Promotions cần theo dõi hết hạn) thì không cần timer
            if (_activeGroups.Count == 0 || (_activeGroups.Count == 1 && !_activeGroups.Contains(IdleSlideGroup.Promotions)))
            {
                return;
            }

            _slideshowTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_slideshowIntervalSeconds)
            };
            _slideshowTimer.Tick += SlideshowTimer_Tick;
            _slideshowTimer.Start();
        }

        private void SlideshowTimer_Tick(object? sender, EventArgs e)
        {
            // 1. Luôn thanh lọc khuyến mãi hết hạn trước khi luân chuyển slide
            PurgeExpiredPromotions();

            // Nếu sau khi lọc không còn nhóm nào (đã chuyển sang Fallback) thì không tiếp tục
            if (_activeGroups.Count == 0) return;

            // 2. Luân chuyển sang nhóm kế tiếp nếu có từ 2 nhóm trở lên
            if (_activeGroups.Count > 1)
            {
                _currentGroupIndex = (_currentGroupIndex + 1) % _activeGroups.Count;
                CurrentGroup = _activeGroups[_currentGroupIndex];
                RaiseSlideStateChanged();
            }
            else if (_activeGroups.Count == 1 && CurrentGroup != _activeGroups[0])
            {
                _currentGroupIndex = 0;
                CurrentGroup = _activeGroups[0];
                RaiseSlideStateChanged();
            }
        }

        private void StopSlideshowTimer()
        {
            if (_slideshowTimer != null)
            {
                _slideshowTimer.Stop();
                _slideshowTimer.Tick -= SlideshowTimer_Tick;
                _slideshowTimer = null;
            }
        }

        public void Cleanup()
        {
            _isInitialized = false;
            CleanupSession();
        }

        private void CleanupSession()
        {
            StopSlideshowTimer();
            StopPromotionExpiryTimer();
            _hasScheduledRetryInSession = false;

            if (_cts != null)
            {
                try
                {
                    if (!_cts.IsCancellationRequested)
                    {
                        _cts.Cancel();
                    }
                }
                catch (ObjectDisposedException) { }
                _cts.Dispose();
                _cts = null;
            }
        }

        private void ExecuteStart()
        {
            Cleanup();
            _navigationService.Navigate<SearchPage>();
        }
    }
}

