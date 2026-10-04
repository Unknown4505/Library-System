using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;

namespace BookKiosk.Kiosk.ViewModels
{
    public class BookDetailViewModel : BaseViewModel
    {
        private readonly IBookService _bookService;
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

        public BookDetailViewModel(IBookService bookService, NavigationService navigationService, CartService cartService)
        {
            _bookService = bookService;
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
                Book = await _bookService.GetBookByIdAsync(bookId);
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
