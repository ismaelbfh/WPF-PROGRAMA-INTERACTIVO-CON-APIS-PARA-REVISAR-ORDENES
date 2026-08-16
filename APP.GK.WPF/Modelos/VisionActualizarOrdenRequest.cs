using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APP.GK.WPF.Modelos
{
    public class VisionActualizarOrdenRequest
    {
        public Guid VisionOrdenResumenId { get; set; }
        public string CodigoEsperado { get; set; }
    }
}
