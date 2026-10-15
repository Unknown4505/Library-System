using System.Windows;
using System.Windows.Controls;

namespace BookKiosk.Kiosk.Controls
{
    public partial class VirtualKeyboardControl : UserControl
    {
        // Dependency Property for TargetText allowing two-way binding with ViewModels
        public static readonly DependencyProperty TargetTextProperty =
            DependencyProperty.Register(
                nameof(TargetText),
                typeof(string),
                typeof(VirtualKeyboardControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string TargetText
        {
            get => (string)GetValue(TargetTextProperty);
            set => SetValue(TargetTextProperty, value);
        }

        // Event for when Enter is clicked
        public static readonly RoutedEvent EnterClickedEvent = EventManager.RegisterRoutedEvent(
            nameof(EnterClicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(VirtualKeyboardControl));

        public event RoutedEventHandler EnterClicked
        {
            add { AddHandler(EnterClickedEvent, value); }
            remove { RemoveHandler(EnterClickedEvent, value); }
        }

        public VirtualKeyboardControl()
        {
            InitializeComponent();
        }

        private void Number_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content != null)
            {
                SetCurrentValue(TargetTextProperty, (TargetText ?? string.Empty) + btn.Content.ToString());
            }
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TargetText))
            {
                SetCurrentValue(TargetTextProperty, TargetText.Substring(0, TargetText.Length - 1));
            }
        }

        private void Enter_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(EnterClickedEvent));
        }
    }
}
