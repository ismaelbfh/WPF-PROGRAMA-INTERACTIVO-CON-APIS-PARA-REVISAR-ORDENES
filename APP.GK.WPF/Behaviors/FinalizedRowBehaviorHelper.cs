using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors; // Usa Microsoft.Xaml.Behaviors.Wpf (asegúrate de instalarlo)
using APP.GK.WPF.Modelos;

namespace APP.GK.WPF.Behaviors
{
    public static class FinalizedRowBehaviorHelper
    {
        public static readonly DependencyProperty AttachFinalizedBehaviorProperty =
            DependencyProperty.RegisterAttached(
                "AttachFinalizedBehavior",
                typeof(bool),
                typeof(FinalizedRowBehaviorHelper),
                new PropertyMetadata(false, OnAttachFinalizedBehaviorChanged));

        public static bool GetAttachFinalizedBehavior(DependencyObject obj)
        {
            return (bool)obj.GetValue(AttachFinalizedBehaviorProperty);
        }

        public static void SetAttachFinalizedBehavior(DependencyObject obj, bool value)
        {
            obj.SetValue(AttachFinalizedBehaviorProperty, value);
        }

        private static void OnAttachFinalizedBehaviorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement fe)
            {
                bool attach = (bool)e.NewValue;
                if (attach)
                {
                    // Usando Microsoft.Xaml.Behaviors: obtenemos la colección de behaviors del elemento
                    var behaviors = Interaction.GetBehaviors(fe);
                    // Solo agregamos si aún no existe
                    if (!behaviors.Any(b => b is FinalizedRowBehavior))
                    {
                        behaviors.Add(new FinalizedRowBehavior());
                    }
                }
            }
        }
    }
}
