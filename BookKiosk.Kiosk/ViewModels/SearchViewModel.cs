using System.Collections.ObjectModel;
using System.Windows.Input;
using BookKiosk.Kiosk.Models;
using BookKiosk.Kiosk.Services;
using BookKiosk.Kiosk.Pages;
using System.Windows.Threading;
using System;

namespace BookKiosk.Kiosk.ViewModels
{
    public class SearchViewModel : BaseViewModel
    {
        private readonly IBookService _bookService;
        private readonly NavigationService _navigationService;
        private int _skip = 0;
        private const int Take = 10;
        private DispatcherTimer _debounceTimer;

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
            set { _selectedCategory = value; OnPropertyChanged(); }
        }

        public ICommand SearchCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand BookClickCommand { get; }

        public SearchViewModel(IBookService bookService, NavigationService navigationService)
        {
            _bookService = bookService;
            _navigationService = navigationService;

            SearchCommand = new RelayCommand(_ => ExecuteSearch(true));
            FilterCommand = new RelayCommand(cat => ExecuteFilter(cat as CategoryModel));
            LoadMoreCommand = new RelayCommand(_ => ExecuteSearch(false));
            BookClickCommand = new RelayCommand(ExecuteBookClick);

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _debounceTimer.Tick += (s, e) => 
            {
                _debounceTimer.Stop();
                ExecuteSearch(true);
            };

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

        private async void LoadCategoriesAsync()
        {
            var cats = await _bookService.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add(new CategoryModel { Id = 0, Name = "Tất cả" });
            foreach(var cat in cats) Categories.Add(cat);
            SelectedCategory = Categories[0];
        }

        private async void ExecuteSearch(bool reset)
        {
            if (reset)
            {
                _skip = 0;
                SearchResults.Clear();
            }

            IsLoading = true;
            int? categoryId = SelectedCategory?.Id == 0 ? null : SelectedCategory?.Id;
            var books = await _bookService.SearchBooksAsync(Keyword, categoryId, _skip, Take);
            
            foreach(var b in books) SearchResults.Add(b);
            
            _skip += Take;
            IsLoading = false;
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
    }
}
