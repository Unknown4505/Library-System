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
        private readonly IBarcodeScannerService _barcodeScannerService;
        private readonly IBookService _bookService;
        private readonly NavigationService _navigationService;

        public ObservableCollection<CartItemModel> CartItems => _cartService.Items;
        public decimal SubTotal => _cartService.GetTotalAmount();
        public bool HasItems => CartItems.Count > 0;

        public ICommand IncreaseCommand { get; }
        public ICommand DecreaseCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand CheckoutCommand { get; }

        public CartViewModel(
            CartService cartService, 
            IBarcodeScannerService barcodeScannerService, 
            IBookService bookService, 
            NavigationService navigationService)
        {
            _cartService = cartService;
            _barcodeScannerService = barcodeScannerService;
            _bookService = bookService;
            _navigationService = navigationService;

            // Subscribe to Service events
            _cartService.CartChanged += OnCartChanged;
            _barcodeScannerService.BarcodeScanned += OnBarcodeScanned;

            // Initialize Commands
            IncreaseCommand = new RelayCommand(ExecuteIncrease);
            DecreaseCommand = new RelayCommand(ExecuteDecrease);
            RemoveCommand = new RelayCommand(ExecuteRemove);
            CheckoutCommand = new RelayCommand(ExecuteCheckout, _ => HasItems);
        }

        private void OnCartChanged()
        {
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(HasItems));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private async void OnBarcodeScanned(object sender, string barcode)
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

        private void ExecuteCheckout(object parameter)
        {
            if (!HasItems) return;
            
            // Chuyển sang màn hình Thành viên (MemberPage)
            _navigationService.Navigate<Pages.MemberPage>();
        }
    }
}
