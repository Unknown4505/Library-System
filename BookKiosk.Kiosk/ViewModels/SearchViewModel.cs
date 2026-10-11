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

namespace BookKiosk.Kiosk.ViewModels
{
    public class SearchViewModel : BaseViewModel, IDisposable
    {
        private readonly NavigationService _navigationService;
        private readonly IBarcodeScanner _scanner;
        private readonly IBookKioskApiClient _apiClient;
        private readonly CartService _cartService;
        private readonly IDialogService _dialogService;

        private int _skip = 0;
        private const int Take = 10;
        private DispatcherTimer? _debounceTimer;
        private int _searchVersion = 0;

        private bool _hasNextPage = true;
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
                _keyword = value; 
                OnPropertyChanged(); 
                if (_debounceTimer != null)
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Start();
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
                    if (_debounceTimer != null)
                    {
                        _debounceTimer.Stop();
                        _debounceTimer.Start();
                    }
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
                Keyword = keyword;
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
            Categories.Clear();
            Categories.Add(new CategoryModel { Id = 0, Name = "Tất cả" });
            
            if (result != null && result.Success && result.Data != null)
            {
                foreach(var cat in result.Data)
                {
                    Categories.Add(new CategoryModel { Id = cat.CategoryId, Name = cat.Name });
                }
            }
            
            _selectedCategory = Categories[0];
            OnPropertyChanged(nameof(SelectedCategory));
        }

        private async void ExecuteSearch(bool reset)
        {
            // FE-13: Chống Load More đồng thời và dừng khi đã hết trang
            if (!reset)
            {
                if (IsLoading || !HasNextPage) return;
            }

            int currentVersion = ++_searchVersion;

            if (reset)
            {
                _skip = 0;
                HasNextPage = true;
                SearchResults.Clear();
            }

            IsLoading = true;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();

            try
            {
                int? categoryId = SelectedCategory?.Id == 0 ? null : SelectedCategory?.Id;
                int page = (_skip / Take) + 1;
                var result = await _apiClient.GetBooksAsync(page, Take, Keyword, categoryId);
                
                // FE-13: Bỏ qua nếu có yêu cầu tìm kiếm mới hơn đã được khởi chạy
                if (currentVersion != _searchVersion) return;

                if (result != null && result.Success && result.Data != null)
                {
                    HasNextPage = result.Data.HasNextPage;
                    foreach (var b in result.Data.Items)
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
                    _skip += Take;
                    ErrorMessage = "";
                }
                else
                {
                    // FE-13: Hiển thị lỗi API khi truy vấn thất bại
                    ErrorMessage = result?.Message ?? "Không thể tải danh sách sách từ máy chủ.";
                    if (reset)
                    {
                        HasNextPage = false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (currentVersion != _searchVersion) return;
                ErrorMessage = "Lỗi kết nối khi tải danh sách sách: " + ex.Message;
                if (reset)
                {
                    HasNextPage = false;
                }
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
            SelectedCategory = category;
            ExecuteSearch(true);
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

        public void Dispose()
        {
            // Giải phóng bộ nhớ, chống memory leak khi Navigate đi
            _scanner.BarcodeScanned -= Scanner_BarcodeScanned;
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Tick -= DebounceTimer_Tick;
                _debounceTimer = null;
            }
        }
    }
}
