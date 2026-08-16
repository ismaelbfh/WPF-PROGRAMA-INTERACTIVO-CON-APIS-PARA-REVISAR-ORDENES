using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using APP.GK.WPF.CustomComponents;
using APP.GK.WPF.Helpers;
using APP.GK.WPF.Modelos;
using Newtonsoft.Json;

namespace APP.GK.WPF.Servicios
{
    public class ApiService
    {
        private HttpClient _httpClient;
        private string _accessToken;
        private string _refreshToken;
        private DateTime _accessTokenExpiration;
        private readonly string _connectionStringAPI = ConfigurationManager.AppSettings["ConectionStringAPI"];
        private readonly string _ipCameraPrincipal = ConfigurationManager.AppSettings["IpCamera"];
        private readonly string _portCameraPrincipal = ConfigurationManager.AppSettings["PuertoIpCamera"];
        private readonly string _ipCamera2 = ConfigurationManager.AppSettings["IpCamera2"];
        private readonly string _portCamera2 = ConfigurationManager.AppSettings["PuertoIpCamera2"];
        string _linea = ConfigurationManager.AppSettings["Linea"];
        string _tipoDestino = ConfigurationManager.AppSettings["TipoDestino"];
        private readonly string _apiAuthUrl = ConfigurationManager.AppSettings["ApiAuthUrl"];
        private Timer _tokenCheckTimer;
        private readonly int _tokenCheckInterval = 1800000; // Cada 30 minutos
        private SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public ApiService()
        {
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            if (!string.IsNullOrEmpty(_connectionStringAPI))
            {
                try
                {
                    TokenRefreshHandler lHandler = new TokenRefreshHandler(
                        getRefreshToken: () => Task.FromResult(_refreshToken),
                        refreshTokenAsync: async () => await RefreshTokenAsync())
                    {
                        InnerHandler = new HttpClientHandler()
                    };

                    _httpClient = new HttpClient(lHandler)
                    {
                        BaseAddress = new Uri(_connectionStringAPI)
                    };
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Fallo al conectar a '{_connectionStringAPI}': {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                CustomMessageBox.Show($"No se ha configurado la ConnectionStringAPI.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                AuthRequest lAuthRequest = new AuthRequest
                {
                    username = ConfigurationManager.AppSettings["UsuarioCamera"],
                    password = ConfigurationManager.AppSettings["PasswordUsuarioCamera"]
                };
                if (string.IsNullOrEmpty(lAuthRequest.username))
                {
                    CustomMessageBox.Show($"Error, no ha establecido un usuario para la aplicacion.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                if (string.IsNullOrEmpty(lAuthRequest.password))
                {
                    CustomMessageBox.Show($"Error, no ha establecido una password para el usuario de la aplicacion.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // --- LOGIN SIEMPRE A API AUTH ---
                using (var authClient = new HttpClient() { BaseAddress = new Uri(_apiAuthUrl) })
                {
                    var lResponseLogin = await authClient.PostAsJsonAsync("/api/Auth/login", lAuthRequest);
                    string json = await lResponseLogin.Content.ReadAsStringAsync();

                    if (!lResponseLogin.IsSuccessStatusCode)
                    {
                        var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(json);
                        string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";
                        CustomMessageBox.Show($"Error al autenticar el usuario de la cámara: {lErrorMessage}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    TokenResponse lTokens = await lResponseLogin.Content.ReadFromJsonAsync<TokenResponse>();
                    _accessToken = lTokens.AccessToken;
                    _refreshToken = lTokens.RefreshToken;
                    _accessTokenExpiration = lTokens.AccessTokenExpiration;
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", _accessToken);

                    // Iniciar timer para refrescar token
                    _tokenCheckTimer = new Timer(
                        async _ => await CheckTokenExpirationAsync(),
                        null,
                        _tokenCheckInterval,
                        _tokenCheckInterval
                    );

                    return true;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error en AuthenticateAsync: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task CheckTokenExpirationAsync()
        {
            if (DateTime.UtcNow >= _accessTokenExpiration.AddMinutes(-1))
            {
                try
                {
                    TokenResponse? lNewTokens = await RefreshTokenAsync();
                    if (lNewTokens != null)
                    {
                        _accessToken = lNewTokens.AccessToken;
                        _refreshToken = lNewTokens.RefreshToken;
                        _accessTokenExpiration = lNewTokens.AccessTokenExpiration;
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", _accessToken);
                    }
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Error al refrescar el token: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- ADAPTADO: Refresh SIEMPRE contra API AUTH ---
        private async Task<TokenResponse> RefreshTokenAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                using (var authClient = new HttpClient() { BaseAddress = new Uri(_apiAuthUrl) })
                {
                    var lResponseRefresh = await authClient.PostAsJsonAsync("/api/Auth/refresh", new { RefreshToken = _refreshToken });
                    string lJson = await lResponseRefresh.Content.ReadAsStringAsync();

                    if (!lResponseRefresh.IsSuccessStatusCode)
                    {
                        var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJson);
                        string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";
                        CustomMessageBox.Show($"Error de refresco de token en el método del servicio: {lErrorMessage}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }

                    TokenResponse lTokens = await lResponseRefresh.Content.ReadFromJsonAsync<TokenResponse>();
                    _accessToken = lTokens.AccessToken;
                    _refreshToken = lTokens.RefreshToken;
                    _accessTokenExpiration = lTokens.AccessTokenExpiration;

                    return lTokens;
                }
            }
            catch
            {
                CustomMessageBox.Show($"Sesión expirada. Por favor inicie sesión nuevamente.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        // Obtiene las órdenes para la línea configurada en app.config
        public async Task<PaginatedResult<OrdenProduccion>?> GetOrdersAsync(int pPageNumber, int pPageSize, string? filtro = null)
        {
            try
            {
                if (string.IsNullOrEmpty(_linea))
                {
                    CustomMessageBox.Show($"No has configurado la Linea en la configuración.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
                if (string.IsNullOrEmpty(_tipoDestino))
                {
                    CustomMessageBox.Show($"No has configurado un Tipo Destino en la configuración.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var lToday = DateTime.Today.ToString("yyyy-MM-dd");
                string lUrlOrdenes = $"{_connectionStringAPI}/api/OrdenesNavision/getOrdenesProduccion?" +
                            $"pLine={_linea}&pTipoDestino={_tipoDestino}&pStartingDate=2025-07-22" +
                            $"&pPageNumber={pPageNumber}&pPageSize={pPageSize}";
                //2021-09-07
                //2025-08-03
                //2025-07-22
                if (!string.IsNullOrWhiteSpace(filtro))
                    lUrlOrdenes += $"&filtro={Uri.EscapeDataString(filtro)}";

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var lResponseOrdenes = await _httpClient.GetAsync(lUrlOrdenes);
                string lJsonContent = await lResponseOrdenes.Content.ReadAsStringAsync();

                if (!lResponseOrdenes.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";
                    CustomMessageBox.Show($"Error obteniendo órdenes: {lErrorMessage}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                return JsonConvert.DeserializeObject<PaginatedResult<OrdenProduccion>?>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener órdenes: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }


        // Obtiene los detalles de una orden
        public async Task<OrdenDetalleProduccion?> GetOrderDetailAsync(string pOpFabricacion)
        {
            try
            {
                string lUrlOrden = $"/api/OrdenesNavision/getDatosOrdenProduccion?pOPFabricacion={pOpFabricacion}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var lResponseOrden = await _httpClient.GetAsync(lUrlOrden);
                string lJsonContent = await lResponseOrden.Content.ReadAsStringAsync();
                if (!lResponseOrden.IsSuccessStatusCode) // Si el código de estado NO es 2xx
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent); // Deserializa el JSON de error
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API"; // Obtiene el mensaje de error

                    CustomMessageBox.Show($"Error obteniendo detalles de la orden: {lErrorMessage}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
                return JsonConvert.DeserializeObject<OrdenDetalleProduccion?>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener detalle de la orden: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        // Obtiene el código de barras para un producto y un tipo de etiqueta
        public async Task<string> GetBarcodeAsync(string pCodigoProducto, string pTipoEtiqueta)
        {
            try
            {
                string lUrlCodigoBarras = $"/api/OrdenesNavision/getCodigoBarras?pNumProduct={pCodigoProducto}&pTipoCodigoBarras={pTipoEtiqueta}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var lResponseCodigoBarras = await _httpClient.GetAsync(lUrlCodigoBarras);
                string lJsonContent = await lResponseCodigoBarras.Content.ReadAsStringAsync();
                if (!lResponseCodigoBarras.IsSuccessStatusCode) // Si el código de estado NO es 2xx
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent); // Deserializa el JSON de error
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API"; // Obtiene el mensaje de error

                    CustomMessageBox.Show($"Error obteniendo codigo de barras: {lErrorMessage}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return string.Empty;
                }
                return JsonConvert.DeserializeObject<string>(lJsonContent) ?? string.Empty;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener codigo de barras: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }

        public async Task<string> GetQrAsync(string pCodigoProducto, string pQrType) // "QR_Arriba"|"QR_Abajo"
        {
            try
            {
                string url = $"/api/OrdenesNavision/getCodigoQr?pNumProduct={pCodigoProducto}&pQrType={pQrType}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var resp = await _httpClient.GetAsync(url);
                string json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    var err = JsonConvert.DeserializeObject<dynamic>(json);
                    string msg = err?.message ?? "Error desconocido en la API";
                    CustomMessageBox.Show($"Error obteniendo QR: {msg}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return string.Empty;
                }

                return JsonConvert.DeserializeObject<string>(json) ?? string.Empty;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener QR: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return string.Empty;
            }
        }

        /// <summary>
        /// Resuelve a qué cámara hay que trabajar en función del tipo seleccionado.
        /// Reglas finales:
        /// - EAN_Abajo -> cámara de abajo (principal)
        /// - QR_Abajo  -> cámara de abajo (principal)
        /// - EAN_Arriba -> cámara de arriba (camera2)
        /// - QR_Arriba  -> cámara de arriba (camera2)
        /// </summary>
        public CameraEndpoint GetCameraEndpointByTipoSeleccionado(string pTipoEtiquetaSeleccionado)
        {
            if (string.Equals(pTipoEtiquetaSeleccionado, "QR_Arriba", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pTipoEtiquetaSeleccionado, "EAN_Arriba", StringComparison.OrdinalIgnoreCase))
            {
                return new CameraEndpoint
                {
                    Ip = _ipCamera2 ?? string.Empty,
                    Port = !string.IsNullOrWhiteSpace(_portCamera2) ? _portCamera2 : (_portCameraPrincipal ?? string.Empty),
                    Name = "Camara2"
                };
            }

            // Todo lo de abajo va a la cámara principal
            return new CameraEndpoint
            {
                Ip = _ipCameraPrincipal ?? string.Empty,
                Port = _portCameraPrincipal ?? string.Empty,
                Name = "CamaraPrincipal"
            };
        }

        /// <summary>
        /// Mantengo este overload por compatibilidad con código antiguo.
        /// Por defecto usa la cámara principal.
        /// </summary>
        public async Task<bool> SendCommandsToCameraAsync(string pBarcode)
        {
            CameraEndpoint lDefaultEndpoint = GetCameraEndpointByTipoSeleccionado("EAN");
            return await SendCommandsToCameraAsync(pBarcode, lDefaultEndpoint);
        }

        /// <summary>
        /// Envía los comandos a la cámara vía TCP usando el endpoint indicado.
        /// </summary>
        public async Task<bool> SendCommandsToCameraAsync(string pBarcode, CameraEndpoint pEndpoint)
        {
            try
            {
                if (pEndpoint == null)
                {
                    CustomMessageBox.Show("No se ha resuelto el endpoint de cámara.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(pEndpoint.Ip))
                {
                    CustomMessageBox.Show($"No se ha configurado la IP del endpoint '{pEndpoint.Name}'.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(pEndpoint.Port))
                {
                    CustomMessageBox.Show($"No se ha configurado el puerto del endpoint '{pEndpoint.Name}'.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                string[] lCommands;

                if (string.IsNullOrEmpty(pBarcode))
                {
                    lCommands = new string[] { "||>SET DVALID.TYPE 0" };
                }
                else
                {
                    lCommands = new string[]
                    {
                "||>SET DVALID.PROG-TARG 3",
                "||>SET DVALID.TYPE 3",
                $"||>SET DVALID.PATTERN \"{pBarcode}\""
                    };
                }

                using (System.Net.Sockets.TcpClient? lClient = new System.Net.Sockets.TcpClient())
                {
                    await lClient.ConnectAsync(pEndpoint.Ip, int.Parse(pEndpoint.Port));

                    using (var lStream = lClient.GetStream())
                    {
                        foreach (string lCommand in lCommands)
                        {
                            byte[] lData = Encoding.UTF8.GetBytes(lCommand + "\r\n");
                            await lStream.WriteAsync(lData, 0, lData.Length);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error al enviar comandos a la cámara '{pEndpoint?.Name}' (IP: {pEndpoint?.Ip}, Puerto: {pEndpoint?.Port}): {ex.Message}",
                    "ERROR",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// Mantengo este overload por compatibilidad con código antiguo.
        /// Por defecto usa la cámara principal.
        /// </summary>
        public bool PingCameraSuccess()
        {
            CameraEndpoint lDefaultEndpoint = GetCameraEndpointByTipoSeleccionado("EAN");
            return PingCameraSuccess(lDefaultEndpoint);
        }

        /// <summary>
        /// Hace ping al endpoint concreto indicado.
        /// </summary>
        public bool PingCameraSuccess(CameraEndpoint pEndpoint)
        {
            try
            {
                if (pEndpoint == null)
                {
                    CustomMessageBox.Show("No se ha resuelto el endpoint de cámara.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(pEndpoint.Ip))
                {
                    CustomMessageBox.Show($"No se encuentra configurada la IP del endpoint '{pEndpoint.Name}'.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(pEndpoint.Port))
                {
                    CustomMessageBox.Show($"No se encuentra configurado el puerto del endpoint '{pEndpoint.Name}'.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                Ping lPing = new Ping();
                PingReply lReply = lPing.Send(pEndpoint.Ip);

                if (lReply.Status != IPStatus.Success)
                {
                    CustomMessageBox.Show(
                        $"No se ha podido hacer PING a la IP '{pEndpoint.Ip}' del endpoint '{pEndpoint.Name}' porque no está en red.",
                        "ERROR",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return lReply.Status == IPStatus.Success;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(
                    $"Error exception al hacer ping a la IP '{pEndpoint?.Ip}' del endpoint '{pEndpoint?.Name}': {ex.Message}",
                    "ERROR",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return false;
            }
        }

        /// <summary>
        /// Llama al endpoint GET /version para obtener la versión del MSI.
        /// Se espera un JSON con el formato { "version": "1.0.0.2" }.
        /// </summary>
        public async Task<Version> GetServerVersionAsync()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var response = await _httpClient.GetAsync($"api/UpdateInstaller/version");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(json);
            return new Version((string)result.version);
        }

        /// <summary>
        /// Llama al endpoint GET /download para descargar el MSI.
        /// </summary>
        public async Task<byte[]> DownloadUpdateAsync()
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            var response = await _httpClient.GetAsync($"api/UpdateInstaller/download");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Obtiene la versión actual de la aplicación.
        /// </summary>
        public Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }



        public async Task<VisionOrdenResumen?> IniciarVisionOrdenAsync(VisionIniciarOrdenRequest pRequest)
        {
            try
            {
                string lUrl = "/api/Vision/iniciarOrden";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var lResponse = await _httpClient.PostAsJsonAsync(lUrl, pRequest);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    CustomMessageBox.Show($"Error iniciando orden de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                return JsonConvert.DeserializeObject<VisionOrdenResumen>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al iniciar orden de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public async Task<VisionLectura?> RegistrarVisionLecturaAsync(VisionRegistrarLecturaRequest pRequest)
        {
            try
            {
                string lUrl = "/api/Vision/registrarLectura";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var lResponse = await _httpClient.PostAsJsonAsync(lUrl, pRequest);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    CustomMessageBox.Show($"Error registrando lectura de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                return JsonConvert.DeserializeObject<VisionLectura>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al registrar lectura de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public async Task<VisionOrdenResumen?> FinalizarVisionOrdenAsync(Guid pVisionOrdenResumenId)
        {
            try
            {
                string lUrl = "/api/Vision/finalizarOrden";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                VisionFinalizarOrdenRequest lRequest = new VisionFinalizarOrdenRequest
                {
                    VisionOrdenResumenId = pVisionOrdenResumenId
                };

                var lResponse = await _httpClient.PostAsJsonAsync(lUrl, lRequest);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    CustomMessageBox.Show($"Error finalizando orden de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                return JsonConvert.DeserializeObject<VisionOrdenResumen>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al finalizar orden de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public async Task<VisionOrdenResumen?> GetVisionResumenByOrdenAsync(string pOrdenFabricacion)
        {
            try
            {
                string lUrl = $"/api/Vision/getResumenByOrden?ordenFabricacion={pOrdenFabricacion}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var lResponse = await _httpClient.GetAsync(lUrl);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    // Si no existe resumen todavía, no mostramos popup; devolvemos null
                    if (lResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return null;

                    CustomMessageBox.Show($"Error obteniendo resumen de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                return JsonConvert.DeserializeObject<VisionOrdenResumen>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener resumen de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public async Task<List<VisionLectura>> GetVisionLecturasByResumenIdAsync(Guid pVisionOrdenResumenId)
        {
            try
            {
                string lUrl = $"/api/Vision/getLecturasByResumenId?visionOrdenResumenId={pVisionOrdenResumenId}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var lResponse = await _httpClient.GetAsync(lUrl);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    CustomMessageBox.Show($"Error obteniendo lecturas de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return new List<VisionLectura>();
                }

                return JsonConvert.DeserializeObject<List<VisionLectura>>(lJsonContent) ?? new List<VisionLectura>();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener lecturas de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<VisionLectura>();
            }
        }

        public async Task<List<VisionOrdenResumen>> GetVisionResumenesByOrdenAsync(string pOrdenFabricacion)
        {
            try
            {
                string lUrl = $"/api/Vision/getResumenesByOrden?ordenFabricacion={pOrdenFabricacion}";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var lResponse = await _httpClient.GetAsync(lUrl);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    CustomMessageBox.Show($"Error obteniendo los resúmenes de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);

                    return new List<VisionOrdenResumen>();
                }

                return JsonConvert.DeserializeObject<List<VisionOrdenResumen>>(lJsonContent) ?? new List<VisionOrdenResumen>();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener los resúmenes de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);

                return new List<VisionOrdenResumen>();
            }
        }

        public async Task<VisionOrdenResumen?> ActualizarVisionOrdenAsync(VisionActualizarOrdenRequest pRequest)
        {
            try
            {
                string lUrl = "/api/Vision/actualizarOrden";
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                var lResponse = await _httpClient.PostAsJsonAsync(lUrl, pRequest);
                string lJsonContent = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";

                    CustomMessageBox.Show($"Error actualizando orden de visión: {lErrorMessage}",
                        "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);

                    return null;
                }

                return JsonConvert.DeserializeObject<VisionOrdenResumen>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al actualizar orden de visión: {ex.Message}",
                    "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);

                return null;
            }
        }
    }
}
