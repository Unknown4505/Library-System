using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BookKiosk.Kiosk.Converters
{
    public class DecimalGreaterThanZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isGreater = false;
            if (value is decimal d && d > 0)
            {
                isGreater = true;
            }
            else if (value is int i && i > 0)
            {
                isGreater = true;
            }
            else if (value is double db && db > 0)
            {
                isGreater = true;
            }

            if (parameter is string paramStr && string.Equals(paramStr, "Invert", StringComparison.OrdinalIgnoreCase))
            {
                isGreater = !isGreater;
            }

            return isGreater ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
