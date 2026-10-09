using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Hardware;
using BookKiosk.Kiosk.Services.Api;

namespace BookKiosk.Kiosk.ViewModels
{
    public class CartViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly IBarcodeScanner _barcodeScanner;
        private readonly IBookKioskApiClient _apiClient;
        private readonly NavigationService _navigationService;
        private readonly IDialogService _dialogService;

        public ObservableCollection<CartItemModel> CartItems => _cartService.Items;
        public decimal SubTotal => _cartService.GetTotalAmount();
        public bool HasItems => CartItems.Count > 0;

        public ICommand IncreaseCommand { get; }
        public ICommand DecreaseCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand CheckoutCommand { get; }

        public CartViewModel(
            CartService cartService, 
            IBarcodeScanner barcodeScanner, 
            IBookKioskApiClient apiClient, 
            NavigationService navigationService,
            IDialogService dialogService)
        {
            _cartService = cartService;
            _barcodeScanner = barcodeScanner;
            _apiClient = apiClient;
            _navigationService = navigationService;
            _dialogService = dialogService;

            // Subscribe to Service events
            _cartService.CartChanged += OnCartChanged;
            _barcodeScanner.BarcodeScanned += OnBarcodeScanned;
            _barcodeScanner.StartListening();

            // Initialize Commands
            IncreaseCommand = new RelayCommand(ExecuteIncrease);
            DecreaseCommand = new RelayCommand(ExecuteDecrease);
            RemoveCommand = new RelayCommand(ExecuteRemove);
            CheckoutCommand = new RelayCommand(ExecuteCheckout);
        }

        private void OnCartChanged()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(HasItems));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async void OnBarcodeScanned(object? sender, string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return;

            var result = await _apiClient.GetBookByBarcodeAsync(barcode);
            if (result != null && result.Success && result.Data != null)
            {
                var bookDto = result.Data;
                // Ensure UI updates are made on the Dispatcher thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (bookDto.AvailableStock <= 0)
                    {
                        // Could trigger a toast notification here
                        return;
                    }
                    var bookModel = new BookModel 
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
                    _cartService.AddItem(bookModel);
                    
                    // Phát âm thanh báo hiệu quét mã thành công
                    System.Media.SystemSounds.Beep.Play();
                });
            }
        }

        private void ExecuteIncrease(object parameter)
        {
            if (parameter is int bookId)
            {
                var book = CartItems.FirstOrDefault(i => i.Book.BookId == bookId)?.Book;
                if (book != null) _cartService.AddItem(book);
            }
        }

        private void ExecuteDecrease(object parameter)
        {
            if (parameter is int bookId)
            {
                _cartService.DecreaseItem(bookId);
            }
        }

        private void ExecuteRemove(object parameter)
        {
            if (parameter is int bookId)
            {
                _cartService.RemoveItem(bookId);
            }
        }

        private async void ExecuteCheckout(object parameter)
        {
            if (!HasItems)
            {
                await _dialogService.ShowDialogAsync("Bạn chưa có sách để thanh toán!", false);
                return;
            }
            
            bool isConfirm = await _dialogService.ShowDialogAsync("Bạn có chắc chắn muốn thanh toán các cuốn sách này?", true);
            if (isConfirm)
            {
                _navigationService.Navigate<Pages.MemberPage>();
            }
        }
    }
}
