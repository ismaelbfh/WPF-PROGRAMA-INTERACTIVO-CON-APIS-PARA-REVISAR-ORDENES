using System;
using System.Linq;
using System.Windows;
using APP.GK.WPF.ViewModels;

namespace APP.GK.WPF.CustomComponents
{
    public static class CustomMessageBox
    {
        public static MessageBoxResult Show(string pMessage, string pCaption, MessageBoxButton pButtons, MessageBoxImage pIcon)
        {
            MessageBoxResult lBoxResult = MessageBoxResult.None;
            // Se crea el ViewModel con un callback que asigna el resultado y cierra la ventana
            var viewModel = new CustomMessageDialogViewModel(pCaption, pMessage, pButtons, pIcon, r =>
            {
                lBoxResult = r;
                // Se cierra la ventana; el callback se ejecutará en el contexto de la ventana
                foreach (Window win in Application.Current.Windows.OfType<CustomMessageDialog>())
                {
                    win.Close();
                    break;
                }
            });
            // Se crea la ventana y se asigna el DataContext
            var dialog = new CustomMessageDialog
            {
                DataContext = viewModel
            };

            // Asigna el Owner solo si existe una ventana principal válida y no es la misma que el diálogo
            if (Application.Current != null &&
                Application.Current.MainWindow != null &&
                Application.Current.MainWindow != dialog)
            {
                dialog.Owner = Application.Current.MainWindow;
            }

            dialog.ShowDialog();
            return lBoxResult;
        }
    }
}
