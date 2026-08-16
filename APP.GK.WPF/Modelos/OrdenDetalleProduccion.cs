using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APP.GK.WPF.Modelos
{
    public class OrdenDetalleProduccion
    {
        public string OPFabricacion { get; set; }
        public string CodigoProducto { get; set; }
        public string Descripcion { get; set; }
        public DateTime Fecha { get; set; }
        public string Linea { get; set; }
        public string CodigoEmbalaje { get; set; }
        public decimal CantidadAProducirEmbalajes { get; set; }
    }
}
