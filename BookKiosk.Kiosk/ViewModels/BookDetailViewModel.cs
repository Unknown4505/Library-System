using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Api;

namespace BookKiosk.Kiosk.ViewModels
{
    public class BookDetailViewModel : BaseViewModel
    {
        private readonly IBookKioskApiClient _apiClient;
        private readonly NavigationService _navigationService;
        private readonly CartService _cartService;

        private BookModel _book;
        public BookModel Book 
        {
            get => _book;
            set { _book = value; OnPropertyChanged(); }
        }

        public ICommand AddToCartCommand { get; }
        public ICommand BackCommand { get; }

        public BookDetailViewModel(IBookKioskApiClient apiClient, NavigationService navigationService, CartService cartService)
        {
            _apiClient = apiClient;
            _navigationService = navigationService;
            _cartService = cartService;

            AddToCartCommand = new RelayCommand(ExecuteAddToCart, _ => !HasError && Book != null);
            BackCommand = new RelayCommand(_ => _navigationService.GoBack());
        }

        public override async void Initialize(object parameter)
        {
            if (parameter is int bookId)
            {
                IsLoading = true;
                ErrorMessage = "";
                SuccessMessage = "";
                Book = null;
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();

                try
                {
                    var result = await _apiClient.GetBookByIdAsync(bookId);

                    if (result != null && result.Success && result.Data != null)
                    {
                        var bookDto = result.Data;
                        Book = new BookModel
                        {
                            BookId = bookDto.BookId,
                            Barcode = bookDto.Barcode,
                            Title = bookDto.Title,
                            Author = bookDto.Author,
                            Publisher = bookDto.Publisher,
                            ImageUrl = bookDto.ImageUrl,
                            SellingPrice = bookDto.SellingPrice,
                            AvailableStock = bookDto.AvailableStock,
                            CategoryId = bookDto.CategoryId,
                            CategoryName = bookDto.CategoryName,
                            AreaName = bookDto.AreaName
                        };
                    }
                    else
                    {
                        // FE-DETAIL-01: Gán lỗi khi API trả về thất bại hoặc 404
                        ErrorMessage = result?.Message ?? "Không thể tải thông tin sách. Vui lòng quay lại và thử lại.";
                    }
                }
                catch (Exception ex)
                {
                    // FE-DETAIL-01: Bắt lỗi mạng/exception không mong muốn
                    ErrorMessage = "Lỗi kết nối khi tải chi tiết sách: " + ex.Message;
                }
                finally
                {
                    IsLoading = false;
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private void ExecuteAddToCart(object parameter)
        {
            // Reset cả 2 trạng thái trước mỗi lần thực thi
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (Book == null) return;
            if (Book.AvailableStock <= 0)
            {
                ErrorMessage = "Sách đã hết hàng!";
                return;
            }

            _cartService.AddItem(Book);
            SuccessMessage = "Đã thêm vào giỏ hàng!";
        }
    }
}
