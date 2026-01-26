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
            else if (msg == "GRANDES" || msg == "TXIKIAK" || msg == "PAREAK" || msg == "JOKOA")
            {
                await ManejarDecisionApuesta(msg);
            }
            else if (msg.StartsWith("RESUMEN:"))
            {
                string datos = msg.Replace("RESUMEN:", "");
                MostrarResumenRonda(datos);
            }
        }
        private async Task ManejarDecisionApuesta(string fase)
        {
            Dispatcher.Invoke(() => {
                OcultarTodosLosBotones();
                // Mostramos opciones de apuesta
                BtnPaso.Visibility = Visibility.Visible;
                BtnEnvido.Visibility = Visibility.Visible;
                PanelSubApuesta.Visibility = Visibility.Visible;

                Console.WriteLine($"Fase actual: {fase}");
            });

            // Creamos la pausa
            _decisionTaskSource = new TaskCompletionSource<string>();

            string respuesta = await _decisionTaskSource.Task;

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
            string comando = (valor == "hordago") ? "hordago" : $"{valor}";

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

        private void TestResumen_Click(object sender, RoutedEventArgs e) => MostrarResumenRonda("test");

        private async void MostrarResumenRonda(string datosRonda)
        {
            Dispatcher.Invoke(() => {
                TxtG_Nos.Text = "2"; TxtG_Ellos.Text = "0";
                TxtC_Nos.Text = "0"; TxtC_Ellos.Text = "1";
                TxtP_Nos.Text = "3"; TxtP_Ellos.Text = "0";
                TxtJ_Nos.Text = "0"; TxtJ_Ellos.Text = "2";
                TxtPt_Nos.Text = "1"; TxtPt_Ellos.Text = "0";
                TxtTotalNos.Text = "6"; TxtTotalEllos.Text = "3";
                OverlayResumen.Visibility = Visibility.Visible;
            });

            await Task.Delay(8000);

            Dispatcher.Invoke(() => {
                OverlayResumen.Visibility = Visibility.Collapsed;
            });
        }

        private void BtnEnvido_Click(object sender, RoutedEventArgs e)
        {
            PanelSubApuesta.Visibility = (PanelSubApuesta.Visibility == Visibility.Visible)
                ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}