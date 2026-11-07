using System;
using System.Globalization;
using System.Windows.Data;
using BookKiosk.Domain.Enums;

namespace BookKiosk.Kiosk.Converters
{
    public class PromotionTypeToStringConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string label = string.Empty;

            if (value is PromotionType pt)
            {
                label = pt switch
                {
                    PromotionType.OrderDiscount => "Khuyến mãi theo hóa đơn",
                    PromotionType.ProductDiscount => "Khuyến mãi theo từng sách",
                    _ => string.Empty
                };
            }
            else if (value is int intVal)
            {
                label = intVal switch
                {
                    1 => "Khuyến mãi theo hóa đơn",
                    2 => "Khuyến mãi theo từng sách",
                    _ => string.Empty
                };
            }

            if (string.IsNullOrEmpty(label))
                return string.Empty;

            if (parameter is string paramStr && string.Equals(paramStr, "Parens", StringComparison.OrdinalIgnoreCase))
            {
                return $"({label})";
            }

            return label;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
