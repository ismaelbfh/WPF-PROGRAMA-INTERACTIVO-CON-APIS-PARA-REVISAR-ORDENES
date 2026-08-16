using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WPF.GK.BARTENDER.Vistas
{
    /// <summary>
    /// Lógica de interacción para OrdersView.xaml
    /// </summary>
    public partial class OrdersView : UserControl
    {
        public OrdersView()
        {
            InitializeComponent();
        }

        private void OrdersDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Reenviar la rueda al ScrollViewer externo para que la vista baje/suba
            e.Handled = true;

            // e.Delta > 0: rueda hacia arriba
            // e.Delta < 0: rueda hacia abajo
            MainScroll.ScrollToVerticalOffset(MainScroll.VerticalOffset - e.Delta);
        }
    }
}
