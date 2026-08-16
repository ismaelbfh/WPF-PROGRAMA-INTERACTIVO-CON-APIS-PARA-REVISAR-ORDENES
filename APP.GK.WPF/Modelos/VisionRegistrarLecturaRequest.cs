using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APP.GK.WPF.Modelos
{
    public class VisionRegistrarLecturaRequest
    {
        public Guid VisionOrdenResumenId { get; set; }
        public string CodigoLeido { get; set; }
        public string? RawMensaje { get; set; }
        public string? Observaciones { get; set; }
        public string TipoEtiqueta { get; set; }
    }
}
