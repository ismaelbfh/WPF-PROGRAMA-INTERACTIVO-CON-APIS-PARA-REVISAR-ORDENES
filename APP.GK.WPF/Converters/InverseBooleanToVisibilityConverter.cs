using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace APP.GK.WPF.Converters
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        // Convierte el booleano en Visibility, invirtiendo el valor:
        // true -> Collapsed, false -> Visible
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolean)
            {
                return boolean ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        // Convierte de vuelta Visibility a booleano (inverso)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return false;
        }
    }
}
