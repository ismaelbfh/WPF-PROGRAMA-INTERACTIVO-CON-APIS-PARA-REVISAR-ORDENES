using APP.GK.WPF.Modelos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace APP.GK.WPF.Servicios
{
    /// <summary>
    /// Coordinador de pestañas/sesiones de visión para la UI.
    ///
    /// Responsabilidades:
    /// - decidir qué pestaña seleccionar tras una carga/refresco
    /// - reemplazar o añadir un resumen en la colección visual
    /// - localizar un resumen por Id dentro de la colección actual
    ///
    /// NO llama a la API.
    /// NO toca socket.
    /// NO toca la UI directamente.
    /// Solo trabaja con colecciones/modelos ya cargados.
    /// </summary>
    public class VisionTabsCoordinator
    {
        /// <summary>
        /// Decide qué resumen debe quedar seleccionado tras un refresco.
        /// Prioridad:
        /// 1. Id explícito a conservar
        /// 2. resumen actualmente seleccionado si sigue existiendo
        /// 3. el que esté activo
        /// 4. el primero
        /// </summary>
        public VisionOrdenResumen ResolveResumenToSelect(
            IEnumerable<VisionOrdenResumen> pResumenes,
            VisionOrdenResumen pCurrentSelectedResumen,
            Guid? pResumenIdToKeepSelected = null)
        {
            var lResumenes = pResumenes?.ToList() ?? new List<VisionOrdenResumen>();

            if (!lResumenes.Any())
                return null;

            if (pResumenIdToKeepSelected.HasValue)
            {
                var lById = lResumenes.FirstOrDefault(x => x.Id == pResumenIdToKeepSelected.Value);
                if (lById != null)
                    return lById;
            }

            if (pCurrentSelectedResumen != null)
            {
                var lCurrent = lResumenes.FirstOrDefault(x => x.Id == pCurrentSelectedResumen.Id);
                if (lCurrent != null)
                    return lCurrent;
            }

            return lResumenes.FirstOrDefault(x => x.Activa)
                   ?? lResumenes.FirstOrDefault();
        }

        /// <summary>
        /// Sustituye un resumen en colección si ya existe por Id,
        /// o lo añade si todavía no está.
        /// </summary>
        public void ReplaceOrAddResumen(
            ObservableCollection<VisionOrdenResumen> pResumenes,
            VisionOrdenResumen pResumen)
        {
            if (pResumenes == null || pResumen == null)
                return;

            var lExistente = pResumenes.FirstOrDefault(x => x.Id == pResumen.Id);

            if (lExistente != null)
            {
                int lIndex = pResumenes.IndexOf(lExistente);
                pResumenes[lIndex] = pResumen;
            }
            else
            {
                pResumenes.Add(pResumen);
            }
        }

        /// <summary>
        /// Devuelve el resumen de la colección por Id.
        /// </summary>
        public VisionOrdenResumen FindResumenById(
            IEnumerable<VisionOrdenResumen> pResumenes,
            Guid pResumenId)
        {
            return pResumenes?.FirstOrDefault(x => x.Id == pResumenId);
        }
    }
}