using System.Windows;

namespace BookKiosk.Kiosk.Controls
{
    public partial class PasswordDialog : Window
    {
        private readonly string? _expectedPassword;
        public bool IsAuthenticated { get; private set; }

        public PasswordDialog(string? expectedPassword = null)
        {
            InitializeComponent();
            _expectedPassword = expectedPassword;

#if !DEBUG
            if (string.IsNullOrWhiteSpace(_expectedPassword))
            {
                txtError.Text = "Chế độ bảo trì bị khóa: Chưa cấu hình xác thực an toàn (Fail-closed).";
                txtError.Visibility = Visibility.Visible;
                pwdBox.IsEnabled = false;
            }
#endif
            pwdBox.Focus();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Bỏ mật khẩu hardcode "123456". Production/Release phải fail closed nếu thiếu cấu hình.
            if (string.IsNullOrWhiteSpace(_expectedPassword))
            {
                txtError.Text = "Chế độ bảo trì bị khóa: Chưa cấu hình xác thực an toàn (Fail-closed).";
                txtError.Visibility = Visibility.Visible;
                IsAuthenticated = false;
                return;
            }

            if (pwdBox.Password == _expectedPassword)
            {
                IsAuthenticated = true;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                txtError.Text = "Mật khẩu không chính xác!";
                txtError.Visibility = Visibility.Visible;
                pwdBox.Clear();
                pwdBox.Focus();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
