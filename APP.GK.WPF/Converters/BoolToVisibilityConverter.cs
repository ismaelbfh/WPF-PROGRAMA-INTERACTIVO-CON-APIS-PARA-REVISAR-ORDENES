using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace APP.GK.WPF.Converters
{
    /// <summary>
    /// Convierte un valor booleano a un valor de Visibility.
    /// Si el valor es true se retorna Visibility.Visible, de lo contrario Visibility.Collapsed.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        // Convierte de bool a Visibility.
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                return flag ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        // Convierte de Visibility a bool.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
}
