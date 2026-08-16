using APP.GK.WPF.CustomComponents;
using APP.GK.WPF.Helpers;
using APP.GK.WPF.Modelos;
using APP.GK.WPF.Servicios;
using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace APP.GK.WPF.ViewModels
{
    public class OrderDetailViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly VisionSessionCoordinator _visionSessionCoordinator;

        // Dos runtimes independientes:
        // - cámara principal (EAN o QR_Abajo)
        // - cámara 2 (QR_Arriba)
        private readonly VisionReadingRuntimeService _runtimeCamera1;
        private readonly VisionReadingRuntimeService _runtimeCamera2;

        private readonly CameraChannelState _channelCamera1;
        private readonly CameraChannelState _channelCamera2;

        private const bool UseMockCameraForTesting = true;

        public ObservableCollection<OrdenProduccion> Orders { get; set; } = new ObservableCollection<OrdenProduccion>();
        public ObservableCollection<string> TipoEtiquetas { get; set; } = new ObservableCollection<string>();

        // Se dejan para no romper bindings actuales aunque visualmente ya no sean relevantes
        public ObservableCollection<VisionLectura> VisionLecturas { get; set; } = new ObservableCollection<VisionLectura>();
        public ObservableCollection<VisionOrdenResumen> VisionResumenes { get; set; } = new ObservableCollection<VisionOrdenResumen>();

        private OrdenProduccion _selectedOrder;
        public OrdenProduccion SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                var lPreviousOrder = _selectedOrder;
                _selectedOrder = value;
                OnPropertyChanged();

                if (HasAnyListening() &&
                    lPreviousOrder != null &&
                    lPreviousOrder.OP != "Seleccione una orden" &&
                    _selectedOrder != null &&
                    lPreviousOrder.OP != _selectedOrder.OP)
                {
                    _ = StopAllListeningAndFinalizeAsync("Lecturas finalizadas por cambio de orden.");
                    _ = LoadSelectedOrderDataAsync();
                }
                else
                {
                    if (_selectedOrder != null && _selectedOrder.OP != "Seleccione una orden")
                    {
                        _ = LoadSelectedOrderDataAsync();
                    }
                    else
                    {
                        Order = null;
                    }
                }

                RaiseCommandStates();
            }
        }

        private OrdenDetalleProduccion _order;
        public OrdenDetalleProduccion Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged();
                RaiseCommandStates();
            }
        }

        private string _tipoEtiqueta;
        public string TipoEtiqueta
        {
            get => _tipoEtiqueta;
            set
            {
                _tipoEtiqueta = value;
                OnPropertyChanged();
                RaiseCommandStates();
            }
        }

        private string _estadoLecturasTexto;
        public string EstadoLecturasTexto
        {
            get => _estadoLecturasTexto;
            set
            {
                _estadoLecturasTexto = value;
                OnPropertyChanged();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public string Linea { get; set; }

        public string TipoDestinoText
        {
            get
            {
                string tipoDestino = ConfigurationManager.AppSettings["TipoDestino"];
                if (tipoDestino == "2")
                    return "Loncheado";
                if (tipoDestino == "5")
                    return "Empaquetado";
                return tipoDestino;
            }
        }

        public bool HasOrders => Orders != null && Orders.Count > 1;

        public string EmptyMessage =>
            $"No hay OPs de Fabricacion para la linea '{Linea}' con el tipo destino '{TipoDestinoText}' para la fecha '{DateTime.Today:yyyy-MM-dd}' (que es hoy)";

        public ICommand LoadOrderDetailCommand { get; }
        public ICommand SendCommand { get; }
        public ICommand FinalizarLecturasCommand { get; }

        public OrderDetailViewModel(ApiService pApiService)
        {
            _apiService = pApiService;
            _visionSessionCoordinator = new VisionSessionCoordinator(pApiService);

            // Runtime cámara 1
            _runtimeCamera1 = new VisionReadingRuntimeService(
                new CameraReadListenerService(),
                UseMockCameraForTesting);

            // Runtime cámara 2
            _runtimeCamera2 = new VisionReadingRuntimeService(
                new CameraReadListenerService(),
                UseMockCameraForTesting);

            _channelCamera1 = new CameraChannelState("Camara1");
            _channelCamera2 = new CameraChannelState("Camara2");

            // Eventos cámara 1
            _runtimeCamera1.OnLecturaRecibida += async pLectura =>
            {
                await ProcessIncomingLecturaAsync(_channelCamera1, pLectura);
            };
            _runtimeCamera1.OnEstadoInfo += pEstado =>
            {
                EstadoLecturasTexto = pEstado;
            };
            _runtimeCamera1.OnCommunicationError += async pError =>
            {
                EstadoLecturasTexto = pError;
                await StopChannelAndFinalizeAsync(_channelCamera1);
            };

            // Eventos cámara 2
            _runtimeCamera2.OnLecturaRecibida += async pLectura =>
            {
                await ProcessIncomingLecturaAsync(_channelCamera2, pLectura);
            };
            _runtimeCamera2.OnEstadoInfo += pEstado =>
            {
                EstadoLecturasTexto = pEstado;
            };
            _runtimeCamera2.OnCommunicationError += async pError =>
            {
                EstadoLecturasTexto = pError;
                await StopChannelAndFinalizeAsync(_channelCamera2);
            };

            _channelCamera1.Runtime = _runtimeCamera1;
            _channelCamera2.Runtime = _runtimeCamera2;

            Linea = ConfigurationManager.AppSettings["Linea"];

            Orders.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasOrders));

            Task.Run(async () => await LoadOrdersAsync());
            LoadTiposEtiquetas();

            SendCommand = new RelayCommand(async _ => await SendToCameraAsync(), _ => CanSendToCamera());
            FinalizarLecturasCommand = new RelayCommand(async _ => await StopAllListeningAndFinalizeAsync("Lecturas finalizadas."), _ => HasAnyListening());
            LoadOrderDetailCommand = new RelayCommand(async _ => await LoadOrderDetailAsync());
        }

        private void RaiseCommandStates()
        {
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FinalizarLecturasCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private bool CanSendToCamera()
        {
            return SelectedOrder != null &&
                   SelectedOrder.OP != "Seleccione una orden" &&
                   Order != null &&
                   !string.IsNullOrEmpty(Order.OPFabricacion) &&
                   !string.IsNullOrEmpty(TipoEtiqueta) &&
                   TipoEtiqueta != "Seleccione un tipo de etiqueta...";
        }

        private bool HasAnyListening()
        {
            return _channelCamera1.IsListening || _channelCamera2.IsListening;
        }

        private bool IsEanSelection(string pTipo)
        {
            return string.Equals(pTipo, "EAN_Arriba", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pTipo, "EAN_Abajo", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsQrSelection(string pTipo)
        {
            return string.Equals(pTipo, "QR_Arriba", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pTipo, "QR_Abajo", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCameraArribaSelection(string pTipo)
        {
            return string.Equals(pTipo, "QR_Arriba", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pTipo, "EAN_Arriba", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsCameraAbajoSelection(string pTipo)
        {
            return string.Equals(pTipo, "QR_Abajo", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(pTipo, "EAN_Abajo", StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadSelectedOrderDataAsync()
        {
            await LoadOrderDetailAsync();
        }

        private async Task LoadOrdersAsync()
        {
            try
            {
                IsLoading = true;
                var lPaginatedResult = await _apiService.GetOrdersAsync(1, 1000);

                if (lPaginatedResult != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Orders.Clear();

                        var dummyOrder = new OrdenProduccion { OP = "Seleccione una orden" };
                        Orders.Add(dummyOrder);

                        foreach (OrdenProduccion lOrdenItem in lPaginatedResult.Items)
                        {
                            if (!string.IsNullOrEmpty(lOrdenItem.OP))
                                Orders.Add(lOrdenItem);
                        }

                        SelectedOrder = dummyOrder;
                    });
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error cargando órdenes en el combobox: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(1000);
                IsLoading = false;
            }
        }

        private async Task LoadOrderDetailAsync()
        {
            if (SelectedOrder == null || SelectedOrder.OP == "Seleccione una orden")
                return;

            try
            {
                OrdenDetalleProduccion lOrderDetail = await _apiService.GetOrderDetailAsync(SelectedOrder.OP);

                if (lOrderDetail != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        Order = lOrderDetail;
                    });
                }
                else
                {
                    CustomMessageBox.Show("Error cargando detalles de ésta orden, no se encuentran los datos", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error en LoadOrderDetailAsync: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Regla funcional final:
        /// - Si hay una sesión EAN activa, solo se permiten más EAN.
        /// - Si hay una sesión QR activa, solo se permiten más QR.
        /// - No se puede mezclar EAN y QR dentro de la misma ejecución de la app.
        /// - Para cambiar de familia, el usuario debe cerrar la aplicación.
        /// </summary>
        private bool ValidateTipoChangeRules(string pSelectedTipo)
        {
            bool lSelectedIsEan = IsEanSelection(pSelectedTipo);
            bool lSelectedIsQr = IsQrSelection(pSelectedTipo);

            bool lHasAnyEanListening =
                (_channelCamera1.IsListening && IsEanSelection(_channelCamera1.CurrentTipoEtiqueta)) ||
                (_channelCamera2.IsListening && IsEanSelection(_channelCamera2.CurrentTipoEtiqueta));

            bool lHasAnyQrListening =
                (_channelCamera1.IsListening && IsQrSelection(_channelCamera1.CurrentTipoEtiqueta)) ||
                (_channelCamera2.IsListening && IsQrSelection(_channelCamera2.CurrentTipoEtiqueta));

            // Si intenta lanzar EAN teniendo QR activo, bloqueamos
            if (lSelectedIsEan && lHasAnyQrListening)
            {
                CustomMessageBox.Show(
                    "No se puede cambiar de tipo porque ahora mismo la aplicación está a la escucha de QR. Para cambiar a EAN debe cerrar la aplicación antes de continuar.",
                    "ATENCIÓN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            // Si intenta lanzar QR teniendo EAN activo, bloqueamos
            if (lSelectedIsQr && lHasAnyEanListening)
            {
                CustomMessageBox.Show(
                    "No se puede cambiar de tipo porque ahora mismo la aplicación está a la escucha de EAN. Para cambiar a QR debe cerrar la aplicación antes de continuar.",
                    "ATENCIÓN",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return false;
            }

            return true;
        }

        private async Task SendToCameraAsync()
        {
            try
            {
                if (Order == null)
                    await LoadOrderDetailAsync();

                if (Order == null)
                {
                    CustomMessageBox.Show("No se han podido cargar los detalles de la orden.", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateTipoChangeRules(TipoEtiqueta))
                    return;

                VisionRoutingInfo lRouting = await ResolveRoutingInfoAsync();
                if (lRouting == null || !lRouting.IsValid)
                    return;

                if (!_apiService.PingCameraSuccess(lRouting.CameraEndpoint))
                    return;

                // Reutiliza o crea el registro por ORDEN + IP CÁMARA
                VisionOrdenResumen? lResumen = await _visionSessionCoordinator.StartOrReuseSessionAsync(
                    Order,
                    lRouting.TipoEtiquetaBackend,
                    lRouting.PosicionQr,
                    lRouting.CodigoEsperado,
                    lRouting.CameraEndpoint.Ip);

                if (lResumen == null)
                    return;

                bool lEnvioOk = await _apiService.SendCommandsToCameraAsync(
                    lRouting.CodigoEsperado,
                    lRouting.CameraEndpoint);

                if (!lEnvioOk)
                {
                    await _visionSessionCoordinator.FinalizeSessionAsync(lResumen.Id);
                    return;
                }

                CustomMessageBox.Show(
                    $"Se ha enviado el código '{lRouting.CodigoEsperado}' a la cámara con éxito.",
                    "ÉXITO",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                CameraChannelState lChannel = lRouting.ChannelKey == "Camara2"
                    ? _channelCamera2
                    : _channelCamera1;

                lChannel.ResumenId = lResumen.Id;
                lChannel.CurrentTipoEtiqueta = lRouting.TipoEtiquetaLectura;
                lChannel.CodigoEsperadoActual = lRouting.CodigoEsperado;
                lChannel.Endpoint = lRouting.CameraEndpoint;

                // Importante:
                // si ya está escuchando este canal, no hacemos nada raro,
                // solo seguimos escuchando de forma transparente.
                if (!lChannel.IsListening)
                {
                    bool lStarted = await lChannel.Runtime.StartAsync(
                        lRouting.CodigoEsperado,
                        lRouting.CameraEndpoint);

                    if (!lStarted)
                    {
                        await _visionSessionCoordinator.FinalizeSessionAsync(lResumen.Id);
                        return;
                    }

                    lChannel.IsListening = true;
                }

                EstadoLecturasTexto = "Escuchando lecturas...";
                RaiseCommandStates();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error en SendToCameraAsync: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<VisionRoutingInfo?> ResolveRoutingInfoAsync()
        {
            CameraEndpoint lEndpoint = _apiService.GetCameraEndpointByTipoSeleccionado(TipoEtiqueta);

            VisionRoutingInfo lInfo = new VisionRoutingInfo
            {
                Selection = TipoEtiqueta,
                CameraEndpoint = lEndpoint,
                ChannelKey = string.Equals(lEndpoint.Name, "Camara2", StringComparison.OrdinalIgnoreCase)
                    ? "Camara2"
                    : "Camara1"
            };

            // ==========================================================
            // QR ARRIBA
            // ==========================================================
            if (string.Equals(TipoEtiqueta, "QR_Arriba", StringComparison.OrdinalIgnoreCase))
            {
                lInfo.TipoEtiquetaBackend = "QR";
                lInfo.TipoEtiquetaLectura = "QR_Arriba";
                lInfo.PosicionQr = "Superior";
                lInfo.CodigoEsperado = await _apiService.GetQrAsync(Order.CodigoProducto, "QR_Arriba");
            }
            // ==========================================================
            // QR ABAJO
            // ==========================================================
            else if (string.Equals(TipoEtiqueta, "QR_Abajo", StringComparison.OrdinalIgnoreCase))
            {
                lInfo.TipoEtiquetaBackend = "QR";
                lInfo.TipoEtiquetaLectura = "QR_Abajo";
                lInfo.PosicionQr = "Inferior";
                lInfo.CodigoEsperado = await _apiService.GetQrAsync(Order.CodigoProducto, "QR_Abajo");
            }
            // ==========================================================
            // EAN ARRIBA
            // IMPORTANTE:
            // Para backend de código de barras se manda siempre "EAN13"
            // aunque funcionalmente queramos auditar EAN_Arriba.
            // ==========================================================
            else if (string.Equals(TipoEtiqueta, "EAN_Arriba", StringComparison.OrdinalIgnoreCase))
            {
                lInfo.TipoEtiquetaBackend = "EAN";
                lInfo.TipoEtiquetaLectura = "EAN_Arriba";
                lInfo.PosicionQr = "Superior";
                lInfo.CodigoEsperado = await _apiService.GetBarcodeAsync(Order.CodigoProducto, "EAN13");
            }
            // ==========================================================
            // EAN ABAJO
            // IMPORTANTE:
            // Para backend de código de barras se manda siempre "EAN13"
            // aunque funcionalmente queramos auditar EAN_Abajo.
            // ==========================================================
            else if (string.Equals(TipoEtiqueta, "EAN_Abajo", StringComparison.OrdinalIgnoreCase))
            {
                lInfo.TipoEtiquetaBackend = "EAN";
                lInfo.TipoEtiquetaLectura = "EAN_Abajo";
                lInfo.PosicionQr = "Inferior";
                lInfo.CodigoEsperado = await _apiService.GetBarcodeAsync(Order.CodigoProducto, "EAN13");
            }
            else
            {
                CustomMessageBox.Show(
                    $"El tipo '{TipoEtiqueta}' no está soportado.",
                    "WARNING",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                lInfo.IsValid = false;
                return lInfo;
            }

            if (string.IsNullOrWhiteSpace(lInfo.CodigoEsperado))
            {
                if (IsQrSelection(TipoEtiqueta))
                {
                    CustomMessageBox.Show(
                        $"El producto '{Order.CodigoProducto}' no tiene '{TipoEtiqueta}' dado de alta en el sistema. Avise en oficina.",
                        "WARNING",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    CustomMessageBox.Show(
                        $"El producto '{Order.CodigoProducto}' no tiene EAN13 dado de alta en el sistema. Avise en oficina.",
                        "WARNING",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                lInfo.IsValid = false;
                return lInfo;
            }

            lInfo.IsValid = true;
            return lInfo;
        }

        private async Task ProcessIncomingLecturaAsync(CameraChannelState pChannel, string pLecturaRaw)
        {
            try
            {
                if (pChannel == null || !pChannel.IsListening || !pChannel.ResumenId.HasValue)
                    return;

                if (string.IsNullOrWhiteSpace(pLecturaRaw))
                    return;

                VisionLectura? lLectura = await _visionSessionCoordinator.RegisterLecturaAsync(
                    pChannel.ResumenId.Value,
                    pLecturaRaw,
                    pChannel.CurrentTipoEtiqueta);

                if (lLectura != null)
                {
                    // Aunque visualmente no lo quieran, no molesta dejarlo por compatibilidad
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        VisionLecturas.Insert(0, lLectura);
                    });
                }
            }
            catch
            {
                // Silencioso a propósito
            }
        }

        private async Task StopChannelAndFinalizeAsync(CameraChannelState pChannel)
        {
            if (pChannel == null)
                return;

            try
            {
                if (pChannel.IsListening)
                {
                    await pChannel.Runtime.StopAsync();
                    pChannel.IsListening = false;
                }

                if (pChannel.ResumenId.HasValue)
                {
                    await _visionSessionCoordinator.FinalizeSessionAsync(pChannel.ResumenId.Value);
                }

                pChannel.ResumenId = null;
                pChannel.CurrentTipoEtiqueta = null;
                pChannel.CodigoEsperadoActual = null;
            }
            catch
            {
                // Silencioso
            }
            finally
            {
                RaiseCommandStates();
            }
        }

        private async Task StopAllListeningAndFinalizeAsync(string pEstadoFinal)
        {
            await StopChannelAndFinalizeAsync(_channelCamera1);
            await StopChannelAndFinalizeAsync(_channelCamera2);

            EstadoLecturasTexto = pEstadoFinal;
            RaiseCommandStates();
        }

        private void LoadTiposEtiquetas()
        {
            TipoEtiquetas.Clear();
            TipoEtiquetas.Add("Seleccione un tipo de etiqueta...");

            string lConfigTipos = ConfigurationManager.AppSettings["TipoEtiqueta"];
            if (!string.IsNullOrEmpty(lConfigTipos))
            {
                var lTipos = lConfigTipos.Split(',').Select(t => t.Trim()).ToList();
                foreach (string t in lTipos)
                {
                    TipoEtiquetas.Add(t);
                }
            }

            TipoEtiqueta = "Seleccione un tipo de etiqueta...";
        }

        /// <summary>
        /// Método que puedes llamar al cerrar la app
        /// para dejar Activa = false en los resúmenes que estén escuchando.
        /// </summary>
        public async Task FinalizeOpenVisionSessionsAsync()
        {
            await StopAllListeningAndFinalizeAsync("Aplicación cerrada.");
        }

        private class CameraChannelState
        {
            public CameraChannelState(string pName)
            {
                Name = pName;
            }

            public string Name { get; }
            public VisionReadingRuntimeService Runtime { get; set; }
            public bool IsListening { get; set; }
            public Guid? ResumenId { get; set; }
            public string? CurrentTipoEtiqueta { get; set; }
            public string? CodigoEsperadoActual { get; set; }
            public CameraEndpoint? Endpoint { get; set; }
        }

        private class VisionRoutingInfo
        {
            public bool IsValid { get; set; }
            public string Selection { get; set; } = string.Empty;
            public string TipoEtiquetaBackend { get; set; } = string.Empty;
            public string TipoEtiquetaLectura { get; set; } = string.Empty;
            public string? PosicionQr { get; set; }
            public string CodigoEsperado { get; set; } = string.Empty;
            public CameraEndpoint CameraEndpoint { get; set; } = new CameraEndpoint();
            public string ChannelKey { get; set; } = string.Empty;
        }
    }
}