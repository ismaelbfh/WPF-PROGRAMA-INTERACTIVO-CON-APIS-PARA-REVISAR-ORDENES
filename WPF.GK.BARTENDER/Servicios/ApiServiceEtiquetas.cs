using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WPF.GK.BARTENDER.CustomComponents;
using WPF.GK.BARTENDER.Helpers;
using WPF.GK.BARTENDER.Modelos;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace WPF.GK.BARTENDER.Servicios
{
    public class ApiServiceEtiquetas
    {
        private HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly string _connectionStringAPI = ConfigurationManager.AppSettings["ConectionStringAPI_Etiquetas"];
        private ApiBartenderClient _apiClientEtiquetas;
        public ApiServiceEtiquetas(AuthService authService)
        {
            _authService = authService;
            ConfigureHttpClient();
            _authService.TokensRefreshed += UpdateTokenHeader;
            
        }

        private void ConfigureHttpClient()
        {
            TokenRefreshHandler handler = new TokenRefreshHandler(
                getRefreshToken: () => Task.FromResult(_authService.RefreshToken),
                refreshTokenAsync: async () => await _authService.RefreshTokenAsync())
            {
                InnerHandler = new HttpClientHandler()
            };

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_connectionStringAPI)
            };

            UpdateTokenHeader();
            _apiClientEtiquetas = new ApiBartenderClient(
                            _httpClient.BaseAddress.ToString().TrimEnd('/'),
                            _httpClient
                        );
        }

        private void UpdateTokenHeader()
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        }

        public async Task<Etiqueta> GetEtiquetaByCodigoProductoAsync(string codigoProducto)
        {
            try
            {
                // Llamada tipada generada por NSwag; devuelve un EtiquetaDTO
                return await _apiClientEtiquetas.GetEtiquetaPorCodigoProductoAsync(codigoProducto);
            }
            catch (ApiException ex)
            {
                // ApiException.Response suele contener el JSON de error
                var errorBody = ex.Response;
                try
                {
                    // Intentamos extraer "message" si viene en el JSON de error
                    var errorObj = JsonConvert.DeserializeObject<dynamic>(errorBody);
                    string errorMessage = errorObj?.message ?? "Error desconocido en la API";
                    CustomMessageBox.Show(
                        $"Error obteniendo etiqueta: {errorMessage}",
                        "ERROR",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                catch
                {
                    // Si no era un JSON válido, mostramos el texto completo
                    CustomMessageBox.Show(
                        $"Error obteniendo etiqueta:\n{errorBody}",
                        "ERROR",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }

                return null;
            }
            catch (JsonSerializationException ex)
            {
                CustomMessageBox.Show($"Error deserializando etiqueta:\n{ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error inesperado:\n{ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public async Task<PrintLabelResultDTO> PrintLabelAsync(Dictionary<string, string> datos)
        {
            try
            {
                var response = await _apiClientEtiquetas.PrintLabelAsync(datos);

                string textoPrincipal = "";
                string detallesBartender = "";

                if (response?.BartenderResponse?.Messages != null && response.BartenderResponse.Messages.Count > 0)
                {
                    // Buscamos el level mas alto (normalmente solo devolverá uno pero por si acaso)
                    var mensaje = response.BartenderResponse.Messages.OrderByDescending(m => m.Level).First();

                    if (mensaje.Level == 3)
                    {
                        textoPrincipal = "Impresión enviada con advertencias.";
                        detallesBartender = mensaje.Text?.Trim();
                    }
                    else if (mensaje.Level < 3)
                    {
                        textoPrincipal = "Impresión realizada con éxito.";
                        detallesBartender = mensaje.Text?.Trim();
                    }
                }
                else
                {
                    // Si no hay mensajes, usa el message principal (puede venir de la API)
                    textoPrincipal = !string.IsNullOrWhiteSpace(response?.Message) ? response.Message : "Impresión realizada.";
                }

                string mensajeMostrar = textoPrincipal;
                if (!string.IsNullOrWhiteSpace(detallesBartender))
                {
                    mensajeMostrar += "\n\n" + detallesBartender;
                }

                CustomMessageBox.Show(
                    mensajeMostrar,
                    "Impresion correcta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                return response;
            }
            catch (ApiException ex)
            {
                string mensajePretty = "";
                try
                {
                    var token = JToken.Parse(ex.Response);

                    var messages = token["bartenderResponse"]?["messages"] as JArray;
                    if (messages != null && messages.Count > 0)
                    {
                        var text = messages[0]?["text"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                            mensajePretty = text.Trim();
                    }
                    else
                    {
                        // Si no hay mensajes, intenta mostrar el message principal
                        mensajePretty = token["message"]?.ToString() ?? "";
                    }
                }
                catch
                {
                    mensajePretty = ex.Response;
                }

                if (string.IsNullOrEmpty(mensajePretty))
                    mensajePretty = "Ha ocurrido un error inesperado en la impresión.";

                mensajePretty += "\n\nPor favor contacte con el administrador de etiquetas para su resolución.";

                CustomMessageBox.Show(
                    mensajePretty,
                    "Error al enviar impresión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                PrintLabelResultDTO errorResult = null;
                try
                {
                    errorResult = JsonConvert.DeserializeObject<PrintLabelResultDTO>(ex.Response);
                }
                catch
                {
                    errorResult = new PrintLabelResultDTO
                    {
                        Message = mensajePretty,
                        BartenderResponse = null
                    };
                }

                return errorResult;
            }
        }

        public async Task<bool> PostHistoricoImpresionAsync(HistoricoImpresionDTO historicoImpresionDTO)
        {
            try
            {
                await _apiClientEtiquetas.PostHistoricoImpresionAsync(historicoImpresionDTO);
                return true;
            }
            catch (ApiException ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Llama al endpoint GET /version para obtener la versión del MSI.
        /// Se espera un JSON con el formato { "version": "1.0.0.2" }.
        /// </summary>
        public async Task<Version> GetServerVersionAsync()
        {
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
    }
}
