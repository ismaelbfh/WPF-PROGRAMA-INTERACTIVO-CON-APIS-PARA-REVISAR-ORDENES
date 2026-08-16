using System;
using System.Configuration;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace APP.GK.WPF.Servicios
{
    /// <summary>
    /// Servicio de escucha TCP de la cámara.
    ///
    /// RESPONSABILIDAD:
    /// - abrir un socket TCP contra la IP/puerto indicados
    /// - leer mensajes del lector/cámara
    /// - trocearlos por líneas
    /// - publicar lecturas limpias por eventos
    ///
    /// IMPORTANTE:
    /// - No toca UI
    /// - No llama a backend
    /// - No decide negocio
    /// </summary>
    public class CameraReadListenerService
    {
        private TcpClient _client;
        private CancellationTokenSource _cts;
        private Task _listenTask;
        private bool _isListening;

        // Endpoint actualmente en uso, útil para mensajes de error.
        private string _currentIpAddress;
        private string _currentPort;

        public bool IsListening => _isListening;

        public event Action<string> OnLecturaRecibida;
        public event Action<string> OnEstadoInfo;
        public event Action<string> OnCommunicationError;

        /// <summary>
        /// Overload por compatibilidad. Usa la cámara principal configurada.
        /// </summary>
        public async Task<bool> StartListeningAsync()
        {
            string lIp = ConfigurationManager.AppSettings["IpCamera"];
            string lPort = ConfigurationManager.AppSettings["PuertoIpCamera"];

            return await StartListeningAsync(lIp, lPort);
        }

        /// <summary>
        /// Arranca la escucha TCP usando la IP/puerto indicados.
        /// </summary>
        public async Task<bool> StartListeningAsync(string pIpAddress, string pPort)
        {
            try
            {
                if (_isListening)
                    return true;

                _currentIpAddress = pIpAddress;
                _currentPort = pPort;

                if (string.IsNullOrWhiteSpace(_currentIpAddress) || string.IsNullOrWhiteSpace(_currentPort))
                {
                    OnCommunicationError?.Invoke("No se puede escuchar: faltan IP o puerto en la configuración del endpoint.");
                    return false;
                }

                _cts = new CancellationTokenSource();
                _client = new TcpClient();

                await _client.ConnectAsync(_currentIpAddress, int.Parse(_currentPort));

                _isListening = true;
                OnEstadoInfo?.Invoke("Escuchando lecturas...");

                _listenTask = Task.Run(async () => await ListenLoopAsync(_cts.Token));

                return true;
            }
            catch (SocketException ex)
            {
                _isListening = false;
                OnCommunicationError?.Invoke(GetSocketErrorMessage(ex));
                return false;
            }
            catch (FormatException)
            {
                _isListening = false;
                OnCommunicationError?.Invoke("No se puede escuchar: el puerto configurado no es válido.");
                return false;
            }
            catch (Exception ex)
            {
                _isListening = false;
                OnCommunicationError?.Invoke($"No se puede escuchar por un error de comunicación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detiene la escucha actual.
        /// </summary>
        public async Task StopListeningAsync()
        {
            try
            {
                if (!_isListening)
                    return;

                _cts?.Cancel();

                try
                {
                    _client?.Close();
                }
                catch
                {
                    // Silencioso a propósito
                }

                if (_listenTask != null)
                {
                    await Task.WhenAny(_listenTask, Task.Delay(1000));
                }
            }
            catch
            {
                // Silencioso a propósito
            }
            finally
            {
                _isListening = false;
                _listenTask = null;
                _cts?.Dispose();
                _cts = null;
                _client = null;
            }
        }

        /// <summary>
        /// Bucle continuo de lectura TCP.
        /// </summary>
        private async Task ListenLoopAsync(CancellationToken pCancellationToken)
        {
            try
            {
                using (NetworkStream lStream = _client.GetStream())
                {
                    byte[] lBuffer = new byte[4096];

                    while (!pCancellationToken.IsCancellationRequested)
                    {
                        int lBytesRead = await lStream.ReadAsync(lBuffer, 0, lBuffer.Length, pCancellationToken);

                        if (lBytesRead == 0)
                        {
                            break;
                        }

                        string lRawText = Encoding.UTF8.GetString(lBuffer, 0, lBytesRead);

                        if (string.IsNullOrWhiteSpace(lRawText))
                            continue;

                        string[] lFragments = lRawText
                            .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string lFragment in lFragments)
                        {
                            string lLectura = lFragment?.Trim();

                            if (!string.IsNullOrWhiteSpace(lLectura))
                            {
                                OnLecturaRecibida?.Invoke(lLectura);
                            }
                        }
                    }
                }

                if (!pCancellationToken.IsCancellationRequested)
                {
                    _isListening = false;
                    OnCommunicationError?.Invoke("Se ha perdido la comunicación con los lectores.");
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelación controlada
            }
            catch (SocketException ex)
            {
                _isListening = false;
                OnCommunicationError?.Invoke(GetSocketErrorMessage(ex));
            }
            catch (Exception ex)
            {
                _isListening = false;
                OnCommunicationError?.Invoke($"Se ha perdido la comunicación con los lectores: {ex.Message}");
            }
        }

        private string GetSocketErrorMessage(SocketException ex)
        {
            return ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => $"No se puede escuchar en {_currentIpAddress}:{_currentPort}: la conexión fue rechazada por la cámara o lector.",
                SocketError.TimedOut => $"No se puede escuchar en {_currentIpAddress}:{_currentPort}: tiempo de espera agotado al conectar con la cámara o lector.",
                SocketError.HostUnreachable => $"No se puede escuchar en {_currentIpAddress}:{_currentPort}: el host no es accesible en red.",
                SocketError.NetworkUnreachable => $"No se puede escuchar en {_currentIpAddress}:{_currentPort}: la red no está accesible.",
                SocketError.ConnectionReset => $"Se ha perdido la comunicación en {_currentIpAddress}:{_currentPort}: la conexión fue reiniciada por el dispositivo remoto.",
                SocketError.AddressNotAvailable => $"No se puede escuchar: la dirección IP configurada '{_currentIpAddress}' no está disponible.",
                _ => $"No se puede escuchar por un error de socket en {_currentIpAddress}:{_currentPort}: {ex.Message}"
            };
        }
    }
}