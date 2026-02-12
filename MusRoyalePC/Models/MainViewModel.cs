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
                // IP de tu servidor (Asegúrate de que sea la correcta, antes pusiste una terminada en .35)
                string ipServer = "44.210.239.166";
                int puerto = 13000;

                switch (modo)
                {
                    case "PUBLICA":
                        string resPub = await _netService.ConectarYUnirse(ipServer, puerto, "PUBLICA");
                        if (resPub == "OK")
                        {
                            Navegar("Partida");
                        }
                        else MessageBox.Show("Ezin izan da partidan sartu.");
                        break;

                    case "CREAR_PRIVADA":
                        // 1. ConectarYUnirse ya se encarga de todo (Conectar -> Enviar ID -> Recibir Código -> Iniciar Escucha)
                        string codigoNuevo = await _netService.ConectarYUnirse(ipServer, puerto, "CREAR_PRIVADA");

                        if (codigoNuevo != "ERROR" && codigoNuevo.Length == 4)
                        {
                            CodigoAIntroducir = codigoNuevo;

                            // Navegamos. Como 'ConectarYUnirse' ya inició la escucha internamente,
                            // no hay que tocar el socket de nuevo.
                            App.Current.Dispatcher.Invoke(() => Navegar("Partida"));

                            MessageBox.Show($"Partida sortuta! Kodea: {codigoNuevo}");
                        }
                        else { MessageBox.Show("Errorea."); }
                        break;

                    case "UNIRSE_PRIVADA":
                        string res = await _netService.ConectarYUnirse(ipServer, puerto, "UNIRSE_PRIVADA", CodigoAIntroducir);
                        if (res == "OK")
                        {
                            // Usamos el Dispatcher para asegurar que la vista cambie sin errores
                            App.Current.Dispatcher.Invoke(() => Navegar("Partida"));
                        }
                        else
                        {
                            MessageBox.Show("Ezin izan da sartu.");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // Esto atrapará el timeout o errores de red sin colgar la App
                MessageBox.Show($"Errorea: {ex.Message}");
            }
        }

        public void Navegar(string destino)
        {
            CurrentPageName = destino;
            switch (destino)
            {
                case "Home": CurrentView = null; break;
                case "Partida": CurrentView = new PartidaView(_netService); break;
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