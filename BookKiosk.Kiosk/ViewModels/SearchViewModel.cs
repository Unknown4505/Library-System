using System.Collections.ObjectModel;
using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;
using BookKiosk.Kiosk.Services.Api;
using BookKiosk.Kiosk.Services.Hardware;
using System.Windows.Threading;
using System;
using System.Linq;
using System.Threading;

namespace BookKiosk.Kiosk.ViewModels
{
    public class SearchViewModel : BaseViewModel, IDisposable
    {
        private record SearchQuerySnapshot(string? Keyword, int? CategoryId, int Page);

        private enum SearchExecutionStatus
        {
            NotStarted,
            InFlight,
            Completed,
            CancelledOnDeactivate,
            Failed
        }

        private readonly NavigationService _navigationService;
        private readonly IBarcodeScanner _scanner;
        private readonly IBookKioskApiClient _apiClient;
        private readonly CartService _cartService;
        private readonly IDialogService _dialogService;

        private int _skip = 0;
        private const int Take = 10;
        private DispatcherTimer? _debounceTimer;
        private int _searchVersion = 0;
        private CancellationTokenSource? _searchCts;
        private SearchQuerySnapshot? _activeQuerySnapshot;
        private bool _isActive = false;
        private SearchExecutionStatus _searchStatus = SearchExecutionStatus.NotStarted;
        private bool _suppressCategorySearch = false;

        private bool _hasNextPage = false;
        public bool HasNextPage
        {
            get => _hasNextPage;
            private set
            {
                _hasNextPage = value;
                OnPropertyChanged();
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollection<BookModel> SearchResults { get; } = new ObservableCollection<BookModel>();
        public ObservableCollection<CategoryModel> Categories { get; } = new ObservableCollection<CategoryModel>();

        private string _keyword;
        public string Keyword 
        {
            get => _keyword;
            set 
            { 
                if (_keyword != value)
                {
                    _keyword = value; 
                    OnPropertyChanged(); 

                    // SEARCH-04: Invalidate con trỏ phân trang ngay khi keyword đổi
                    HasNextPage = false;

                    if (_debounceTimer != null)
                    {
                        _debounceTimer.Stop();
                        _debounceTimer.Start();
                    }
                }
            }
        }

        private CategoryModel _selectedCategory;
        public CategoryModel SelectedCategory
        {
            get => _selectedCategory;
            set 
            { 
                if (_selectedCategory != value)
                {
                    _selectedCategory = value; 
                    OnPropertyChanged(); 
                    if (_suppressCategorySearch) return;

                    // SEARCH-04: Invalidate con trỏ phân trang ngay khi Category đổi
                    HasNextPage = false;

                    // BUG-01: Đổi Category hủy debounce keyword đang chờ và tìm ngay lập tức
                    _debounceTimer?.Stop();
                    ExecuteSearch(true);
                }
            }
        }

        private bool _isScannerOpen;
        public bool IsScannerOpen
        {
            get => _isScannerOpen;
            set { _isScannerOpen = value; OnPropertyChanged(); }
        }

        public ICommand SearchCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand BookClickCommand { get; }
        
        public ICommand OpenScannerCommand { get; }
        public ICommand CloseScannerCommand { get; }
        public ICommand SimulateScanCommand { get; }

        public SearchViewModel(
            NavigationService navigationService,
            IBarcodeScanner scanner,
            IBookKioskApiClient apiClient,
            CartService cartService,
            IDialogService dialogService)
        {
            _navigationService = navigationService;
            _scanner = scanner;
            _apiClient = apiClient;
            _cartService = cartService;
            _dialogService = dialogService;

            SearchCommand = new RelayCommand(_ => ExecuteSearch(true));
            FilterCommand = new RelayCommand(cat => ExecuteFilter(cat as CategoryModel));
            LoadMoreCommand = new RelayCommand(_ => ExecuteSearch(false), _ => !IsLoading && HasNextPage);
            BookClickCommand = new RelayCommand(ExecuteBookClick);
            
            OpenScannerCommand = new RelayCommand(_ => OpenScanner());
            CloseScannerCommand = new RelayCommand(_ => CloseScanner());
            SimulateScanCommand = new RelayCommand(p => ExecuteScan(p as string));

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _debounceTimer.Tick += DebounceTimer_Tick;

            // Lắng nghe sự kiện quét từ máy quét (phần cứng hoặc Mock)
            _scanner.BarcodeScanned += Scanner_BarcodeScanned;

            LoadCategoriesAsync();
            ExecuteSearch(true);
        }

        public override void Initialize(object parameter)
        {
            if (parameter is string keyword && !string.IsNullOrWhiteSpace(keyword))
            {
                _keyword = keyword;
                OnPropertyChanged(nameof(Keyword));
                ExecuteSearch(true);
            }
        }

        private void OpenScanner()
        {
            IsScannerOpen = true;
            _scanner.StartListening();
        }

        private void CloseScanner()
        {
            IsScannerOpen = false;
            _scanner.StopListening();
        }

        private void Scanner_BarcodeScanned(object? sender, string barcode)
        {
            // Sự kiện có thể bắn từ background thread (tuỳ loại máy quét), 
            // nên cần InvokeAsync để đảm bảo an toàn cập nhật UI
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ExecuteScan(barcode));
        }

        private async void ExecuteScan(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            // Dừng lắng nghe ngay để chống quét liên tục (debounce barcode)
            _scanner.StopListening();
            IsLoading = true;

            try
            {
                // Gọi API đã tự động bắt lỗi 404
                var result = await _apiClient.GetBookByBarcodeAsync(barcode);

                if (result == null || !result.Success || result.Data == null)
                {
                    // Quăng Dialog lỗi (message có thể là 404 "Không tìm thấy sách...")
                    await _dialogService.ShowDialogAsync(result?.Message ?? "Lỗi không xác định khi quét mã vạch.", false);
                    
                    // Mở camera lại cho khách quét cuốn khác
                    _scanner.StartListening();
                }
                else
                {
                    var bookDto = result.Data;

                    // LUẬT THÉP: Kiểm tra tồn kho
                    if (bookDto.AvailableStock > 0)
                    {
                        var bookModel = new BookModel 
                        {
                            BookId = bookDto.BookId,
                            Barcode = bookDto.Barcode,
                            Title = bookDto.Title,
                            Author = bookDto.Author,
                            ImageUrl = bookDto.ImageUrl,
                            SellingPrice = bookDto.SellingPrice,
                            AvailableStock = bookDto.AvailableStock,
                            AreaName = bookDto.AreaName
                        };
                        
                        _cartService.AddItem(bookModel);
                        
                        // Đóng Popup và báo thành công
                        IsScannerOpen = false;
                        await _dialogService.ShowDialogAsync($"Đã thêm {bookDto.Title} vào giỏ hàng!", false);
                    }
                    else
                    {
                        await _dialogService.ShowDialogAsync($"Sách {bookDto.Title} đã hết hàng hoặc vừa bị mua mất, vui lòng chọn cuốn khác.", false);
                        _scanner.StartListening();
                    }
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void LoadCategoriesAsync()
        {
            var result = await _apiClient.GetCategoriesAsync();
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            Action updateCategories = () =>
            {
                Categories.Clear();
                Categories.Add(new CategoryModel { Id = 0, Name = "Tất cả" });
                
                if (result != null && result.Success && result.Data != null)
                {
                    foreach(var cat in result.Data)
                    {
                        Categories.Add(new CategoryModel { Id = cat.CategoryId, Name = cat.Name });
                    }
                }
                
                _suppressCategorySearch = true;
                try
                {
                    SelectedCategory = Categories[0];
                }
                finally
                {
                    _suppressCategorySearch = false;
                }
            };

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(updateCategories);
            }
            else
            {
                updateCategories();
            }
        }

        private async void ExecuteSearch(bool reset)
        {
            // FE-13 & SEARCH-04: Chống Load More đồng thời và dừng khi đã hết trang hoặc chưa có active snapshot
            if (!reset)
            {
                if (IsLoading || !HasNextPage || _activeQuerySnapshot == null) return;
            }

            // BUG-01: Hủy CTS cũ và tạo CTS mới cho mỗi lần tìm
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var cancellationToken = _searchCts.Token;

            int currentVersion = Interlocked.Increment(ref _searchVersion);
            _searchStatus = SearchExecutionStatus.InFlight;

            int? categoryId;
            string? keyword;
            int targetPage;

            if (reset)
            {
                _skip = 0;
                targetPage = 1;
                HasNextPage = false;
                ErrorMessage = "";
                categoryId = SelectedCategory?.Id == 0 ? null : SelectedCategory?.Id;
                keyword = Keyword;

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => SearchResults.Clear());
                }
                else
                {
                    SearchResults.Clear();
                }
            }
            else
            {
                // SEARCH-04: Load More CHỈ dùng snapshot của truy vấn hiện hành đã hoàn tất
                keyword = _activeQuerySnapshot!.Keyword;
                categoryId = _activeQuerySnapshot.CategoryId;
                targetPage = _activeQuerySnapshot.Page + 1;
            }

            var snapshot = new SearchQuerySnapshot(keyword, categoryId, targetPage);

            IsLoading = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            try
            {
                if (cancellationToken.IsCancellationRequested || currentVersion != _searchVersion)
                {
                    return;
                }

                var result = await _apiClient.GetBooksAsync(snapshot.Page, Take, snapshot.Keyword, snapshot.CategoryId);
                
                // BUG-01: SAU MỖI await: Bỏ qua nếu token đã hủy hoặc version không còn hiện hành
                if (cancellationToken.IsCancellationRequested || currentVersion != _searchVersion)
                {
                    return;
                }

                if (result != null && result.Success && result.Data != null)
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    Action populateAction = () =>
                    {
                        HasNextPage = result.Data.HasNextPage;
                        foreach (var b in result.Data.Items)
                        {
                            // FE-13: Chống nạp trùng dữ liệu sách
                            if (!SearchResults.Any(x => x.BookId == b.BookId))
                            {
                                SearchResults.Add(new BookModel 
                                {
                                    BookId = b.BookId,
                                    Barcode = b.Barcode,
                                    Title = b.Title,
                                    Author = b.Author,
                                    ImageUrl = b.ImageUrl,
                                    SellingPrice = b.SellingPrice,
                                    AvailableStock = b.AvailableStock,
                                    CategoryId = b.CategoryId,
                                    AreaName = b.AreaName
                                });
                            }
                        }
                    };

                    if (dispatcher != null && !dispatcher.CheckAccess())
                    {
                        dispatcher.Invoke(populateAction);
                    }
                    else
                    {
                        populateAction();
                    }

                    _activeQuerySnapshot = snapshot;
                    _skip = snapshot.Page * Take;
                    ErrorMessage = "";
                    _searchStatus = SearchExecutionStatus.Completed;
                }
                else
                {
                    // FE-13: Hiển thị lỗi API khi truy vấn thất bại
                    ErrorMessage = result?.Message ?? "Không thể tải danh sách sách từ máy chủ.";
                    if (reset)
                    {
                        HasNextPage = false;
                        _activeQuerySnapshot = null;
                    }
                    _searchStatus = SearchExecutionStatus.Failed;
                }
            }
            catch (OperationCanceledException)
            {
                // BUG-01: Nuốt OperationCanceledException im lặng
                return;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested || currentVersion != _searchVersion)
                {
                    return;
                }
                ErrorMessage = "Lỗi kết nối khi tải danh sách sách: " + ex.Message;
                if (reset)
                {
                    HasNextPage = false;
                    _activeQuerySnapshot = null;
                }
                _searchStatus = SearchExecutionStatus.Failed;
            }
            finally
            {
                if (currentVersion == _searchVersion)
                {
                    IsLoading = false;
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private void ExecuteFilter(CategoryModel category)
        {
            if (category == null) return;
            // BUG-01: SelectedCategory setter đã tự động hủy debounce và gọi ExecuteSearch(true)
            // Không gọi ExecuteSearch(true) lần hai ở đây để đếm request = đúng 1
            SelectedCategory = category;
        }

        private void ExecuteBookClick(object parameter)
        {
            if (parameter is int bookId)
            {
                _navigationService.Navigate<BookDetailPage>(bookId);
            }
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            ExecuteSearch(true);
        }

        public void Activate()
        {
            if (_isActive) return;
            _isActive = true;

            if (_debounceTimer == null)
            {
                _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _debounceTimer.Tick += DebounceTimer_Tick;
            }

            // BUG-02: Idempotent scanner subscription (gỡ trước rồi gán)
            _scanner.BarcodeScanned -= Scanner_BarcodeScanned;
            _scanner.BarcodeScanned += Scanner_BarcodeScanned;

            // SEARCH-05: Chỉ khôi phục tìm kiếm khi truy vấn trước bị Deactivate hủy giữa chừng
            // Nếu đang InFlight (request từ constructor đang bay) -> KHÔNG gọi lại (chống duplicate)
            // Nếu đã Completed (kể cả kết quả rỗng) hoặc Failed -> KHÔNG gọi lại (giữ nguyên kết quả/lỗi)
            if (_searchStatus == SearchExecutionStatus.CancelledOnDeactivate)
            {
                ExecuteSearch(true);
            }
            else if (_searchStatus == SearchExecutionStatus.NotStarted)
            {
                ExecuteSearch(true);
            }
        }

        public void Deactivate()
        {
            if (!_isActive) return;
            _isActive = false;

            _debounceTimer?.Stop();
            _scanner.BarcodeScanned -= Scanner_BarcodeScanned;

            if (IsLoading)
            {
                _searchStatus = SearchExecutionStatus.CancelledOnDeactivate;
                _searchCts?.Cancel();
                IsLoading = false;
            }
        }

        public void Dispose()
        {
            Deactivate();
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = null;
            if (_debounceTimer != null)
            {
                _debounceTimer.Tick -= DebounceTimer_Tick;
                _debounceTimer = null;
            }
        }
    }
}
