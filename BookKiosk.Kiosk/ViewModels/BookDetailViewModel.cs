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

            AddToCartCommand = new RelayCommand(ExecuteAddToCart);
            BackCommand = new RelayCommand(_ => _navigationService.GoBack());
        }

        public override async void Initialize(object parameter)
        {
            if (parameter is int bookId)
            {
                IsLoading = true;
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
                        ImageUrl = bookDto.ImageUrl,
                        SellingPrice = bookDto.SellingPrice,
                        AvailableStock = bookDto.AvailableStock,
                        CategoryId = bookDto.CategoryId,
                        AreaName = bookDto.AreaName
                    };
                }
                IsLoading = false;
            }
        }

        private void ExecuteAddToCart(object parameter)
        {
            if (Book == null) return;
            if (Book.AvailableStock <= 0)
            {
                ErrorMessage = "Sách đã hết hàng!";
                return;
            }
            
            _cartService.AddItem(Book);
            ErrorMessage = "Đã thêm vào giỏ hàng!"; // Hiển thị popup tạm
        }
    }
}
