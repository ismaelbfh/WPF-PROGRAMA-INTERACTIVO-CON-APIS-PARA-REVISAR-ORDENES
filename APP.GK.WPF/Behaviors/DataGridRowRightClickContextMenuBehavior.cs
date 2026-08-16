using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace APP.GK.WPF.Behaviors
{
    public static class DataGridRowRightClickContextMenuBehavior
    {
        // Se define una propiedad adjunta para asignar el comando que se usará en el menú contextual.
        public static readonly DependencyProperty ShowContextMenuCommandProperty =
            DependencyProperty.RegisterAttached(
                "ShowContextMenuCommand",
                typeof(ICommand),
                typeof(DataGridRowRightClickContextMenuBehavior),
                new PropertyMetadata(null, OnShowContextMenuCommandChanged));

        public static void SetShowContextMenuCommand(DependencyObject element, ICommand value)
        {
            element.SetValue(ShowContextMenuCommandProperty, value);
        }

        public static ICommand GetShowContextMenuCommand(DependencyObject element)
        {
            return (ICommand)element.GetValue(ShowContextMenuCommandProperty);
        }

        private static void OnShowContextMenuCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGridRow row)
            {

                if (e.NewValue != null)
                {
                    row.MouseRightButtonUp += Row_MouseRightButtonUp;
                }
                else
                {
                    row.MouseRightButtonUp -= Row_MouseRightButtonUp;
                }
            }
        }

        private static void Row_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                // Creamos un ContextMenu de forma dinámica.
                var contextMenu = new ContextMenu();

                // Creamos un MenuItem con el header deseado.
                var menuItem = new MenuItem { Header = "Finalizar Orden" };

                // Obtenemos el comando asignado mediante la propiedad adjunta.
                ICommand command = GetShowContextMenuCommand(row);
                menuItem.Command = command;
                // Usamos el DataContext de la fila (la orden actual) como CommandParameter.
                menuItem.CommandParameter = row.DataContext;

                contextMenu.Items.Add(menuItem);

                // Asignamos la fila como elemento de colocación y mostramos el menú.
                contextMenu.PlacementTarget = row;
                contextMenu.IsOpen = true;

                e.Handled = true;
            }
        }
    }
}
