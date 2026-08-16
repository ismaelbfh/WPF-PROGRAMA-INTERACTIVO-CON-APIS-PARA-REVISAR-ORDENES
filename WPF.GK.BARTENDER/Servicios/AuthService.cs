using System;
using System.Configuration;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WPF.GK.BARTENDER.CustomComponents;
using WPF.GK.BARTENDER.Modelos;

namespace WPF.GK.BARTENDER.Servicios
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);
        private Timer _tokenCheckTimer;
        private readonly int _tokenCheckInterval = 1800000; // 30 minutos

        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public DateTime AccessTokenExpiration { get; private set; }

        public event Action TokensRefreshed; // Suscríbete desde ApiService y ApiServiceEtiquetas

        public AuthService()
        {
            var apiAuthUrl = ConfigurationManager.AppSettings["ApiAuthUrl"];
            _httpClient = new HttpClient { BaseAddress = new Uri(apiAuthUrl) };
        }

        public async Task<bool> AuthenticateAsync()
        {
            try
            {
                var username = ConfigurationManager.AppSettings["UsuarioBartender"];
                var password = ConfigurationManager.AppSettings["PasswordUsuarioBartender"];
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    CustomMessageBox.Show("Usuario o password de API Auth no configurado.", "ERROR", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                var lAuthRequest = new AuthRequest { username = username, password = password };
                var lResponse = await _httpClient.PostAsJsonAsync("/api/Auth/login", lAuthRequest);
                var lJson = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<dynamic>(lJson);
                    string msg = error?.message ?? "Error desconocido";
                    CustomMessageBox.Show($"Login fallido en API Auth: {msg}", "ERROR", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return false;
                }

                var lTokens = JsonConvert.DeserializeObject<TokenResponse>(lJson);
                AccessToken = lTokens.AccessToken;
                RefreshToken = lTokens.RefreshToken;
                AccessTokenExpiration = lTokens.AccessTokenExpiration;

                // Inicia el timer para refrescar
                _tokenCheckTimer = new Timer(async _ => await CheckTokenExpirationAsync(), null, _tokenCheckInterval, _tokenCheckInterval);

                TokensRefreshed?.Invoke(); // Notifica a los servicios que hay nuevo token
                return true;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error autenticando usuario en API Auth: {ex.Message}", "ERROR", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private async Task CheckTokenExpirationAsync()
        {
            if (DateTime.UtcNow >= AccessTokenExpiration.AddMinutes(-1))
            {
                await RefreshTokenAsync();
            }
        }

        public async Task<TokenResponse> RefreshTokenAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                var lResponse = await _httpClient.PostAsJsonAsync("/api/Auth/refresh", new { RefreshToken = RefreshToken });
                var lJson = await lResponse.Content.ReadAsStringAsync();

                if (!lResponse.IsSuccessStatusCode)
                {
                    var error = JsonConvert.DeserializeObject<dynamic>(lJson);
                    string msg = error?.message ?? "Error desconocido";
                    CustomMessageBox.Show($"Error refrescando token en AuthService: {msg}", "ERROR", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return null;
                }

                var lTokens = JsonConvert.DeserializeObject<TokenResponse>(lJson);
                AccessToken = lTokens.AccessToken;
                RefreshToken = lTokens.RefreshToken;
                AccessTokenExpiration = lTokens.AccessTokenExpiration;

                TokensRefreshed?.Invoke(); // Notifica a los servicios que hay nuevo token
                return lTokens;
            }
            catch
            {
                CustomMessageBox.Show("Sesión expirada. Inicie sesión de nuevo.", "ERROR", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return null;
            }
            finally
            {
                _refreshLock.Release();
            }
        }
    }
}
