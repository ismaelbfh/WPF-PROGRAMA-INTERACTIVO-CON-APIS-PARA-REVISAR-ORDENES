using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using APP.GK.WPF.Modelos;

namespace APP.GK.WPF.Helpers
{
    public class TokenRefreshHandler : DelegatingHandler
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly Func<Task<string>> _getRefreshToken;

        private readonly Func<Task<TokenResponse>> _refreshTokenAsync;
        private int _retryCount = 0; // Contador para limitar los intentos de reintentos

        public TokenRefreshHandler(
            Func<Task<string>> getRefreshToken,
            Func<Task<TokenResponse>> refreshTokenAsync)
        {
            _getRefreshToken = getRefreshToken;
            _refreshTokenAsync = refreshTokenAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {

            // Primera ejecución
            var lResponse = await base.SendAsync(request, cancellationToken);

            // Si no es 401, retornar normal
            if (lResponse.StatusCode != HttpStatusCode.Unauthorized)
                return lResponse;

            // Bloquear para evitar múltiples refrescos concurrentes
            await _semaphore.WaitAsync();
            try
            {
                // Evitar reintentos infinitos en caso de fallo
                if (_retryCount >= 3) // Límite de intentos
                {
                    throw new InvalidOperationException("El token no pudo ser refrescado después de varios intentos.");
                }

                _retryCount++;

                // Intentar refrescar el token
                string? lRefreshToken = await _getRefreshToken();
                if (string.IsNullOrEmpty(lRefreshToken)) return lResponse;

                TokenResponse? lNewTokens = await _refreshTokenAsync();
                if (lNewTokens == null) return lResponse;

                // Actualizar headers con nuevo token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", lNewTokens.AccessToken);

                // Reintentar petición original
                lResponse = await base.SendAsync(request, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }

            return lResponse;
        }
    }
}
