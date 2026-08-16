using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using APP.GK.WPF.ViewModels;

namespace APP.GK.WPF.CustomComponents
{
    /// <summary>
    /// Lógica de interacción para CustomMessageDialog.xaml
    /// </summary>
    public partial class CustomMessageDialog : Window
    {
        public CustomMessageDialog()
        {
            InitializeComponent();
            Loaded += CustomMessageDialog_Loaded;
        }

        private void CustomMessageDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is CustomMessageDialogViewModel vm)
            {
                vm.RequestClose += () =>
                {
                    // Puedes definir un valor por defecto si lo necesitas
                    this.DialogResult = false;
                    this.Close();
                };
            }
        }
    }
}
