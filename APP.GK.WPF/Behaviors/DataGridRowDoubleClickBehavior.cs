using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace APP.GK.WPF.Behaviors
{
    public static class DataGridRowDoubleClickBehavior
    {
        public static readonly DependencyProperty DoubleClickCommandProperty =
            DependencyProperty.RegisterAttached(
                "DoubleClickCommand",
                typeof(ICommand),
                typeof(DataGridRowDoubleClickBehavior),
                new PropertyMetadata(null, OnDoubleClickCommandChanged));

        public static void SetDoubleClickCommand(DependencyObject element, ICommand value)
        {
            element.SetValue(DoubleClickCommandProperty, value);
        }

        public static ICommand GetDoubleClickCommand(DependencyObject element)
        {
            return (ICommand)element.GetValue(DoubleClickCommandProperty);
        }

        private static void OnDoubleClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGridRow row)
            {
                if (e.NewValue != null)
                    row.MouseDoubleClick += Row_MouseDoubleClick;
                else
                    row.MouseDoubleClick -= Row_MouseDoubleClick;
            }
        }

        private static void Row_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                ICommand command = GetDoubleClickCommand(row);
                if (command != null && command.CanExecute(row.DataContext))
                    command.Execute(row.DataContext);
            }

        }
    }
}
