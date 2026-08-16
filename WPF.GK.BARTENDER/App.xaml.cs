using System.Configuration;
using System.Data;
using System.IO;
using System.Windows;
using WPF.GK.BARTENDER.CustomComponents;
using WPF.GK.BARTENDER.Servicios;
using WPF.GK.BARTENDER.ViewModels;
using WPF.GK.BARTENDER.Vistas;

namespace WPF.GK.BARTENDER;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private AuthService _authService;
        private ApiService _apiService;
        private ApiServiceEtiquetas _apiServiceEtiquetas;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _authService = new AuthService();
            var loginOk = await _authService.AuthenticateAsync();
            if (!loginOk)
            {
                Shutdown();
                return;
            }

            _apiService = new ApiService(_authService);
            _apiServiceEtiquetas = new ApiServiceEtiquetas(_authService);

            try
            {
                var lMainViewModel = new MainViewModel(_apiService, _apiServiceEtiquetas);
                var lMainWindow = new MainWindow { DataContext = lMainViewModel };
                lMainWindow.Show();
                ShowUpdateSuccessIfExists();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error crítico al abrir ventana principal: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

    private void ShowUpdateSuccessIfExists()
    {
        try
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string logPath = Path.Combine(appDir, "update_log.txt");
            string tempDir = Path.Combine(appDir, "UpdateTemp");

            if (File.Exists(logPath))
            {
                string logContent = File.ReadAllText(logPath);

                if (logContent.Contains("Relanzando aplicación"))
                {
                    CustomMessageBox.Show("¡Actualización aplicada correctamente!", "Actualización", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Limpiar log
                    File.Delete(logPath);
                }
            }

            // Limpieza de carpeta UpdateTemp por si quedó algo
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
        catch
        {
            // Ignorar errores silenciosamente para no interrumpir la app
        }
    }



}

