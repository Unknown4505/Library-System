using System.Windows;
using System.Windows.Controls;

namespace BookKiosk.Kiosk.Controls
{
    public partial class VirtualKeyboardQWERTYControl : UserControl
    {
        public static readonly DependencyProperty TargetTextProperty =
            DependencyProperty.Register(
                nameof(TargetText),
                typeof(string),
                typeof(VirtualKeyboardQWERTYControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string TargetText
        {
            get => (string)GetValue(TargetTextProperty);
            set => SetValue(TargetTextProperty, value);
        }

        public static readonly RoutedEvent EnterClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(EnterClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(VirtualKeyboardQWERTYControl));

        public event RoutedEventHandler EnterClicked
        {
            add { AddHandler(EnterClickedEvent, value); }
            remove { RemoveHandler(EnterClickedEvent, value); }
        }

        public VirtualKeyboardQWERTYControl()
        {
            InitializeComponent();
        }

        private void Char_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string character)
            {
                TargetText = (TargetText ?? string.Empty) + character.ToLower(); // Tìm kiếm thường dùng chữ thường
            }
        }

        private void Space_Click(object sender, RoutedEventArgs e)
        {
            TargetText = (TargetText ?? string.Empty) + " ";
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TargetText))
            {
                TargetText = TargetText.Substring(0, TargetText.Length - 1);
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(EnterClickedEvent));
        }
    }
}
