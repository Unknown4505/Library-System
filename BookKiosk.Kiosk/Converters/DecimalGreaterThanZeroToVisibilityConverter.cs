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
            if (value is decimal d && d > 0)
            {
                return Visibility.Visible;
            }
            if (value is int i && i > 0)
            {
                return Visibility.Visible;
            }
            if (value is double db && db > 0)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
