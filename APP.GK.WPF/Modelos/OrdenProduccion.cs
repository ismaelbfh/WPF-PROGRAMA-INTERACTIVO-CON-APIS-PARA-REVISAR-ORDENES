using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APP.GK.WPF.Modelos
{
    public class OrdenProduccion
    {
        public string NumeroPrevision { get; set; }
        public string OP { get; set; }
        public int Orden { get; set; }
        public string Maquina { get; set; }
        public string PF { get; set; }
        public string Descripcion { get; set; }
        public DateTime Fecha { get; set; }
        public DateTime FechaCaducidad { get; set; }
        public string Lote { get; set; }
        public int Cajas { get; set; }
        public string UM { get; set; }
        public int Sobres { get; set; }
        public int SobresHoraObjetivo { get; set; }
        public double TiempoProduccion { get; set; }
        public double NumeroPersonas { get; set; }
        public string PC { get; set; }
        public string DescripcionEsca { get; set; }
        public decimal CE { get; set; }
    }
}
