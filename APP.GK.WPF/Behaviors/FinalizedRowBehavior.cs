using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors; // Usa Microsoft.Xaml.Behaviors.Wpf
using APP.GK.WPF.ViewModels;

namespace APP.GK.WPF.Behaviors
{
    public class FinalizedRowBehavior : Behavior<DataGridRow>
    {
        private INotifyCollectionChanged _finalizedCollection;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.DataContextChanged += AssociatedObject_DataContextChanged;
            SubscribeToFinalizedOrders();
            UpdateRowVisual();
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.DataContextChanged -= AssociatedObject_DataContextChanged;
            if (_finalizedCollection != null)
                _finalizedCollection.CollectionChanged -= FinalizedCollection_CollectionChanged;
        }

        private void AssociatedObject_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UpdateRowVisual();
        }

        private void SubscribeToFinalizedOrders()
        {
            // Buscamos el DataGrid y su DataContext (OrdersViewModel)
            DataGrid grid = FindAncestor<DataGrid>(AssociatedObject);
            if (grid != null && grid.DataContext is OrdersViewModel vm)
            {
                if (_finalizedCollection != null)
                    _finalizedCollection.CollectionChanged -= FinalizedCollection_CollectionChanged;
                _finalizedCollection = vm.FinalizedOrders as INotifyCollectionChanged;
                if (_finalizedCollection != null)
                    _finalizedCollection.CollectionChanged += FinalizedCollection_CollectionChanged;
            }
        }

        private void FinalizedCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateRowVisual();
        }

        private void UpdateRowVisual()
        {
            if (AssociatedObject.DataContext is APP.GK.WPF.Modelos.OrdenProduccion order)
            {
                DataGrid grid = FindAncestor<DataGrid>(AssociatedObject);
                if (grid != null && grid.DataContext is OrdersViewModel vm)
                {
                    if (vm.FinalizedIds.Contains(order.OP))
                    {
                        AssociatedObject.SetValue(Control.BackgroundProperty, Brushes.Gray);
                        AssociatedObject.SetValue(Control.ForegroundProperty, Brushes.White);

                        // Deshabilita efectos de hover y selección
                        AssociatedObject.IsHitTestVisible = false;
                        AssociatedObject.Focusable = false;
                    }
                    else
                    {
                        // Restaurar interactividad si la orden no está finalizada
                        AssociatedObject.ClearValue(Control.BackgroundProperty);
                        AssociatedObject.ClearValue(Control.ForegroundProperty);
                        AssociatedObject.IsHitTestVisible = true;
                        AssociatedObject.Focusable = true;
                    }
                }
            }
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T target)
                    return target;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
