using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MusRoyalePC.Views;

namespace MusRoyalePC
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        private string _currentPageName = "Login";
        private string _userName;
        private string _balance;

        public string CurrentPageName
        {
            get => _currentPageName;
            set { _currentPageName = value; OnPropertyChanged("CurrentPageName"); }
        }
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged("UserName"); }
        }
        public string Balance
        {
            get => _balance;
            set { _balance = value; OnPropertyChanged("Balance"); }
        }

        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged("CurrentView"); }
        }

        // Comandos
        public ICommand NavigateCommand { get; }
        public ICommand PowerOffCommand { get; }
        public ICommand StartMatchCommand { get; }

        public MainViewModel()
        {
            // Vista inicial
            CurrentView = new LoginView();

            // Navegación general (Home, Lagunak, etc.)
            NavigateCommand = new RelayCommand<string>(Navegar);

            // Cerrar aplicación
            PowerOffCommand = new RelayCommand<object>(_ => Application.Current.Shutdown());

            // El botón "Jokatu" simplemente navega a la vista de partida
            StartMatchCommand = new RelayCommand<object>(_ => Navegar("Partida"));
        }

        public void Navegar(string destino)
        {
            CurrentPageName = destino;

            switch (destino)
            {
                case "Home":
                    CurrentView = null; // O la vista de tu menú principal
                    break;
                case "Partida":
                    // Esta es la vista donde pusimos todo el código del socket
                    CurrentView = new PartidaView();
                    break;
                case "Lagunak":
                    CurrentView = new LagunakView();
                    break;
                case "Register":
                    CurrentView = new RegisterView();
                    break;
                case "Login":
                    CurrentView = new LoginView();
                    break;
                case "PartidaAzkarra":
                    CurrentView = new PartidaAzkarraView();
                    break;
                case "Bikoteak":
                    CurrentView = new BikoteakView();
                    break;
                case "Pribatua":
                    CurrentView = new PribatuaView();
                    break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Clase RelayCommand genérica para simplificar
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter == null ? default : (T)parameter);
        public event EventHandler CanExecuteChanged;
    }
}