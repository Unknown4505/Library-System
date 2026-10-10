using System.Windows;

namespace BookKiosk.Kiosk.Controls
{
    public partial class PasswordDialog : Window
    {
        public bool IsAuthenticated { get; private set; }

        public PasswordDialog()
        {
            InitializeComponent();
            pwdBox.Focus();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // Mật khẩu cứng giả lập, thực tế nên lấy từ config hoặc CSDL
            if (pwdBox.Password == "123456")
            {
                IsAuthenticated = true;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
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
