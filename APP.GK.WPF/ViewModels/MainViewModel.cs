using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using APP.GK.WPF.CustomComponents;
using APP.GK.WPF.Helpers;
using APP.GK.WPF.Servicios;
using Newtonsoft.Json;

namespace APP.GK.WPF.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;

        private BaseViewModel _currentViewModel;

        private OrderDetailViewModel _orderDetailViewModel;

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

        private bool _isCameraSelected;
        public bool IsCameraSelected
        {
            get => _isCameraSelected;
            set { _isCameraSelected = value; OnPropertyChanged(); }
        }

        // Servicio compartido de FinalizedOrders
        public FinalizedOrdersService FinalizedOrdersService { get; }

        public ICommand ShowHomeCommand { get; }
        public ICommand ShowOrdersCommand { get; }
        public ICommand ShowOrderDetailCommand { get; }
        public ICommand CheckUpdateCommand { get; }

        public MainViewModel(ApiService pApiService)
        {
            _apiService = pApiService;
            FinalizedOrdersService = new FinalizedOrdersService();

            ShowHomeCommand = new RelayCommand(_ => ShowHome());
            ShowOrdersCommand = new RelayCommand(_ => ShowOrders());
            ShowOrderDetailCommand = new RelayCommand(_ => ShowOrderDetail());
            CheckUpdateCommand = new RelayCommand(async _ => await CheckForUpdates());

            ShowHome();
        }

        // Propiedad para mostrar la versión actual en la vista.
        public string AppVersion => $"Versión: {_apiService.GetCurrentVersion()}";

        private void ShowHome()
        {
            CurrentViewModel = new HomeViewModel(_apiService, this);
            IsHomeSelected = true;
            IsOrdersSelected = false;
            IsCameraSelected = false;
        }

        private void ShowOrders()
        {
            CurrentViewModel = new OrdersViewModel(_apiService, FinalizedOrdersService);
            IsHomeSelected = false;
            IsOrdersSelected = true;
            IsCameraSelected = false;
        }

        public async Task FinalizeVisionOnAppClosingAsync()
        {
            if (_orderDetailViewModel != null)
            {
                await _orderDetailViewModel.FinalizeOpenVisionSessionsAsync();
            }
        }

        private void ShowOrderDetail()
        {
            if (_orderDetailViewModel == null)
            {
                _orderDetailViewModel = new OrderDetailViewModel(_apiService);
            }

            CurrentViewModel = _orderDetailViewModel;
            IsHomeSelected = false;
            IsOrdersSelected = false;
            IsCameraSelected = true;
        }

        /// <summary>
        /// Método que se ejecuta al pulsar el botón "Verificar actualizaciones".
        /// Consulta el endpoint de la API, compara versiones y, si corresponde, descarga y ejecuta el instalador.
        /// </summary>
        private async Task CheckForUpdates()
        {
            try
            {
                Version serverVersion = await _apiService.GetServerVersionAsync();
                Version currentVersion = _apiService.GetCurrentVersion();

                if (serverVersion > currentVersion)
                {
                    var result = CustomMessageBox.Show(
                        $"Hay una nueva versión disponible: {serverVersion}. ¿Desea actualizar?",
                        "Actualizar", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Descargar ZIP con nuevas DLLs
                        byte[] zipBytes = await _apiService.DownloadUpdateAsync();
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
                string exeName = "APP.GK.WPF.exe";
                string exeFullPath = Path.Combine(appDir, exeName);
                string pendingJsonPath = Path.Combine(appDir, "update_pending.json");
                string logPath = Path.Combine(appDir, "update_log.txt");
                string vbsPath = Path.Combine(Path.GetTempPath(), "goikoa_launcher.vbs");

                string batContent = $@"
                @echo off
                echo [%%DATE%% %%TIME%%] Iniciando actualización... > ""{logPath}""
                timeout /t 2 >nul
                del ""{Path.Combine(tempDir, "APP.GK.WPF.dll.config")}"" >nul 2>&1
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


        private bool FilesAreEqual(string path1, string path2)
        {
            byte[] file1 = File.ReadAllBytes(path1);
            byte[] file2 = File.ReadAllBytes(path2);
            return file1.SequenceEqual(file2);
        }
    }
}
