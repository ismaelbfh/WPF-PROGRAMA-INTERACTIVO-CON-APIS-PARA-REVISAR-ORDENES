using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WPF.GK.BARTENDER.Modelos
{
    public class OrdenPrevision
    {
        public string NUMEROPREVISION { get; set; }
        public string OP { get; set; }
        public string MAQUINA { get; set; }
        public DateTime? FECHA { get; set; }
        public string PF { get; set; }
        public string DESCRIPCION { get; set; }
        public DateTime? FECHACADUCIDAD { get; set; }
        public string LOTE { get; set; }
        public int? CAJAS { get; set; }
        public decimal? KILOS { get; set; }
        public string EAN { get; set; }
        public string DUN { get; set; }
        public decimal? UNIDADESCAJA { get; set; }
        public decimal? PESONETO { get; set; }
    }
}
