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
using WPF.GK.BARTENDER.CustomComponents;
using WPF.GK.BARTENDER.Helpers;
using WPF.GK.BARTENDER.Modelos;
using Newtonsoft.Json;

namespace WPF.GK.BARTENDER.Servicios
{
    public class ApiService
    {
        private HttpClient _httpClient;
        private readonly AuthService _authService;
        private readonly string _connectionStringAPI = ConfigurationManager.AppSettings["ConectionStringAPI"];
        string _linea = ConfigurationManager.AppSettings["Linea"];
        string _tipoDestino = ConfigurationManager.AppSettings["TipoDestino"];

        public ApiService(AuthService authService)
        {
            _authService = authService;
            ConfigureHttpClient();

            // Suscribirse para actualizar el token cuando cambie
            _authService.TokensRefreshed += UpdateTokenHeader;
        }

        private void ConfigureHttpClient()
        {
            TokenRefreshHandler lHandler = new TokenRefreshHandler(
                getRefreshToken: () => Task.FromResult(_authService.RefreshToken),
                refreshTokenAsync: async () => await _authService.RefreshTokenAsync())
            {
                InnerHandler = new HttpClientHandler()
            };

            _httpClient = new HttpClient(lHandler)
            {
                BaseAddress = new Uri(_connectionStringAPI)
            };

            UpdateTokenHeader();
        }

        private void UpdateTokenHeader()
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
        }

        // Obtiene las órdenes para la línea configurada en app.config
        public async Task<PaginatedResult<OrdenPrevision>?> GetOrdersAsync(int pPageNumber, int pPageSize, string? filtro = null, DateTime? fecha = null)
        {
            try
            {
                var lFecha = (fecha ?? DateTime.Today).ToString("yyyy-MM-dd");
                string lUrlOrdenes = $"{_connectionStringAPI}/api/OrdenesNavision/getPrevisionesPorFecha?" +
                            $"fecha={lFecha}" +
                            $"&pageNumber={pPageNumber}&pageSize={pPageSize}";

                if (!string.IsNullOrWhiteSpace(filtro))
                    lUrlOrdenes += $"&filtro={Uri.EscapeDataString(filtro)}";

                var lResponseOrdenes = await _httpClient.GetAsync(lUrlOrdenes);
                string lJsonContent = await lResponseOrdenes.Content.ReadAsStringAsync();

                if (!lResponseOrdenes.IsSuccessStatusCode)
                {
                    var lErrorResponse = JsonConvert.DeserializeObject<dynamic>(lJsonContent);
                    string lErrorMessage = lErrorResponse?.message ?? "Error desconocido en la API";
                    CustomMessageBox.Show($"Error obteniendo órdenes: {lErrorMessage}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                return JsonConvert.DeserializeObject<PaginatedResult<OrdenPrevision>?>(lJsonContent);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al obtener órdenes: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
