using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading.Tasks;
using System.Windows;
using APP.GK.WPF.Modelos;
using APP.GK.WPF.Servicios;
using APP.GK.WPF.Helpers;
using APP.GK.WPF.CustomComponents;

namespace APP.GK.WPF.ViewModels
{
    public class OrdersViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly FinalizedOrdersService _finalizedService;
        private int _currentPage = 1;
        private int _pageSize = 10;
        private int _totalRows;
        private string? _currentFilter;
        public bool IsFiltroActivo => !string.IsNullOrWhiteSpace(SearchText);
        public bool IsSinDatosBase => !HasOrders && !IsFiltroActivo && !IsLoading;
        public bool IsSinResultadosFiltro => !HasOrders && IsFiltroActivo && !IsLoading;

        public ObservableCollection<OrdenProduccion> Orders { get; set; } = new ObservableCollection<OrdenProduccion>();

        // Usamos las colecciones del servicio compartido
        public ObservableCollection<OrdenProduccion> FinalizedOrders { get; }
        public ObservableCollection<string> FinalizedIds { get; }

        public ICommand LoadOrdersCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand FinalizeOrderCommand { get; }  // Nuevo comando

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

        private string _searchText;
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

        private OrdenProduccion _selectedOrder;
        public OrdenProduccion SelectedOrder
        {
            get => _selectedOrder;
            set { _selectedOrder = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
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

        public ICollectionView OrdersView { get; }

        // Propiedades para obtener los valores desde el app.config
        public string Linea { get; set; }
        public string TipoDestinoText
        {
            get
            {
                // Se obtiene el valor desde el app.config
                string tipoDestino = ConfigurationManager.AppSettings["TipoDestino"];
                if (tipoDestino == "2")
                    return "Loncheado";
                else if (tipoDestino == "5")
                    return "Empaquetado";
                else
                    return tipoDestino;
            }
        }
        public string EmptyMessage
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SearchText))
                    return "No se han encontrado resultados a su búsqueda.";
                else
                    return $"No hay OPs de Fabricacion para la linea '{Linea}' con el tipo destino '{TipoDestinoText}' para la fecha '{DateTime.Today:yyyy-MM-dd}' (que es hoy).";
            }
        }
        public bool HasOrders => Orders != null && Orders.Count > 0;

        public OrdersViewModel(ApiService pApiService, FinalizedOrdersService finalizedOrdersService)
        {
            _apiService = pApiService;
            _finalizedService = finalizedOrdersService;
            FinalizedOrders = _finalizedService.FinalizedOrders;
            FinalizedIds = _finalizedService.FinalizedIds;

            // Leer valores desde el app.config
            Linea = ConfigurationManager.AppSettings["Linea"];

            // Suscribirse al evento CollectionChanged para notificar el cambio en HasOrders
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

            // Inicializamos el comando para finalizar la orden
            FinalizeOrderCommand = new RelayCommand(FinalizeOrder, CanFinalizeOrder);
            FinalizedOrders.CollectionChanged += (s, e) =>
            {
                (FinalizeOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };
            OrdersView = CollectionViewSource.GetDefaultView(Orders);

            _ = LoadOrdersAsync();
            
        }

        private async Task BuscarAsync()
        {
            _currentFilter = SearchText;
            CurrentPage = 1;
            await LoadOrdersAsync(_currentFilter);
        }

        private async Task RefreshAsync()
        {
            await LoadOrdersAsync();
            SearchText = string.Empty;
            _currentFilter = null;
            CurrentPage = 1;
            OnPropertyChanged(nameof(EmptyMessage));
            CustomMessageBox.Show("Se acaba de actualizar el listado de las OP Fabricacion.", "Listado actualizado con éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task LoadOrdersAsync(string? filtro = null)
        {
            try
            {
                IsLoading = true;
                var paginatedResult = await _apiService.GetOrdersAsync(CurrentPage, PageSize, filtro);
                
                if (paginatedResult != null)
                {
                    Orders.Clear();
                    OnPropertyChanged(nameof(EmptyMessage));
                    foreach (var order in paginatedResult.Items)
                    {
                        Orders.Add(order);
                    }
                    TotalRows = paginatedResult.TotalRows;
                    OrdersView.Refresh();
                    OnPropertyChanged(nameof(HasOrders));
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error en 'LoadOrdersAsync' en OrdersViewModel: {ex.Message}", "ERROR", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }


        private async Task ChangePageAsync(int newPage)
        {
            if (newPage < 1 || newPage > TotalPages)
                return;
            CurrentPage = newPage;
            await LoadOrdersAsync(_currentFilter);
        }

        // Permite finalizar la orden si aún no está en la colección de finalizadas
        private bool CanFinalizeOrder(object parameter)
        {
            if (parameter is OrdenProduccion order)
            {
                return !FinalizedIds.Contains(order.OP);
            }
            return false;
        }

        // Lógica para finalizar la orden
        private void FinalizeOrder(object parameter)
        {
            if (parameter is OrdenProduccion order)
            {
                var result = CustomMessageBox.Show("¿Desea finalizar la orden seguro?", "Confirmar Finalización", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    FinalizedOrders.Add(order);
                    FinalizedIds.Add(order.OP);

                    if (SelectedOrder == order)
                    {
                        SelectedOrder = null;
                        OnPropertyChanged(nameof(SelectedOrder));
                    }

                    (FinalizeOrderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OrdersView.Refresh();
                }
            }
        }
    }
}
