using Google.Cloud.Firestore;
using MusRoyalePC.Models;
using MusRoyalePC.Views;
using System;
using System.ComponentModel;
using System.Threading.Tasks; 
using System.Windows;
using System.Windows.Input;
using MusRoyalePC.Services;

namespace MusRoyalePC
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        private string _currentPageName = "Login";
        private string _userName;
        private string _balance;
        private readonly MusClientService _netService = new MusClientService();
        public MusClientService NetService => _netService;
        private string _codigoPartida = "----"; // El que generamos nosotros
        private string _codigoAIntroducir = "";  // El que escribimos para unirnos



        // Propiedades Públicas
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
        public string CodigoPartida
        {
            get => _codigoPartida;
            set { _codigoPartida = value; OnPropertyChanged("CodigoPartida"); }
        }

        public string CodigoAIntroducir
        {
            get => _codigoAIntroducir;
            set { _codigoAIntroducir = value; OnPropertyChanged("CodigoAIntroducir"); }
        }

        // Comandos
        public ICommand NavigateCommand { get; }
        public ICommand PowerOffCommand { get; }
        public ICommand StartMatchCommand { get; }
        public ICommand SalirPartidaCommand { get; } // Agregado aquí

        public MainViewModel()
        {
            var db = Services.FirestoreService.Instance.Db;
            string currentUserId = Properties.Settings.Default.savedId;

            if (currentUserId != "")
            {
                var userDoc = db.Collection("Users").Document(currentUserId).GetSnapshotAsync().Result;
                if (userDoc.Exists)
                {
                    UserName = userDoc.GetValue<string>("username");
                    var dinero = userDoc.ContainsField("dinero") ? userDoc.GetValue<object>("dinero").ToString() : "0";
                    Balance = dinero;
                }
                CurrentPageName = "Home";
            }
            else
            {
                CurrentView = new LoginView();
            }

            _netService.OnError += (ex) => {
                MessageBox.Show($"Error de red: {ex.Message}");
            };

            // --- INICIALIZACIÓN DE COMANDOS ---

            NavigateCommand = new RelayCommand<string>(Navegar);

            PowerOffCommand = new RelayCommand<object>(_ => {
                Properties.Settings.Default.savedId = string.Empty;
                Properties.Settings.Default.Save();
                Application.Current.Shutdown();
            });

            // Cambia la inicialización en el constructor
            StartMatchCommand = new RelayCommand<string>(async (modo) => await EjecutarInicioPartida(modo));

           

            // Comando para salir de la partida (DENTRO DEL CONSTRUCTOR)
            SalirPartidaCommand = new RelayCommand<object>(async (obj) => {
                // 1. Avisar al servidor
                // Si tienes un servicio de red, llámalo aquí, ej:
                // _netService.EnviarComando("QUIT"); 

                await Task.Delay(100);

                // 2. Volver al Home
                this.CurrentPageName = "Home";
                this.CurrentView = null;
            });
        }

        public async Task EjecutarInicioPartida(string modo)
        {
            try
            {
                switch (modo)
                {
                    case "PUBLICA":
                        // Conectamos y vamos directo a la mesa a esperar rivales
                        string resPub = await _netService.ConectarYUnirse("34.233.112.247", 13000, "PUBLICA");
                        if (resPub == "OK") Navegar("Partida");
                        else MessageBox.Show("Ezin izan da partidan sartu.");
                        break;

                    case "CREAR_PRIVADA":
                        // Solo pedimos el código, nos quedamos en la vista actual para mostrarlo
                        string codigoNuevo = await _netService.ConectarYUnirse("34.233.112.247", 13000, "CREAR_PRIVADA");
                        if (codigoNuevo.Length == 4)
                        {
                            CodigoAIntroducir = codigoNuevo; // Se muestra en el TextBox amarillo
                        }
                        break;

                    case "UNIRSE_PRIVADA":
                        // Validamos que haya algo escrito y enviamos
                        if (string.IsNullOrEmpty(CodigoAIntroducir)) return;

                        string resPriv = await _netService.ConectarYUnirse("34.233.112.247", 13000, "UNIRSE_PRIVADA", CodigoAIntroducir);
                        if (resPriv == "OK") Navegar("Partida");
                        else MessageBox.Show("Kode okerra.");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errorea: {ex.Message}");
            }
        }

        public void Navegar(string destino)
        {
            CurrentPageName = destino;
            switch (destino)
            {
                case "Home": CurrentView = null; break;
                case "Partida": CurrentView = new PartidaView(); break;
                case "Lagunak": CurrentView = new LagunakView(); break;
                case "Perfila": CurrentView = new PerfilaView(); break;
                case "Chat": CurrentView = new ChatView(); break;
                case "Register": CurrentView = new RegisterView(); break;
                case "Login": CurrentView = new LoginView(); break;
                case "PartidaAzkarra": CurrentView = new PartidaAzkarraView(); break;
                case "Bikoteak": CurrentView = new BikoteakView(); break;
                case "Pribatua": CurrentView = new PribatuaView(); break;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter == null ? default : (T)parameter);
        public event EventHandler CanExecuteChanged;
    }
}