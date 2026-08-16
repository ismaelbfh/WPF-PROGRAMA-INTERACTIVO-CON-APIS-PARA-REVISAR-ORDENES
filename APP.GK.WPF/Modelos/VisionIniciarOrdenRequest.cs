using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APP.GK.WPF.Modelos
{
    public class VisionIniciarOrdenRequest
    {
        public string OrdenFabricacion { get; set; }
        public string CodigoProducto { get; set; }
        public string DescripcionProducto { get; set; }
        public DateTime? FechaProduccion { get; set; }
        public string? Linea { get; set; }
        public string TipoEtiqueta { get; set; }
        public string? PosicionQr { get; set; }
        public string CodigoEsperado { get; set; }
        public string? IpCamara { get; set; }
    }
}
