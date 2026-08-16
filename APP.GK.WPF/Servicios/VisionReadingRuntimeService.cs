using APP.GK.WPF.Modelos;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace APP.GK.WPF.Servicios
{
    /// <summary>
    /// Runtime de lectura de visión.
    ///
    /// RESPONSABILIDAD:
    /// - arrancar/parar modo mock
    /// - arrancar/parar modo real apoyándose en CameraReadListenerService
    /// - exponer eventos unificados a la WPF
    ///
    /// IMPORTANTE:
    /// - el mock se mantiene exactamente igual
    /// - el endpoint solo afecta al modo real
    /// </summary>
    public class VisionReadingRuntimeService
    {
        private readonly CameraReadListenerService _cameraReadListenerService;
        private readonly bool _useMockForTesting;

        private CancellationTokenSource _mockReadingCts;
        private Task _mockReadingTask;
        private bool _isRunning;

        public bool IsRunning => _isRunning;

        public event Action<string> OnLecturaRecibida;
        public event Action<string> OnEstadoInfo;
        public event Action<string> OnCommunicationError;

        public VisionReadingRuntimeService(
            CameraReadListenerService pCameraReadListenerService,
            bool pUseMockForTesting)
        {
            _cameraReadListenerService = pCameraReadListenerService;
            _useMockForTesting = pUseMockForTesting;

            _cameraReadListenerService.OnLecturaRecibida += (pLectura) =>
            {
                OnLecturaRecibida?.Invoke(pLectura);
            };

            _cameraReadListenerService.OnEstadoInfo += (pEstado) =>
            {
                OnEstadoInfo?.Invoke(pEstado);
            };

            _cameraReadListenerService.OnCommunicationError += (pError) =>
            {
                _isRunning = false;
                OnCommunicationError?.Invoke(pError);
            };
        }

        /// <summary>
        /// Overload por compatibilidad. Si no se pasa endpoint, se usa el comportamiento antiguo.
        /// </summary>
        public async Task<bool> StartAsync(string pCodigoEsperado)
        {
            return await StartAsync(pCodigoEsperado, null);
        }

        /// <summary>
        /// Arranca la lectura:
        /// - en mock: genera lecturas fake
        /// - en real: abre socket contra el endpoint indicado
        /// </summary>
        public async Task<bool> StartAsync(string pCodigoEsperado, CameraEndpoint pEndpoint)
        {
            if (_useMockForTesting)
            {
                await StopAsync();

                _mockReadingCts = new CancellationTokenSource();
                CancellationToken lToken = _mockReadingCts.Token;

                _isRunning = true;
                OnEstadoInfo?.Invoke("Escuchando lecturas...");

                _mockReadingTask = Task.Run(async () =>
                {
                    int lCounter = 0;

                    while (!lToken.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(5000, lToken);

                            if (lToken.IsCancellationRequested)
                                break;

                            lCounter++;

                            string lMockCodigoLeido = lCounter % 3 == 0
                                ? $"{pCodigoEsperado}_NOK"
                                : pCodigoEsperado;

                            OnLecturaRecibida?.Invoke(lMockCodigoLeido);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _isRunning = false;
                            OnCommunicationError?.Invoke($"Error en modo mock de lectura: {ex.Message}");
                            break;
                        }
                    }
                }, lToken);

                return true;
            }

            bool lStarted;

            if (pEndpoint == null)
            {
                lStarted = await _cameraReadListenerService.StartListeningAsync();
            }
            else
            {
                lStarted = await _cameraReadListenerService.StartListeningAsync(pEndpoint.Ip, pEndpoint.Port);
            }

            _isRunning = lStarted;
            return lStarted;
        }

        /// <summary>
        /// Para la lectura mock o real.
        /// </summary>
        public async Task StopAsync()
        {
            if (_useMockForTesting)
            {
                try
                {
                    _mockReadingCts?.Cancel();

                    if (_mockReadingTask != null)
                    {
                        await Task.WhenAny(_mockReadingTask, Task.Delay(1000));
                    }
                }
                catch
                {
                    // Silencioso a propósito
                }
                finally
                {
                    _mockReadingCts?.Dispose();
                    _mockReadingCts = null;
                    _mockReadingTask = null;
                    _isRunning = false;
                }

                return;
            }

            await _cameraReadListenerService.StopListeningAsync();
            _isRunning = false;
        }
    }
}