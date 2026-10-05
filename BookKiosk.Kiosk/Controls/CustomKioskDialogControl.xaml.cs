using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BookKiosk.Kiosk.Controls
{
    public partial class CustomKioskDialogControl : UserControl
    {
        private TaskCompletionSource<bool>? _tcs;

        public CustomKioskDialogControl()
        {
            InitializeComponent();
        }

        public Task<bool> ShowDialogAsync(string message, bool isTwoButtons)
        {
            MessageText.Text = message;
            
            if (isTwoButtons)
            {
                CancelButton.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(OkButton, 1);
                OkButton.Margin = new Thickness(10, 0, 0, 0);
            }
            else
            {
                CancelButton.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(OkButton, 2);
                OkButton.Margin = new Thickness(0, 0, 0, 0);
            }

            this.Visibility = Visibility.Visible;
            _tcs = new TaskCompletionSource<bool>();
            return _tcs.Task;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(false);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
            _tcs?.TrySetResult(true);
        }
    }
}
