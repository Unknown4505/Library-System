using System.Windows.Controls;
using BookKiosk.Kiosk.ViewModels;

namespace BookKiosk.Kiosk.Pages
{
    public partial class SearchPage : Page
    {
        public SearchPage(SearchViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void SearchTextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            VirtualKeyboard.Visibility = System.Windows.Visibility.Visible;
        }

        private void ClearButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (this.DataContext is SearchViewModel vm)
            {
                vm.Keyword = string.Empty;
            }
            SearchTextBox.Focus();
        }

        private void VirtualKeyboard_EnterClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            VirtualKeyboard.Visibility = System.Windows.Visibility.Collapsed;
            if (this.DataContext is SearchViewModel vm && vm.SearchCommand.CanExecute(null))
            {
                vm.SearchCommand.Execute(null);
            }
        }

        private void Page_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Bỏ qua nếu bàn phím đang ẩn
            if (VirtualKeyboard.Visibility == System.Windows.Visibility.Collapsed) return;

            // Kiểm tra phần tử được click
            System.Windows.DependencyObject clickedElement = e.OriginalSource as System.Windows.DependencyObject;

            bool clickedInsideSearchContainer = IsDescendantOf(SearchContainer, clickedElement);
            bool clickedInsideKeyboard = IsDescendantOf(VirtualKeyboard, clickedElement);

            // Nếu click ra ngoài TextBox và ngoài bàn phím thì ẩn bàn phím
            if (!clickedInsideSearchContainer && !clickedInsideKeyboard)
            {
                VirtualKeyboard.Visibility = System.Windows.Visibility.Collapsed;
                System.Windows.Input.Keyboard.ClearFocus();
            }
        }

        private bool IsDescendantOf(System.Windows.DependencyObject parent, System.Windows.DependencyObject child)
        {
            if (parent == null || child == null) return false;
            
            System.Windows.DependencyObject current = child;
            while (current != null)
            {
                if (current == parent) return true;
                
                // Tránh lỗi khi gặp phần tử không thuộc VisualTree (ví dụ Run)
                if (current is System.Windows.Media.Visual || current is System.Windows.Media.Media3D.Visual3D)
                {
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
                else
                {
                    current = System.Windows.LogicalTreeHelper.GetParent(current);
                }
            }
            return false;
        }
    }
}
