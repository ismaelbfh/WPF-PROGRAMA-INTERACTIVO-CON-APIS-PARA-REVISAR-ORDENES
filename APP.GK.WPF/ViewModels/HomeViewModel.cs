using System.Windows.Input;
using APP.GK.WPF.Helpers;
using APP.GK.WPF.Servicios;

namespace APP.GK.WPF.ViewModels
{
    public class HomeViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        public ICommand ShowCommandOrders { get; }
        public ICommand ShowCommandCamera { get; }

        private readonly MainViewModel _parentVM;

        public HomeViewModel(ApiService pApiService, MainViewModel pParentVM)
        {
            _apiService = pApiService;
            _parentVM = pParentVM;
            ShowCommandOrders = new RelayCommand(_ => ShowOrders());
            ShowCommandCamera = new RelayCommand(_ => ShowCamera());
        }

        private void ShowOrders()
        {
            _parentVM.CurrentViewModel = new OrdersViewModel(_apiService, _parentVM.FinalizedOrdersService);
            _parentVM.IsHomeSelected = false;
            _parentVM.IsOrdersSelected = true;
            _parentVM.IsCameraSelected = false;
        }
        private void ShowCamera()
        {
            _parentVM.CurrentViewModel = new OrderDetailViewModel(_apiService);
            _parentVM.IsHomeSelected = false;
            _parentVM.IsOrdersSelected = false;
            _parentVM.IsCameraSelected = true;
        }
    }
}
