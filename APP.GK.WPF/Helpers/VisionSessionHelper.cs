using APP.GK.WPF.Modelos;
using System;

namespace APP.GK.WPF.Helpers
{
    /// <summary>
    /// Helper de apoyo para trabajar con sesiones de visión
    /// sin meter esa lógica repetida dentro del ViewModel.
    /// 
    /// OJO:
    /// - No cambia comportamiento.
    /// - Solo centraliza comparaciones y nombres.
    /// </summary>
    public static class VisionSessionHelper
    {
        public static bool IsQrSuperior(VisionOrdenResumen pResumen)
        {
            if (pResumen == null)
                return false;

            return string.Equals(pResumen.TipoEtiqueta, "QR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(pResumen.PosicionQr, "Superior", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsQrInferior(VisionOrdenResumen pResumen)
        {
            if (pResumen == null)
                return false;

            return string.Equals(pResumen.TipoEtiqueta, "QR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(pResumen.PosicionQr, "Inferior", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEan(VisionOrdenResumen pResumen)
        {
            if (pResumen == null)
                return false;

            return string.Equals(pResumen.TipoEtiqueta, "EAN", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetTabDisplayName(VisionOrdenResumen pResumen)
        {
            if (pResumen == null)
                return string.Empty;

            if (IsQrSuperior(pResumen))
                return "QR Superior";

            if (IsQrInferior(pResumen))
                return "QR Inferior";

            if (IsEan(pResumen))
                return "EAN";

            return pResumen.TipoEtiqueta ?? string.Empty;
        }

        /// <summary>
        /// Comprueba si un resumen de visión coincide con la combinación de negocio:
        /// Orden + Tipo + Posición.
        /// </summary>
        public static bool MatchesSession(
            VisionOrdenResumen pResumen,
            string pOrdenFabricacion,
            string pTipoEtiqueta,
            string pPosicionQr)
        {
            if (pResumen == null)
                return false;

            return string.Equals(pResumen.OrdenFabricacion, pOrdenFabricacion, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pResumen.TipoEtiqueta ?? string.Empty, pTipoEtiqueta ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pResumen.PosicionQr ?? string.Empty, pPosicionQr ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}