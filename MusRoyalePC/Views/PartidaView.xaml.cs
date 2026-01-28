using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MusRoyalePC.Services;

namespace MusRoyalePC.Views
{
    public partial class PartidaView : UserControl
    {
        private MusClientService _netService;
        private string ip = "34.233.112.247";
        private int port = 13000;
        private List<int> _cartasSeleccionadas = new List<int>();
        private string[] _misCartasActuales = new string[4];

        private TaskCompletionSource<string>? _decisionTaskSource;

        public PartidaView()
        {
            InitializeComponent();
            _netService = new MusClientService();

            _netService.OnCartasRecibidas += ActualizarMisCartas;
            _netService.OnMiTurno += ActivarControles;
            _netService.OnComandoRecibido += ProcesarMensajeServer;

            Conectar();
        }

        private async void Conectar() => await _netService.Conectar(ip, port);

        // --- LÓGICA DE MENSAJES DEL SERVIDOR ---
        private async void ProcesarMensajeServer(string msg)
        {
            if (msg == "ALL_MUS")
            {
                Dispatcher.Invoke(() =>
                {
                    OcultarTodosLosBotones();
                    Button btnDescarte = new Button
                    {
                        Content = "DESCARTAR",
                        Style = (Style)this.Resources["RoundedButton"],
                        Background = System.Windows.Media.Brushes.DarkGreen,
                        Width = 150,
                        Height = 55
                    };
                    btnDescarte.Click += (s, e) => EnviarDescarte(btnDescarte);
                    PanelBotones.Children.Add(btnDescarte);
                });
            }
            else if (msg == "GRANDES" || msg == "PEQUEÑAS" || msg == "PARES" || msg == "JUEGO")
            {
                await ManejarDecisionApuesta(msg);
            }
            else if (msg.StartsWith("PUNTOS|"))
            {
                string[] partes = msg.Split('|'); // [0]=PUNTOS, [1]=Esk1, [2]=Esk2, [3]=Ezk1, [4]=Ezk2
                if (partes.Length == 5)
                {
                    ActualizarMarcadorReal(partes[1], partes[2], partes[3], partes[4]);
                }
            }
        }
        private async Task ManejarDecisionApuesta(string fase)
        {
            // 1. Forzar limpieza de cualquier tarea anterior
            _decisionTaskSource?.TrySetCanceled();
            _decisionTaskSource = new TaskCompletionSource<string>();

            Dispatcher.Invoke(() => {
                OcultarTodosLosBotones();

                // Activar botones necesarios
                BtnPaso.Visibility = Visibility.Visible;
                BtnEnvido.Visibility = Visibility.Visible;
                BtnQuiero.Visibility = Visibility.Visible;
                PanelSubApuesta.Visibility = Visibility.Collapsed;

                Console.WriteLine($"--- UI Activada para fase: {fase} ---");
            });

            // 2. Esperar respuesta del usuario
            string respuesta = await _decisionTaskSource.Task;

            // 3. Enviar y limpiar inmediatamente
            _netService.EnviarComando(respuesta);

            Dispatcher.Invoke(() => OcultarTodosLosBotones());
        }

        private void BtnPaso_Click(object sender, RoutedEventArgs e)
        {
            if (_decisionTaskSource != null && !_decisionTaskSource.Task.IsCompleted)
            {
                _decisionTaskSource.TrySetResult("paso");
            }
            else
            {
                _netService.EnviarComando("paso");
                OcultarTodosLosBotones();
            }
        }

        private void BtnApuesta_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            string valor = btn.Tag.ToString();
            string comando = (valor == "ordago") ? "ordago" : $"{valor}";

            if (_decisionTaskSource != null && !_decisionTaskSource.Task.IsCompleted)
            {
                _decisionTaskSource.TrySetResult(comando);
            }
            else
            {
                _netService.EnviarComando(comando);
                OcultarTodosLosBotones();
            }
        }

        private void BtnMus_Click(object sender, RoutedEventArgs e)
        {
            _netService.EnviarComando("mus");
            OcultarTodosLosBotones();
        }

        private void BtnQuiero_Click(object sender, RoutedEventArgs e)
        {
            // 1. Verificamos si hay una tarea de decisión esperando
            if (_decisionTaskSource != null && !_decisionTaskSource.Task.IsCompleted)
            {
                // 2. "Despertamos" al método ManejarDecisionApuesta enviando "quiero"
                _decisionTaskSource.TrySetResult("quiero");
            }
            else
            {
                // Caso de seguridad: si no hay tarea, lo enviamos directo
                _netService.EnviarComando("quiero");
                OcultarTodosLosBotones();
            }
        }

        private void ActualizarMisCartas(string[] cartas)
        {
            _misCartasActuales = cartas;
            Dispatcher.Invoke(() => {
                try
                {
                    ImgCarta1.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Cartas/{cartas[0]}.png"));
                    ImgCarta2.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Cartas/{cartas[1]}.png"));
                    ImgCarta3.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Cartas/{cartas[2]}.png"));
                    ImgCarta4.Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/Cartas/{cartas[3]}.png"));

                    _cartasSeleccionadas.Clear();
                    ImgCarta1.Opacity = 1.0; ImgCarta2.Opacity = 1.0;
                    ImgCarta3.Opacity = 1.0; ImgCarta4.Opacity = 1.0;
                }
                catch (Exception ex) { Console.WriteLine("Error imágenes: " + ex.Message); }
            });
        }

        private void ActivarControles()
        {
            Dispatcher.Invoke(() => {
                BtnMus.Visibility = Visibility.Visible;
                BtnPaso.Visibility = Visibility.Visible;
                BtnEnvido.Visibility = Visibility.Visible;
                BtnQuiero.Visibility = Visibility.Visible;
                PanelSubApuesta.Visibility = Visibility.Collapsed;
            });
        }

        private void OcultarTodosLosBotones()
        {
            Dispatcher.Invoke(() => {
                BtnMus.Visibility = Visibility.Collapsed;
                BtnPaso.Visibility = Visibility.Collapsed;
                BtnEnvido.Visibility = Visibility.Collapsed;
                BtnQuiero.Visibility = Visibility.Collapsed;
                PanelSubApuesta.Visibility = Visibility.Collapsed;

                var descartes = PanelBotones.Children.OfType<Button>()
                    .Where(b => b.Content.ToString() == "DESCARTAR").ToList();
                foreach (var d in descartes) PanelBotones.Children.Remove(d);
            });
        }

        private void Carta_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var img = sender as Image;
            int index = int.Parse(img.Tag.ToString());

            if (_cartasSeleccionadas.Contains(index))
            {
                _cartasSeleccionadas.Remove(index);
                img.Opacity = 1.0;
            }
            else
            {
                _cartasSeleccionadas.Add(index);
                img.Opacity = 0.5;
            }
        }

        private void EnviarDescarte(Button btnOrigen)
        {
            string comando = _cartasSeleccionadas.Count == 0 ? "*"
                : string.Join("-", _cartasSeleccionadas.Select(i => _misCartasActuales[i]));

            _netService.EnviarComando(comando);
            OcultarTodosLosBotones();
        }

        private void TestResumen_Click(object sender, RoutedEventArgs e) => ActualizarMarcadorReal("3", "9", "2", "4");

        private void ActualizarMarcadorReal(string e1, string e2, string z1, string z2)
        {
            Dispatcher.Invoke(() => {
                // Asumiendo que Eskuin (Derecha) somos NOSOTROS y Ezker (Izquierda) ellos
                TxtTotalNos.Text = (int.Parse(e1) * 5 + int.Parse(e2)).ToString();
                TxtTotalEllos.Text = (int.Parse(z1) * 5 + int.Parse(z2)).ToString();

                // Si quieres mostrar amarracos y piedras por separado en el resumen:
                TxtG_Nos.Text = e1;   // Amarracos nuestros
                TxtG_Ellos.Text = e2; // Piedras nuestras

                OverlayResumen.Visibility = Visibility.Visible;
            });

            // Se oculta solo tras 5 segundos para que deje ver el progreso
            Task.Delay(5000).ContinueWith(_ =>
                Dispatcher.Invoke(() => OverlayResumen.Visibility = Visibility.Collapsed));
        }

        private void BtnEnvido_Click(object sender, RoutedEventArgs e)
        {
            PanelSubApuesta.Visibility = (PanelSubApuesta.Visibility == Visibility.Visible)
                ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}