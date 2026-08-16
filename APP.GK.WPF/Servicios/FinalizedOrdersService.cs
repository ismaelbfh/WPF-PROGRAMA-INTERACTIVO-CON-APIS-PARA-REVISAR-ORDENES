using System.Collections.ObjectModel;
using APP.GK.WPF.Modelos;

namespace APP.GK.WPF.Servicios
{
    public class FinalizedOrdersService
    {
        // Colección de órdenes finalizadas
        public ObservableCollection<OrdenProduccion> FinalizedOrders { get; set; } = new ObservableCollection<OrdenProduccion>();
        // Colección de OP finalizadas (IDs)
        public ObservableCollection<string> FinalizedIds { get; set; } = new ObservableCollection<string>();
    }
}
