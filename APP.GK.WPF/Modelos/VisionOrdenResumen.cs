using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APP.GK.WPF.Modelos
{
    public class VisionOrdenResumen
    {
        public Guid Id { get; set; }
        public string OrdenFabricacion { get; set; }
        public string CodigoProducto { get; set; }
        public string DescripcionProducto { get; set; }
        public DateTime? FechaProduccion { get; set; }
        public string? Linea { get; set; }
        public string TipoEtiqueta { get; set; }
        public string? PosicionQr { get; set; }
        public string CodigoEsperado { get; set; }
        public DateTime FechaHoraInicio { get; set; }
        public DateTime? FechaHoraUltimaLectura { get; set; }
        public int TotalLecturas { get; set; }
        public int TotalOK { get; set; }
        public int TotalNOK { get; set; }
        public bool Activa { get; set; }
        public string? IpCamara { get; set; }
    }
}
