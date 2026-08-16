using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WPF.GK.BARTENDER.CustomComponents;
using WPF.GK.BARTENDER.Helpers;
using WPF.GK.BARTENDER.Modelos;
using WPF.GK.BARTENDER.Servicios;

namespace WPF.GK.BARTENDER.ViewModels
{
    public class OrdersViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private ApiServiceEtiquetas _apiServiceEtiquetas { get; }
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalRows;
        private string _searchText;
        private OrdenPrevision _selectedOrder;
        private bool _isLoading;
        private string? _currentFilter;

        public bool IsFiltroActivo => !string.IsNullOrWhiteSpace(SearchText);
        public bool IsSinDatosBase => !HasOrders && !IsFiltroActivo && !IsLoading;
        public bool IsSinResultadosFiltro => !HasOrders && IsFiltroActivo && !IsLoading;

        public ObservableCollection<OrdenPrevision> Orders { get; } = new ObservableCollection<OrdenPrevision>();
        public ICollectionView OrdersView { get; }

        public ICommand LoadOrdersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand PrintLabelCommand { get; }
        public ICommand ClearDateFilterCommand { get; }
        public ICommand CheckOrderCommand { get; }

        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value)
                {
                    _currentPage = value;
                    OnPropertyChanged();
                    (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PrevPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public int PageSize
        {
            get => _pageSize;
            set { _pageSize = value; OnPropertyChanged(); }
        }

        public int TotalRows
        {
            get => _totalRows;
            set
            {
                if (_totalRows != value)
                {
                    _totalRows = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalPages));
                    (NextPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (PrevPageCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalRows / PageSize);

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EmptyMessage));
                OnPropertyChanged(nameof(IsFiltroActivo));
                OnPropertyChanged(nameof(IsSinDatosBase));
                OnPropertyChanged(nameof(IsSinResultadosFiltro));
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    _currentFilter = null;
                    CurrentPage = 1;
                    _ = LoadOrdersAsync();
                }
            }
        }

        public OrdenPrevision SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                if (_selectedOrder != value)
                {
                    _selectedOrder = value;
                    OnPropertyChanged();
                    (PrintLabelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSinDatosBase));
                OnPropertyChanged(nameof(IsSinResultadosFiltro));
            }
        }

        public string EmptyMessage
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SearchText))
                    return "No se han encontrado resultados a su búsqueda.";
                else
                    return $"No hay OPs de fabricación para la fecha '{DateTime.Today:yyyy-MM-dd}' que se puedan mostrar.";
            }
        }

        public bool HasOrders => Orders.Count > 0;

        private ObservableCollection<string> _printerNames = new ObservableCollection<string>();
        public ObservableCollection<string> PrinterNames
        {
            get => _printerNames;
            set { _printerNames = value; OnPropertyChanged(); }
        }

        private string _selectedPrinter;
        public string SelectedPrinter
        {
            get => _selectedPrinter;
            set { _selectedPrinter = value; OnPropertyChanged(); }
        }

        private DateTime? _selectedDate = DateTime.Today;
        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFiltroFechaActivo));
                    _ = LoadOrdersAsync(_currentFilter, _selectedDate);
                }
            }
        }

        public bool IsFiltroFechaActivo => SelectedDate != null && SelectedDate != DateTime.Today;

        public OrdersViewModel(ApiService apiService, ApiServiceEtiquetas apiServiceEtiquetas)
        {
            _apiService = apiService;
            _apiServiceEtiquetas = apiServiceEtiquetas;

            Orders.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasOrders));
                OnPropertyChanged(nameof(IsSinDatosBase));
                OnPropertyChanged(nameof(IsSinResultadosFiltro));
            };

            LoadOrdersCommand = new RelayCommand(async _ => await LoadOrdersAsync());
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            NextPageCommand = new RelayCommand(async _ => await ChangePageAsync(CurrentPage + 1), _ => CurrentPage < TotalPages);
            PrevPageCommand = new RelayCommand(async _ => await ChangePageAsync(CurrentPage - 1), _ => CurrentPage > 1);
            SearchCommand = new RelayCommand(async _ => await BuscarAsync());
            PrintLabelCommand = new RelayCommand(async _ => await PrintLabelAsync(), _ => SelectedOrder != null);

            ClearDateFilterCommand = new RelayCommand(_ =>
            {
                SelectedDate = DateTime.Today;
            });

            OrdersView = CollectionViewSource.GetDefaultView(Orders);
            CheckOrderCommand = new RelayCommand(param => OnCheckOrder(param as OrdenPrevision));

            _ = LoadOrdersAsync();
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            var configPrinters = ConfigurationManager.AppSettings["PrintersName"];
            if (!string.IsNullOrWhiteSpace(configPrinters))
            {
                var printers = configPrinters.Split(',').Select(p => p.Trim()).ToList();
                foreach (var printer in printers)
                    PrinterNames.Add(printer);

                SelectedPrinter = PrinterNames.FirstOrDefault();
            }
        }

        private void OnCheckOrder(OrdenPrevision selected)
        {
            if (selected == null) return;

            SelectedOrder = selected;
            (PrintLabelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task BuscarAsync()
        {
            _currentFilter = SearchText;
            CurrentPage = 1;
            await LoadOrdersAsync(_currentFilter, SelectedDate);
        }

        private async Task LoadOrdersAsync(string? filtro = null, DateTime? fecha = null)
        {
            try
            {
                IsLoading = true;
                var fechaConsulta = fecha ?? SelectedDate ?? DateTime.Today;
                var result = await _apiService.GetOrdersAsync(CurrentPage, PageSize, filtro, fechaConsulta);
                if (result != null)
                {
                    Orders.Clear();
                    OnPropertyChanged(nameof(EmptyMessage));
                    foreach (var o in result.Items)
                        Orders.Add(o);

                    TotalRows = result.TotalRows;
                    OrdersView.Refresh();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error cargando órdenes: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private async Task RefreshAsync()
        {
            // 1) Reset visual del DatePicker a HOY sin disparar otra carga extra
            _selectedDate = DateTime.Today;
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(IsFiltroFechaActivo));

            // 2) Comportamiento que ya tenías (no lo cambio)
            await LoadOrdersAsync();
            SearchText = string.Empty;
            _currentFilter = null;
            CurrentPage = 1;

            OnPropertyChanged(nameof(EmptyMessage));
            CustomMessageBox.Show("Listado actualizado con éxito", "Actualizar", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task ChangePageAsync(int newPage)
        {
            if (newPage < 1 || newPage > TotalPages) return;
            CurrentPage = newPage;
            await LoadOrdersAsync(_currentFilter, SelectedDate);
        }

        /// <summary>
        /// Valida y convierte un valor cuando el campo está configurado en Gestión como numérico entero.
        /// Mantiene la funcionalidad actual: solo convierte si realmente se puede convertir.
        /// Si no se puede, lanza un mensaje funcional para el operario indicando que revise el tipo en Gestión.
        /// </summary>
        private string ConvertirANumericoEnteroConMensaje(string nombreCampo, string valorOriginal)
        {
            if (string.IsNullOrWhiteSpace(valorOriginal))
                return valorOriginal ?? "";

            var valorNormalizado = valorOriginal.Trim();

            if (!decimal.TryParse(valorNormalizado.Replace('.', ','), out var decimalValue))
            {
                throw new InvalidOperationException(
                    $"No se puede imprimir porque el campo '{nombreCampo}' está configurado en Gestión como numérico y el valor recibido es '{valorOriginal}'. Revise el tipo de dato configurado en la app de gestión.");
            }

            if (decimalValue > int.MaxValue || decimalValue < int.MinValue)
            {
                throw new InvalidOperationException(
                    $"No se puede imprimir porque el campo '{nombreCampo}' está configurado en Gestión como numérico y el valor recibido es '{valorOriginal}', pero se sale del rango permitido para un número entero. Revise el tipo de dato configurado en la app de gestión.");
            }

            return ((int)decimalValue).ToString();
        }

        /// <summary>
        /// Valida y convierte un valor cuando el campo está configurado en Gestión como decimal.
        /// Mantiene la funcionalidad actual: solo convierte si realmente se puede convertir.
        /// Si no se puede, lanza un mensaje funcional para el operario indicando que revise el tipo en Gestión.
        /// </summary>
        private decimal ConvertirADecimalConMensaje(string nombreCampo, string valorOriginal)
        {
            if (string.IsNullOrWhiteSpace(valorOriginal))
                return 0m;

            var valorNormalizado = valorOriginal.Trim();

            if (!decimal.TryParse(
                    valorNormalizado.Replace('.', ','),
                    out var decimalValue))
            {
                throw new InvalidOperationException(
                    $"No se puede imprimir porque el campo '{nombreCampo}' está configurado en Gestión como decimal y el valor recibido es '{valorOriginal}'. Revise el tipo de dato configurado en la app de gestión.");
            }

            return decimalValue;
        }

        /// <summary>
        /// Método principal de impresión desde la app de planta.
        /// Recupera la etiqueta por código de producto, solicita número de copias, 
        /// construye el diccionario de datos para BarTender (con datos fijos y dinámicos),
        /// valida campos obligatorios y lanza la impresión. Finalmente, registra el histórico.
        /// </summary>
        private async Task PrintLabelAsync()
        {
            try
            {
                // 1) Obtener la etiqueta desde la API en base al código del producto (SelectedOrder.PF)
                var etiqueta = await _apiServiceEtiquetas.GetEtiquetaByCodigoProductoAsync(SelectedOrder.PF);
                if (etiqueta == null)
                    return;

                var plantilla = etiqueta.FK_IdPlantillaNavigation;
                if (plantilla == null)
                {
                    CustomMessageBox.Show(
                        $"Para esa etiqueta no hay ninguna plantilla asociada, por favor contacte con gestion de etiquetas.",
                        "ERROR",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    return;
                }
                // 2) Mostrar modal para elegir el número de copias
                var dialog = new CopiasDialog { Owner = Application.Current.MainWindow };
                bool? resultado = dialog.ShowDialog();
                if (resultado != true)
                    return;

                var numeroCopias = dialog.CopiasSeleccionadas;

                // 3) Crear el diccionario con los datos fijos que se mandarán a BarTender
                var datos = new Dictionary<string, string>
                {
                    ["PRINTERNAME"] = SelectedPrinter,
                    ["RUTAARCHIVO"] = plantilla.RutaArchivo ?? "",
                    ["CLIENTE"] = etiqueta.FK_IdClienteNavigation?.Descripcion ?? "",
                    ["OVALOIDIOMA"] = etiqueta.OvaloIdioma ?? "",
                    ["OVALOPLANTA"] = etiqueta.OvaloPlanta ?? "",
                    ["OVALOCE"] = etiqueta.OvaloCE ?? "",
                    ["DIRECCION"] = etiqueta.FK_IdUbicacionNavigation?.Descripcion ?? "",
                    ["COPIAS"] = numeroCopias.ToString(),
                    ["TIPOENVASADO"] = etiqueta.FK_IdEnvasadoNavigation?.Descripcion ?? "",
                    ["CONSERVACION"] = etiqueta.FK_IdConservacionNavigation?.Descripcion ?? "",
                    ["OP"] = SelectedOrder.OP ?? ""
                };

                // 4) Recorrer los campos dinámicos (por plantilla)
                if (plantilla?.PlantillaCampos != null)
                {
                    foreach (var plantillaCampo in plantilla.PlantillaCampos)
                    {
                        string key = plantillaCampo.NombreCampoBartender ?? "";
                        int tipo = plantillaCampo.FK_IdCampoNavigation?.FK_IdTipoCampoNavigation.PK_IdTipoCampo ?? 0;
                        bool isNav = plantillaCampo.FK_IdCampoNavigation?.IsNavision ?? false;
                        string valor = "";

                        if (isNav) //solo si el campo es de navision
                        {
                            // Nuevo: obtener la propiedad real desde la descripción del campo de Navision
                            string? propiedadEnSelectedOrder = plantillaCampo.FK_IdCampoNavigation?.FK_IdCampoNavisionNavigation?.Descripcion;

                            if (!string.IsNullOrWhiteSpace(propiedadEnSelectedOrder))
                            {
                                //Mediante reflexion obtiene la propiedad que se llamará en la web igual que aquí, es decir aqui la columna 'PesoNeto' haremos
                                //match con el nombre que devuelve el DTO del backend para obtener su propiedad y con ello poder obtener su valor
                                var prop = SelectedOrder.GetType().GetProperty(propiedadEnSelectedOrder);
                                if (prop != null)
                                    valor = prop.GetValue(SelectedOrder)?.ToString() ?? "";

                                if (tipo == 1) // Tipo numerico int parsear a int
                                {
                                    if (!string.IsNullOrWhiteSpace(valor))
                                    {
                                        valor = ConvertirANumericoEnteroConMensaje(key, valor);
                                    }
                                }
                                else if (tipo == 3) // Tipo decimal parsear a dos digitos en gr o kg
                                {
                                    if (!string.IsNullOrWhiteSpace(valor))
                                    {
                                        var decimalValue = ConvertirADecimalConMensaje(key, valor);

                                        if (propiedadEnSelectedOrder == "PESONETO")
                                        {
                                            if (decimalValue < 1) //si es < 1 significa que estan viniendo menos de 1 kg y por tanto lo representaremos en gramos
                                            {
                                                valor = ((int)(decimalValue * 1000)).ToString() + " gr";
                                            }
                                            else // si son >= 1 kg simplemente dejamos 2 decimales para representar los kg con decimales
                                            {
                                                valor = decimalValue.ToString("0.00") + " kg";
                                            }
                                        }
                                        else //para el resto de propiedades decimales solamente deja todos sus digitos separado con coma
                                        {
                                            valor = decimalValue.ToString("G29").Replace('.', ',');
                                        }
                                    }
                                }
                            }
                        }
                        else // campo no es de Navision → buscar valor en EtiquetaCamposValores y guardarlo
                        {
                            var match = etiqueta.EtiquetaCamposValores?.FirstOrDefault(v => v.FK_IdCampo == plantillaCampo.FK_IdCampo);
                            valor = match?.ValorCampo ?? "";
                        }

                        datos[key] = valor; //añadimos al diccionario lo que se va a enviar a bartender en este caso hemos ido añadiendo los campos de etiqueta con sus respectivos valores
                    }
                }

                // 5) Generar código/s de barra compuesto/s según el patrón definido en la etiqueta
                var mapCodigoCampo = new Dictionary<string, string>
                {
                    { "00", "EAN" },
                    { "01", "DUN" },
                    { "10", "LOTE" },
                    { "11", "FECHA" },
                    { "15", "FECHACADUCIDAD" },
                    { "17", "FECHACADUCIDAD" },
                    { "91", "MAQUINA" },
                    { "37", "UNIDADESCAJA" }
                };

                var codigosBarra = etiqueta.EtiquetaCodigosBarra?
                    .OrderBy(x => x.Orden)
                    .ToList() ?? new List<EtiquetaCodigosBarra>();

                if (codigosBarra.Any())
                {
                    if (codigosBarra.Count == 1)
                    {
                        var patronCB = codigosBarra[0].FK_IdTipoCodigoBarraNavigation?.Descripcion ?? "";
                        string lCodigoBarrasConParentesis = GenerarCodigoBarra(patronCB, SelectedOrder, mapCodigoCampo);
                        string lCodigoBarrasSinParentesis = lCodigoBarrasConParentesis.Replace("(", "").Replace(")", "");

                        datos["TIPOCODIGOBARRAS_FMT"] = lCodigoBarrasConParentesis;
                        datos["TIPOCODIGOBARRAS_RAW"] = lCodigoBarrasSinParentesis;
                    }
                    else
                    {
                        foreach (var codigoBarra in codigosBarra)
                        {
                            var patronCB = codigoBarra.FK_IdTipoCodigoBarraNavigation?.Descripcion ?? "";
                            string lCodigoBarrasConParentesis = GenerarCodigoBarra(patronCB, SelectedOrder, mapCodigoCampo);
                            string lCodigoBarrasSinParentesis = lCodigoBarrasConParentesis.Replace("(", "").Replace(")", "");

                            datos[$"TIPOCODIGOBARRAS{codigoBarra.Orden}_FMT"] = lCodigoBarrasConParentesis;
                            datos[$"TIPOCODIGOBARRAS{codigoBarra.Orden}_RAW"] = lCodigoBarrasSinParentesis;
                        }
                    }
                }
                // 6) Labels multilíngues
                if (etiqueta.EtiquetaLabels != null)
                {
                    // Agrupar y concatenar labels multilíngües por tipo
                    // Ejemplo: tipo "Peso" con 2 idiomas → "PesoLabel": "Peso Neto / Net Weight"
                    var labelsPorTipo = etiqueta.EtiquetaLabels
                        .GroupBy(l => l.FK_IdTipoLabelNavigation?.Descripcion ?? "")
                        .ToDictionary(
                            g => g.Key,
                            g => g
                                .OrderBy(l => l.Orden ?? 0)
                                .Select(l => l.FK_IdLabelNavigation?.DescripcionLabel ?? "")
                                .Where(txt => !string.IsNullOrWhiteSpace(txt))
                                .ToList()
                        );

                    foreach (var kv in labelsPorTipo)
                    {
                        var tipoLabel = kv.Key;
                        var listaLabels = kv.Value;
                        if (listaLabels.Count == 0) continue;

                        var claveJson = tipoLabel.ToUpper() + "LABEL";
                        datos[claveJson] = string.Join(" / ", listaLabels);
                    }
                }
                // 7) Validar campos obligatorios antes de enviar a BarTender
                var erroresCamposObligatorio = new List<string>();

                foreach (var campo in plantilla.PlantillaCampos)
                {
                    var nombreBT = campo.NombreCampoBartender ?? "";
                    var esObligatorio = campo.EsObligatorio ?? false;

                    if (esObligatorio && (!datos.ContainsKey(nombreBT) || string.IsNullOrWhiteSpace(datos[nombreBT])))
                    {
                        erroresCamposObligatorio.Add($"Campo obligatorio sin valor: {nombreBT}");
                    }
                }

                if (erroresCamposObligatorio.Any())
                {
                    CustomMessageBox.Show(
                        $"No se puede imprimir. Faltan los siguientes campos obligatorios:\n\n- {string.Join("\n- ", erroresCamposObligatorio)}",
                        "Campos obligatorios",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return; // Salimos sin imprimir
                }

                // 8) Enviar diccionario al printLabel (esto ya muestra MessageBox internamente)
                var responsePrint = await _apiServiceEtiquetas.PrintLabelAsync(datos);

                //9) Creamos objeto de historico que guardamos SIEMPRE
                HistoricoImpresionDTO hist = new HistoricoImpresionDTO()
                {
                    Cliente = etiqueta.FK_IdClienteNavigation?.Descripcion,
                    CodigoOrden = SelectedOrder.OP,
                    CodigoProducto = SelectedOrder.PF,
                    Envasado = etiqueta.FK_IdEnvasadoNavigation?.Descripcion,
                    FK_IdEtiqueta = etiqueta.PK_IdEtiqueta,
                    IpMaquina = GetLocalIpAddress(),
                    FechaImpresion = DateTime.Now,
                    NombreEtiqueta = etiqueta.NombreEtiqueta,
                    NombrePlantilla = plantilla?.NombrePlantilla,
                    OvaloCE = etiqueta.OvaloCE,
                    OvaloIdioma = etiqueta.OvaloIdioma,
                    OvaloPlanta = etiqueta.OvaloPlanta,
                    RutaArchivo = plantilla?.RutaArchivo,
                    Seccion = etiqueta.FK_IdSeccionNavigation?.Descripcion,
                    TipoCodigoBarras = string.Join(" | ",
                        datos
                            .Where(x => x.Key == "TIPOCODIGOBARRAS_FMT" || x.Key.EndsWith("_FMT"))
                            .OrderBy(x => x.Key)
                            .Select(x => $"{x.Key}: {x.Value}")
                    ),
                    TipoConservacion = etiqueta.FK_IdConservacionNavigation?.Descripcion,
                    TipoEtiqueta = etiqueta.FK_IdTipoEtiquetaNavigation?.Descripcion,
                    UsuarioEtiqueta = etiqueta.Usuario,
                    UsuarioPlanta = ConfigurationManager.AppSettings["UsuarioBartender"],
                    JsonPeticionBartender = JsonConvert.SerializeObject(datos),
                    JsonRespuestaBartender = JsonConvert.SerializeObject(responsePrint),
                    NombreEquipo = Environment.MachineName,
                    UsuarioWindows = Environment.UserName
                };

                var historicoGuardado = await _apiServiceEtiquetas.PostHistoricoImpresionAsync(hist);
                if (!historicoGuardado)
                {
                    CustomMessageBox.Show(
                        "La impresión se ha realizado, pero no se ha podido guardar el histórico de impresión.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }
            catch (Exception ex)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Error imprimiendo etiqueta:");
                sb.AppendLine();
                sb.AppendLine("Mensaje:");
                sb.AppendLine(ex.Message);
                sb.AppendLine();
                sb.AppendLine("Traza:");
                sb.AppendLine(ex.StackTrace ?? "Sin información de traza");

                CustomMessageBox.Show(sb.ToString(), "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public string GetLocalIpAddress()
        {
            string? ipAddress = "";

            // Devuelve la primera IPv4 que no sea loopback ni virtual
            var host = Dns.GetHostEntry(Dns.GetHostName());
            ipAddress = host
                .AddressList
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                   && !IPAddress.IsLoopback(ip))?.ToString();

            return ipAddress ?? "";
        }

        /// <summary>
        /// Genera el valor para el campo de código de barras compuesto según el patrón proporcionado.
        /// Cada parte del patrón representa un Application Identifier (AI) estándar GS1, y se completa
        /// con datos extraídos dinámicamente del objeto SelectedOrder usando reflexión.
        ///
        /// Ejemplo de patrón: "(01)(3103)(17)"
        /// Supongamos:
        ///   - EAN: 8437012345678 → código 01
        ///   - PesoNeto: 2.530 kg → código 3103 (3 decimales)
        ///   - FechaCaducidad: 2026-09-01 → código 17
        /// Resultado generado:
        ///   "(01)8437012345678(3103)002530(17)260901"
        /// </summary>
        public string GenerarCodigoBarra(string patron, object selectedOrder, Dictionary<string, string> mapCodigoCampo)
        {
            var sb = new StringBuilder();

            // Extraemos todos los códigos del patrón (ej: "(01)(3103)(17)" → "01", "3103", "17")
            var matches = System.Text.RegularExpressions.Regex.Matches(patron, @"\((\d+)\)");
            bool esPatronUnico = matches.Count == 1;

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string codigo = match.Groups[1].Value;
                string? propName = null;
                // Intentamos mapear el código (ej: "01" → "EAN")
                if (!mapCodigoCampo.TryGetValue(codigo, out propName))
                {
                    // Si es un código del tipo "310X", se refiere al peso neto (AI estándar GS1)
                    if (codigo.StartsWith("310") && codigo.Length == 4 && char.IsDigit(codigo[3]) && codigo[3] != '0')
                        propName = "PESONETO";
                }

                if (propName != null)
                {
                    // Obtenemos la propiedad del objeto SelectedOrder que coincide con el campo esperado
                    var propInfo = selectedOrder.GetType().GetProperty(propName.ToUpper());
                    if (propInfo != null)
                    {
                        var rawValue = propInfo.GetValue(selectedOrder);
                        string texto = "";

                        if (rawValue != null)
                        {
                            // CASO 1: Peso → (310X) se convierte a gramos con 6 dígitos
                            if (codigo.StartsWith("310") && propName == "PESONETO")
                            {
                                if (decimal.TryParse(
                                    rawValue.ToString()?.Replace(',', '.'),
                                    NumberStyles.Any,
                                    CultureInfo.InvariantCulture,
                                    out var pesoKg))
                                {
                                    // Último dígito del AI: 3102 -> 2, 3103 -> 3, etc.
                                    int decimales = codigo[3] - '0';
                                    decimal factor = (decimal)Math.Pow(10, decimales);
                                    int valorEntero = (int)Math.Round(pesoKg * factor, MidpointRounding.AwayFromZero);
                                    texto = valorEntero.ToString("D6");
                                }
                            }
                            else if (propInfo.PropertyType == typeof(DateTime) || propInfo.PropertyType == typeof(DateTime?))
                            {
                                // CASO 2: Fechas → formato YYMMDD (ej: 2026-09-01 → "260901")
                                var dt = (DateTime)rawValue;
                                texto = dt.ToString("yyMMdd");
                            }
                            else // CASO 3: Cualquier otro tipo de dato → se usa tal cual
                            {
                                if (codigo == "37") // UNIDADESCAJA: debe ir sin decimales
                                {
                                    if (rawValue is decimal dec)
                                        texto = decimal.Truncate(dec).ToString();
                                    else if (rawValue is double dbl)
                                        texto = Math.Truncate(dbl).ToString();
                                    else if (rawValue is int i)
                                        texto = i.ToString();
                                    else
                                    {
                                        if (int.TryParse(rawValue.ToString(), out var intVal))
                                            texto = intVal.ToString();
                                        else if (decimal.TryParse(rawValue.ToString(), out var decVal))
                                            texto = decimal.Truncate(decVal).ToString();
                                        else
                                            texto = rawValue.ToString()!;
                                    }
                                }
                                else
                                {
                                    texto = rawValue.ToString()!;
                                    if (codigo == "91" && texto.Length >= 2)
                                        texto = texto.Substring(0, 2);
                                }
                            }
                        }
                        // Añadir el AI entre paréntesis si hay más de un código en el patrón
                        if (!esPatronUnico)
                            sb.Append($"({codigo})");

                        sb.Append(texto);
                    }
                }
            }

            return sb.ToString();
        }
    }
}