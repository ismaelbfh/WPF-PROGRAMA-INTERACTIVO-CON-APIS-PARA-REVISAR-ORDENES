using APP.GK.WPF.Modelos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APP.GK.WPF.Servicios
{
    /// <summary>
    /// Orquestador de sesiones/registros de visión.
    ///
    /// Ahora el resumen se reutiliza por Orden + Cámara.
    /// El tipo real de la operación se audita a nivel de lectura.
    /// </summary>
    public class VisionSessionCoordinator
    {
        private readonly ApiService _apiService;

        public VisionSessionCoordinator(ApiService pApiService)
        {
            _apiService = pApiService;
        }

        public async Task<List<VisionOrdenResumen>> LoadResumenesByOrdenAsync(string pOrdenFabricacion)
        {
            var lResumenes = await _apiService.GetVisionResumenesByOrdenAsync(pOrdenFabricacion);

            return lResumenes?
                .OrderByDescending(x => x.FechaHoraInicio)
                .ToList()
                ?? new List<VisionOrdenResumen>();
        }

        public VisionOrdenResumen FindExistingSessionByOrderAndCamera(
            IEnumerable<VisionOrdenResumen> pResumenes,
            string pOrdenFabricacion,
            string pIpCamara)
        {
            if (pResumenes == null)
                return null;

            return pResumenes.FirstOrDefault(x =>
                x.OrdenFabricacion == pOrdenFabricacion &&
                x.IpCamara == pIpCamara);
        }

        public async Task<VisionOrdenResumen?> StartOrReuseSessionAsync(
            OrdenDetalleProduccion pOrder,
            string pTipoEtiquetaBackend,
            string pPosicionQr,
            string pCodigoEsperado,
            string pIpCamara)
        {
            VisionIniciarOrdenRequest lRequest = new VisionIniciarOrdenRequest
            {
                OrdenFabricacion = pOrder.OPFabricacion,
                CodigoProducto = pOrder.CodigoProducto,
                DescripcionProducto = pOrder.Descripcion,
                FechaProduccion = pOrder.Fecha,
                Linea = pOrder.Linea,
                TipoEtiqueta = pTipoEtiquetaBackend,
                PosicionQr = pPosicionQr,
                CodigoEsperado = pCodigoEsperado,
                IpCamara = pIpCamara
            };

            return await _apiService.IniciarVisionOrdenAsync(lRequest);
        }

        public async Task<VisionOrdenResumen?> FinalizeSessionAsync(Guid pResumenId)
        {
            return await _apiService.FinalizarVisionOrdenAsync(pResumenId);
        }

        public async Task<VisionLectura?> RegisterLecturaAsync(Guid pResumenId, string pLecturaRaw, string pTipoEtiqueta)
        {
            VisionRegistrarLecturaRequest lRequest = new VisionRegistrarLecturaRequest
            {
                VisionOrdenResumenId = pResumenId,
                CodigoLeido = pLecturaRaw?.Trim(),
                RawMensaje = pLecturaRaw,
                Observaciones = null,
                TipoEtiqueta = pTipoEtiqueta
            };

            return await _apiService.RegistrarVisionLecturaAsync(lRequest);
        }
    }
}