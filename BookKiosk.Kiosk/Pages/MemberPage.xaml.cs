using System.Windows.Controls;

namespace BookKiosk.Kiosk.Pages
{
    public partial class MemberPage : Page
    {
        public MemberPage()
        {
            InitializeComponent();
        }

        public MemberPage(ViewModels.MemberViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void Numpad_EnterClicked(object sender, System.Windows.RoutedEventArgs e)
        {
            KeyboardPopup.IsOpen = false;
            System.Windows.Input.Keyboard.ClearFocus();

            if (DataContext is ViewModels.MemberViewModel vm && vm.ConfirmCommand.CanExecute(null))
            {
                vm.ConfirmCommand.Execute(null);
            }
        }

        private void TextBox_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                KeyboardPopup.PlacementTarget = tb;
                KeyboardPopup.IsOpen = true;
                System.Windows.Data.Binding binding = new System.Windows.Data.Binding();
                if (tb.Name == "PhoneTextBox")
                {
                    binding.Path = new System.Windows.PropertyPath("PhoneNumber");
                }
                else if (tb.Name == "PointsTextBox")
                {
                    binding.Path = new System.Windows.PropertyPath("PointsUsedText");
                }

                if (binding.Path != null)
                {
                    binding.Mode = System.Windows.Data.BindingMode.TwoWay;
                    binding.UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged;
                    Numpad.SetBinding(Controls.VirtualKeyboardControl.TargetTextProperty, binding);
                }
            }
        }

        private void Page_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!KeyboardPopup.IsOpen) return;

            System.Windows.DependencyObject clickedElement = e.OriginalSource as System.Windows.DependencyObject;

            bool clickedInsidePhoneBox = IsDescendantOf(PhoneTextBox, clickedElement);
            bool clickedInsidePointsBox = PointsTextBox != null && IsDescendantOf(PointsTextBox, clickedElement);
            bool clickedInsideKeyboard = KeyboardPopup.Child != null && IsDescendantOf(KeyboardPopup.Child, clickedElement);

            if (!clickedInsidePhoneBox && !clickedInsidePointsBox && !clickedInsideKeyboard)
            {
                KeyboardPopup.IsOpen = false;
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
