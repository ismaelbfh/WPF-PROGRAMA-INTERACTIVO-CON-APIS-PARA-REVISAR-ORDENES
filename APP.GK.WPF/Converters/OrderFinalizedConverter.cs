using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Data;
using APP.GK.WPF.Modelos;

namespace APP.GK.WPF.Converters
{
    public class OrderFinalizedConverter : IMultiValueConverter
    {
        // Valores: [0] es la orden actual, [1] es la ObservableCollection<string> FinalizedIds
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is OrdenProduccion order &&
                values[1] is ObservableCollection<string> finalizedIds)
            {
                return finalizedIds.Contains(order.OP);
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
