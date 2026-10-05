using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Services.Hardware;

namespace BookKiosk.Kiosk.ViewModels
{
    public class CartViewModel : BaseViewModel
    {
        private readonly CartService _cartService;
        private readonly IBarcodeScanner _barcodeScanner;
        private readonly IBookService _bookService;
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
            IBookService bookService, 
            NavigationService navigationService,
            IDialogService dialogService)
        {
            _cartService = cartService;
            _barcodeScanner = barcodeScanner;
            _bookService = bookService;
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
            // Parse barcode. Assuming Barcode equals BookId for Mock Scanner
            if (int.TryParse(barcode, out int bookId))
            {
                var book = await _bookService.GetBookByIdAsync(bookId);
                if (book != null)
                {
                    // Ensure UI updates are made on the Dispatcher thread
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (book.AvailableStock <= 0)
                        {
                            // Could trigger a toast notification here
                            return;
                        }
                        _cartService.AddItem(book);
                        
                        // Phát âm thanh báo hiệu quét mã thành công
                        System.Media.SystemSounds.Beep.Play();
                    });
                }
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
