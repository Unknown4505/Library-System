using System.Collections.ObjectModel;
using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
// using BookKiosk.Kiosk.Pages;

namespace BookKiosk.Kiosk.ViewModels
{
    public class HomePageViewModel : BaseViewModel
    {
        private readonly IBookService _bookService;
        private readonly NavigationService _navigationService;

        public ObservableCollection<BookModel> FeaturedBooks { get; } = new ObservableCollection<BookModel>();

        public ICommand SearchCommand { get; }
        public ICommand BookClickCommand { get; }

        public HomePageViewModel(IBookService bookService, NavigationService navigationService)
        {
            _bookService = bookService ?? throw new System.ArgumentNullException(nameof(bookService));
            _navigationService = navigationService ?? throw new System.ArgumentNullException(nameof(navigationService));

            SearchCommand = new RelayCommand(_ => ExecuteSearch());
            BookClickCommand = new RelayCommand(ExecuteBookClick);

            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            IsLoading = true;
            FeaturedBooks.Clear();
            var books = await _bookService.GetFeaturedBooksAsync();
            foreach (var book in books)
            {
                FeaturedBooks.Add(book);
            }
            IsLoading = false;
        }

        private void ExecuteSearch()
        {
            // Điều hướng sang SearchPage
            _navigationService.Navigate<Pages.SearchPage>();
        }

        private void ExecuteBookClick(object parameter)
        {
            if (parameter is int bookId)
            {
                // Điều hướng sang BookDetailPage và truyền tham số bookId
                _navigationService.Navigate<Pages.BookDetailPage>(bookId);
            }
        }
    }
}
