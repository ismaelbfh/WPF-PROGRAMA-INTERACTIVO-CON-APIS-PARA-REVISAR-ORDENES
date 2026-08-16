using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WPF.GK.BARTENDER.CustomComponents;
using WPF.GK.BARTENDER.Helpers;
using WPF.GK.BARTENDER.Servicios;
using Newtonsoft.Json;

namespace WPF.GK.BARTENDER.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ApiServiceEtiquetas _apiServiceEtiquetas;

        private BaseViewModel _currentViewModel;
        public BaseViewModel CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        // Propiedad para controlar el estado del menú (expandido/colapsado)
        private bool _isMenuExpanded = true;
        public bool IsMenuExpanded
        {
            get => _isMenuExpanded;
            set { _isMenuExpanded = value; OnPropertyChanged(); }
        }

        private bool _isHomeSelected;
        public bool IsHomeSelected
        {
            get => _isHomeSelected;
            set { _isHomeSelected = value; OnPropertyChanged(); }
        }

        private bool _isOrdersSelected;
        public bool IsOrdersSelected
        {
            get => _isOrdersSelected;
            set { _isOrdersSelected = value; OnPropertyChanged(); }
        }

        public ICommand ShowHomeCommand { get; }
        public ICommand ShowOrdersCommand { get; }
        public ICommand ShowOrderDetailCommand { get; }
        public ICommand CheckUpdateCommand { get; }

        public MainViewModel(ApiService pApiService, ApiServiceEtiquetas pApiServiceEtiquetas)
        {
            _apiService = pApiService;
            _apiServiceEtiquetas = pApiServiceEtiquetas;
            ShowHomeCommand = new RelayCommand(_ => ShowHome());
            ShowOrdersCommand = new RelayCommand(_ => ShowOrders());
            CheckUpdateCommand = new RelayCommand(async _ => await CheckForUpdates());

            ShowHome();
        }

        // Propiedad para mostrar la versión actual en la vista.
        public string AppVersion => $"Versión: {_apiServiceEtiquetas.GetCurrentVersion()}";

        private void ShowHome()
        {
            CurrentViewModel = new HomeViewModel(_apiService, _apiServiceEtiquetas, this);
            IsHomeSelected = true;
            IsOrdersSelected = false;
        }

        private void ShowOrders()
        {
            CurrentViewModel = new OrdersViewModel(_apiService, _apiServiceEtiquetas);
            IsHomeSelected = false;
            IsOrdersSelected = true;
        }


        /// <summary>
        /// Método que se ejecuta al pulsar el botón "Verificar actualizaciones".
        /// Consulta el endpoint de la API, compara versiones y, si corresponde, descarga y ejecuta el instalador.
        /// </summary>
        private async Task CheckForUpdates()
        {
            try
            {
                Version serverVersion = await _apiServiceEtiquetas.GetServerVersionAsync();
                Version currentVersion = _apiServiceEtiquetas.GetCurrentVersion();

                if (serverVersion > currentVersion)
                {
                    var result = CustomMessageBox.Show(
                        $"Hay una nueva versión disponible: {serverVersion}. ¿Desea actualizar?",
                        "Actualizar", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Descargar ZIP con nuevas DLLs
                        byte[] zipBytes = await _apiServiceEtiquetas.DownloadUpdateAsync();
                        string tempZipPath = Path.Combine(Path.GetTempPath(), "UpdateDlls.zip");
                        File.WriteAllBytes(tempZipPath, zipBytes);

                        // Extraer a carpeta temporal de actualización
                        string tempExtractPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateTemp");
                        if (Directory.Exists(tempExtractPath)) Directory.Delete(tempExtractPath, true);
                        ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);

                        // Guardar ruta para reemplazo en el próximo arranque
                        string pendingUpdatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update_pending.json");
                        File.WriteAllText(pendingUpdatePath, JsonConvert.SerializeObject(new
                        {
                            TempPath = tempExtractPath
                        }));

                        // Cerrar la app para aplicar en el próximo inicio
                        LaunchUpdateScript();
                        Application.Current.Shutdown();
                    }
                }
                else
                {
                    CustomMessageBox.Show("No hay actualizaciones disponibles.",
                                    "Verificar actualizaciones", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error al verificar actualizaciones: {ex.Message}",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LaunchUpdateScript()
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string tempDir = Path.Combine(appDir, "UpdateTemp");
                string batPath = Path.Combine(Path.GetTempPath(), "goikoa_update.bat");
                string exeName = "WPF.GK.BARTENDER.exe";
                string exeFullPath = Path.Combine(appDir, exeName);
                string pendingJsonPath = Path.Combine(appDir, "update_pending.json");
                string logPath = Path.Combine(appDir, "update_log.txt");
                string vbsPath = Path.Combine(Path.GetTempPath(), "goikoa_launcher.vbs");

                string batContent = $@"
                @echo off
                echo [%%DATE%% %%TIME%%] Iniciando actualización... > ""{logPath}""
                timeout /t 2 >nul
                del ""{Path.Combine(tempDir, "WPF.GK.BARTENDER.dll.config")}"" >nul 2>&1
                xcopy /E /Y /Q /D ""{tempDir}"" ""{appDir}"" >> ""{logPath}""
                echo [%%DATE%% %%TIME%%] Archivos copiados. >> ""{logPath}""
                echo [%%DATE%% %%TIME%%] Relanzando aplicación... >> ""{logPath}""
                cscript //B ""{vbsPath}""
                del ""{batPath}"" >> ""{logPath}""
                del ""{pendingJsonPath}"" >> ""{logPath}""
                rmdir /S /Q ""{tempDir}"" >> ""{logPath}""
                ";

                string vbsContent = $@"
Set WshShell = CreateObject(""WScript.Shell"")
WScript.Sleep 2000
WshShell.Run ""\""{exeFullPath}\"""", 0, False
";

                File.WriteAllText(batPath, batContent);
                File.WriteAllText(vbsPath, vbsContent);


                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = batPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creando el script de actualización: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


    }
}
