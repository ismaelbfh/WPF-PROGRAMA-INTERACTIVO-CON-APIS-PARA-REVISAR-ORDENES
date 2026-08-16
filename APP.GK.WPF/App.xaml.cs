using System.Configuration;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;
using APP.GK.WPF.CustomComponents;
using APP.GK.WPF.Servicios;
using APP.GK.WPF.ViewModels;
using APP.GK.WPF.Vistas;

namespace APP.GK.WPF
{
    public partial class App : Application
    {
        private ApiService _apiService;
        private bool _isAuthenticating = false;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _apiService = new ApiService();

            try
            {
                if (!_isAuthenticating)
                {
                    _isAuthenticating = true;
                    // Autenticar al iniciar la aplicación
                    var lIsOk = await _apiService.AuthenticateAsync();
                    if (lIsOk)
                    {
                        var lMainViewModel = new MainViewModel(_apiService);
                        var lMainWindow = new MainWindow { DataContext = lMainViewModel };
                        lMainWindow.Show();
                        // Mostrar mensaje si hubo actualización exitosa
                        ShowUpdateSuccessIfExists();
                    }
                    else
                    {
                        _isAuthenticating = false;
                        Shutdown();
                    }
                }
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
}
