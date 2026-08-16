using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using APP.GK.WPF.ViewModels;

namespace APP.GK.WPF.Vistas
{
    public partial class MainWindow : Window
    {
        private bool _isCloseInProgress = false;
        private bool _allowDirectClose = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            // Si ya hemos autorizado el cierre final, dejamos pasar.
            if (_allowDirectClose)
            {
                return;
            }

            // Si ya estamos en medio del proceso de cierre, no hacemos nada más.
            if (_isCloseInProgress)
            {
                e.Cancel = true;
                return;
            }

            // Primera vez: cancelamos el cierre para poder finalizar listeners/resúmenes.
            e.Cancel = true;
            _isCloseInProgress = true;

            try
            {
                if (DataContext is MainViewModel lMainViewModel)
                {
                    await lMainViewModel.FinalizeVisionOnAppClosingAsync();
                }
            }
            catch
            {
                // Silencioso a propósito: no bloqueamos el cierre por un error aquí.
            }
            finally
            {
                _isCloseInProgress = false;
            }

            // MUY IMPORTANTE:
            // No llamamos a Close() directamente dentro del mismo Closing.
            // Lo reprogramamos en el Dispatcher para que ocurra cuando este ciclo termine.
            _allowDirectClose = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                Close();
            }), DispatcherPriority.Background);
        }
    }
}