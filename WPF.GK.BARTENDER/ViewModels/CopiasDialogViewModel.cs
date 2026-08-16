using System;
using System.Windows.Input;
using WPF.GK.BARTENDER.Helpers;

namespace WPF.GK.BARTENDER.ViewModels
{
    public class CopiasDialogViewModel : BaseViewModel
    {
        private int _copias = 1;
        public int Copias
        {
            get => _copias;
            set
            {
                if (value < 1) value = 1;
                _copias = value;
                OnPropertyChanged();
            }
        }

        public ICommand AceptarCommand { get; }
        public ICommand CancelarCommand { get; }
        public ICommand CloseCommand { get; }

        public Action<bool> CerrarVentana { get; set; }

        public CopiasDialogViewModel()
        {
            AceptarCommand = new RelayCommand(_ => CerrarVentana?.Invoke(true));
            CancelarCommand = new RelayCommand(_ => CerrarVentana?.Invoke(false));
            CloseCommand = CancelarCommand;
        }
    }
}
