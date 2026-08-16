using System.Windows;

namespace APP.GK.WPF.Vistas
{
    public partial class QrChoiceDialog : Window
    {
        // Aquí guardamos el resultado:
        // "QR_Arriba" o "QR_Abajo" o "" si cancela
        public string SelectedQrType { get; private set; } = "";

        public QrChoiceDialog()
        {
            InitializeComponent();
        }

        private void QrArriba_Click(object sender, RoutedEventArgs e)
        {
            SelectedQrType = "QR_Arriba";
            DialogResult = true;  // cierra el modal
        }

        private void QrAbajo_Click(object sender, RoutedEventArgs e)
        {
            SelectedQrType = "QR_Abajo";
            DialogResult = true;  // cierra el modal
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            SelectedQrType = "";
            DialogResult = false; // cierra el modal
        }

        public static string ShowQrChoice(Window owner = null)
        {
            var dlg = new QrChoiceDialog();

            if (owner != null)
                dlg.Owner = owner;

            bool? result = dlg.ShowDialog();
            return result == true ? dlg.SelectedQrType : "";
        }

    }
}
