using System.Windows;
using WPF.GK.BARTENDER.ViewModels;

namespace WPF.GK.BARTENDER.CustomComponents
{
    public partial class CopiasDialog : Window
    {
        public int CopiasSeleccionadas { get; private set; } = 1;

        public CopiasDialog()
        {
            InitializeComponent();

            // Creamos el VM y lo asignamos
            var vm = new CopiasDialogViewModel();
            DataContext = vm;

            // Muy importante: manejar el cierre desde VM
            vm.CerrarVentana = resultado =>
            {
                if (resultado)
                {
                    CopiasSeleccionadas = vm.Copias;
                    DialogResult = true;
                }
                else
                {
                    DialogResult = false;
                }

                Close();
            };
        }

        private void Up_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CopiasDialogViewModel vm)
                vm.Copias++;
        }

        private void Down_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CopiasDialogViewModel vm && vm.Copias > 1)
                vm.Copias--;
        }
    }
}
