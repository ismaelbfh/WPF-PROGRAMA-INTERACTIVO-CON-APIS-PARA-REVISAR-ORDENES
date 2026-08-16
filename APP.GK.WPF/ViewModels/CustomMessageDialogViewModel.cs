using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using APP.GK.WPF.Helpers;

namespace APP.GK.WPF.ViewModels
{
    public class CustomMessageDialogViewModel : BaseViewModel
    {
        public string Caption { get; set; }
        public string Message { get; set; }
        public ImageSource Icon { get; set; }
        public ObservableCollection<DialogButtonViewModel> Buttons { get; set; }

        // Evento para solicitar el cierre de la ventana
        public event Action RequestClose;

        // Comando para el botón de cerrar (la X)
        public ICommand CloseCommand { get; }

        public CustomMessageDialogViewModel(string pCaption, string pMessage, MessageBoxButton pButtons, MessageBoxImage pIcon, Action<MessageBoxResult> pCloseCallback)
        {
            Caption = pCaption;
            Message = pMessage;
            Icon = GetIcon(pIcon);
            Buttons = new ObservableCollection<DialogButtonViewModel>();
            SetupButtons(pButtons, pCloseCallback);

            // Inicializa el comando CloseCommand que invoca el evento RequestClose
            CloseCommand = new RelayCommand(_ => RequestClose?.Invoke());
        }

        private void SetupButtons(MessageBoxButton pButtons, Action<MessageBoxResult> pCloseCallback)
        {
            switch (pButtons)
            {
                case MessageBoxButton.OK:
                    Buttons.Add(new DialogButtonViewModel("OK", new RelayCommand(_ => pCloseCallback(MessageBoxResult.OK))));
                    break;
                case MessageBoxButton.OKCancel:
                    Buttons.Add(new DialogButtonViewModel("OK", new RelayCommand(_ => pCloseCallback(MessageBoxResult.OK))));
                    Buttons.Add(new DialogButtonViewModel("Cancelar", new RelayCommand(_ => pCloseCallback(MessageBoxResult.Cancel))));
                    break;
                case MessageBoxButton.YesNo:
                    Buttons.Add(new DialogButtonViewModel("Sí", new RelayCommand(_ => pCloseCallback(MessageBoxResult.Yes))));
                    Buttons.Add(new DialogButtonViewModel("No", new RelayCommand(_ => pCloseCallback(MessageBoxResult.No))));
                    break;
                case MessageBoxButton.YesNoCancel:
                    Buttons.Add(new DialogButtonViewModel("Sí", new RelayCommand(_ => pCloseCallback(MessageBoxResult.Yes))));
                    Buttons.Add(new DialogButtonViewModel("No", new RelayCommand(_ => pCloseCallback(MessageBoxResult.No))));
                    Buttons.Add(new DialogButtonViewModel("Cancelar", new RelayCommand(_ => pCloseCallback(MessageBoxResult.Cancel))));
                    break;
                default:
                    Buttons.Add(new DialogButtonViewModel("OK", new RelayCommand(_ => pCloseCallback(MessageBoxResult.OK))));
                    break;
            }
        }

        private ImageSource GetIcon(MessageBoxImage pIcon)
        {
            // Asegúrate de que los recursos existan y la ruta sea correcta
            switch (pIcon)
            {
                case MessageBoxImage.Error:
                    return new BitmapImage(new Uri("pack://application:,,,/APP.GK.WPF;component/Recursos/ErrorIcon.png", UriKind.Absolute));
                case MessageBoxImage.Warning:
                    return new BitmapImage(new Uri("pack://application:,,,/APP.GK.WPF;component/Recursos/WarningIcon.png", UriKind.Absolute));
                case MessageBoxImage.Information:
                    return new BitmapImage(new Uri("pack://application:,,,/APP.GK.WPF;component/Recursos/InformationIcon.png", UriKind.Absolute));
                case MessageBoxImage.Question:
                    return new BitmapImage(new Uri("pack://application:,,,/APP.GK.WPF;component/Recursos/QuestionIcon.png", UriKind.Absolute));
                default:
                    return null;
            }
        }
    }

    public class DialogButtonViewModel : BaseViewModel
    {
        public string Content { get; set; }
        public ICommand Command { get; set; }

        public DialogButtonViewModel(string pContent, ICommand pCommand)
        {
            Content = pContent;
            Command = pCommand;
        }
    }
}
