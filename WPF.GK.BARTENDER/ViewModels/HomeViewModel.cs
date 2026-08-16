using System.Windows.Input;
using WPF.GK.BARTENDER.Helpers;
using WPF.GK.BARTENDER.Servicios;

namespace WPF.GK.BARTENDER.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ApiServiceEtiquetas _apiServiceEtiquetas;
        public ICommand ShowCommandOrders { get; }
        public ICommand ShowCommandCamera { get; }

        private readonly MainViewModel _parentVM;

        public HomeViewModel(ApiService pApiService, ApiServiceEtiquetas pApiServiceEtiquetas, MainViewModel pParentVM)
        {
            _apiService = pApiService;
            _apiServiceEtiquetas = pApiServiceEtiquetas;
            _parentVM = pParentVM;
            ShowCommandOrders = new RelayCommand(_ => ShowOrders());
        }

        private void ShowOrders()
        {
            _parentVM.CurrentViewModel = new OrdersViewModel(_apiService, _apiServiceEtiquetas);
            _parentVM.IsHomeSelected = false;
            _parentVM.IsOrdersSelected = true;
        }
    }
}
