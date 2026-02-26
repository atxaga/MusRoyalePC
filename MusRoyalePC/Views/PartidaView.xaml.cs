using MusRoyalePC.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Google.Cloud.Firestore;

namespace MusRoyalePC.Views
{
    public partial class PartidaView : UserControl
    {
        private MusClientService _netService;
        private List<int> _cartasSeleccionadas = new List<int>();
        private string[] _misCartasActuales = new string[4];

        private TaskCompletionSource<string>? _decisionTaskSource;

        // mapping Seat(0..3) -> posición UI
        private Dictionary<int, UiSeat> _seatToUi = new();

        // cache firebaseId -> (Name, AvatarUrl)
        private readonly Dictionary<string, (string Name, string AvatarUrl)> _userDataCache = new(StringComparer.Ordinal);

        private DispatcherTimer? _turnCountdownTimer;
        private DateTime _turnCountdownStart;
        private TimeSpan _turnCountdownDuration;
        private bool _turnCountdownFired;

        private UiSeat? _countdownSeat;

        private static readonly TimeSpan TurnStartDelay = TimeSpan.Zero;
        private static readonly TimeSpan RoundSummaryDelay = TimeSpan.FromSeconds(5);

        private bool _abandonSent;

        private readonly Dictionary<string, UiSeat> _firebaseToUiSeat = new(StringComparer.Ordinal);

        private string? _statsTargetUserId;

        private int _puntosEquipo1;
        private int _puntosEquipo2;

        // RONDA: en grande hasta TURN
        private const double RondaFontSizeNormal = 30d;
        private const double RondaFontSizeGrande = 54d;
        private string? _ultimaRondaRaw;
        private bool _rondaGrandePendiente;

        // LABURPENA
        private readonly List<(string Item, int Total, int Talde)> _laburpenaBuffer = new();

        // NUEVO: totales de puntuación en partida (para los dos equipos)
        private int _totalNos;
        private int _totalEllos;

        public PartidaView(MusClientService servicioConectado)
        {
            InitializeComponent();

            _netService = servicioConectado;

            _netService.OnCartasRecibidas += ActualizarMisCartas;
            _netService.OnMiTurno += ActivarControles;
            _netService.OnComandoRecibido += ProcesarMensajeServer;
            _netService.OnPuntosRecibidos += AlRecibirPuntos;
            _netService.OnAsientosCalculados += PintarAsientosAsync;

            LblNombreYo.Text = "(Tú)";

            LblInfoRonda.Text = "DESKARTEAK";
            LblInfoRonda.FontSize = RondaFontSizeNormal;

            // Estado inicial
            SetQuieroVisible(false);
            ResetTotalesPartida();
        }

        private static bool EsAmaieraRondaRaw(string? rondaRaw)
            => string.Equals(rondaRaw?.Trim(), "AMAIERA", StringComparison.OrdinalIgnoreCase);

        private void MostrarRondaGrandeDesdeServer(string texto)
        {
            Dispatcher.Invoke(() =>
            {
                // cuando llega la ronda, ocultar la info de txanda/turno
                if (TurnInfoPanel != null)
                    TurnInfoPanel.Visibility = Visibility.Collapsed;

                // Mostrar tal cual lo manda el servidor
                LblInfoRonda.Text = texto;
                LblInfoRonda.FontSize = RondaFontSizeGrande;
            });
        }

        private void RestaurarRondaNormalSiHaceFalta()
        {
            if (!_rondaGrandePendiente)
                return;

            _rondaGrandePendiente = false;

            // literal, sin normalizar
            string textoNormal = string.IsNullOrWhiteSpace(_ultimaRondaRaw) ? "DESKARTEAK" : _ultimaRondaRaw;

            Dispatcher.Invoke(() =>
            {
                // Mostrar tal cual lo manda el servidor
                LblInfoRonda.Text = textoNormal;
                LblInfoRonda.FontSize = RondaFontSizeNormal;
            });
        }

        private void AddLaburpenaLine(string item, int total, int talde)
            => _laburpenaBuffer.Add((item, total, talde));

        private static string FormatPuntuacionMus(int puntos)
        {
            if (puntos < 0) puntos = 0;
            int grandes = puntos / 5;
            int chicas = puntos % 5;
            return $"{grandes}.{chicas}";
        }

        private static (int Grandes, int Chicas) SplitPuntuacionMus(int puntos)
        {
            if (puntos < 0) puntos = 0;
            return (puntos / 5, puntos % 5);
        }

        private static int ParsePuntuacionMus(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            text = text.Trim();

            // Acepta "3.1" o "16" (fallback)
            if (text.Contains('.'))
            {
                var parts = text.Split('.', StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out int g) && int.TryParse(parts[1], out int c))
                    return Math.Max(0, (g * 5) + c);
            }

            if (int.TryParse(text, out int raw))
                return Math.Max(0, raw);

            return 0;
        }

        private void SetMarcador(int puntosNos, int puntosEllos)
        {
            puntosNos = Math.Max(0, puntosNos);
            puntosEllos = Math.Max(0, puntosEllos);

            var (gNos, cNos) = SplitPuntuacionMus(puntosNos);
            var (gEll, cEll) = SplitPuntuacionMus(puntosEllos);

            try
            {
                var t = GetType();

                var etxG = t.GetField("LblPuntosEtxekoakG", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;
                var etxC = t.GetField("LblPuntosEtxekoakC", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;
                var kanG = t.GetField("LblPuntosKanpokoakG", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;
                var kanC = t.GetField("LblPuntosKanpokoakC", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;

                if (etxG != null && etxC != null && kanG != null && kanC != null)
                {
                    etxG.Text = gNos.ToString();
                    etxC.Text = cNos.ToString();
                    kanG.Text = gEll.ToString();
                    kanC.Text = cEll.ToString();
                }
            }
            catch
            {
                // ignore
            }
        }

        private (int Nosotros, int Ellos) GetPuntuacionPartidaActual()
        {
            try
            {
                var t = GetType();
                var etxG = t.GetField("LblPuntosEtxekoakG", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;
                var etxC = t.GetField("LblPuntosEtxekoakC", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;
                var kanG = t.GetField("LblPuntosKanpokoakG", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;
                var kanC = t.GetField("LblPuntosKanpokoakC", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(this) as TextBlock;

                int gNos = ParsePuntuacionMus(etxG?.Text);
                int cNos = ParsePuntuacionMus(etxC?.Text);
                int gEll = ParsePuntuacionMus(kanG?.Text);
                int cEll = ParsePuntuacionMus(kanC?.Text);

                int nosotros = Math.Max(0, (gNos * 5) + cNos);
                int ellos = Math.Max(0, (gEll * 5) + cEll);
                return (nosotros, ellos);
            }
            catch
            {
                return (0, 0);
            }
        }

        private async Task MostrarLaburpenaPopupAsync()
        {
            int miTalde = _netService?.MiIdTaldea ?? 0;
            var lines = _laburpenaBuffer.ToList();

            // Total de la ronda: usar el último totala recibido (según tu formato LABURPENA)
            int totalRonda = lines.Count > 0 ? lines[^1].Total : 0;

            var (pNos, pEll) = GetPuntuacionPartidaActual();

            Dispatcher.Invoke(() =>
            {
                // Arriba: total de la ronda (en grande)
                if (TxtLaburpenaTotala != null)
                {
                    TxtLaburpenaTotala.Text = $"TOTAL RONDA: {totalRonda}";
                    TxtLaburpenaTotala.Foreground = Brushes.White;
                }

                if (TxtLaburpenaItems != null)
                {
                    TxtLaburpenaItems.Inlines.Clear();

                    // Cada línea: NOMBRE_RONDA  +X (verde si es nuestro talde, rojo si otro)
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var l = lines[i];
                        bool esMio = l.Talde == miTalde;
                        Brush brush = esMio ? Brushes.LightGreen : Brushes.IndianRed;

                        TxtLaburpenaItems.Inlines.Add(new Run(l.Item) { Foreground = Brushes.White });
                        TxtLaburpenaItems.Inlines.Add(new Run("  ") { Foreground = Brushes.White });
                        TxtLaburpenaItems.Inlines.Add(new Run($"+{l.Total}") { Foreground = brush, FontWeight = FontWeights.Black });

                        if (i < lines.Count - 1)
                            TxtLaburpenaItems.Inlines.Add(new LineBreak());
                    }

                    // Abajo: total de la partida en formato mus (grandes.chicas - grandes.chicas)
                    TxtLaburpenaItems.Inlines.Add(new LineBreak());
                    TxtLaburpenaItems.Inlines.Add(new LineBreak());

                    string txtNos = FormatPuntuacionMus(pNos);
                    string txtEll = FormatPuntuacionMus(pEll);

                    TxtLaburpenaItems.Inlines.Add(new Run("PARTIDA: ") { Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                    TxtLaburpenaItems.Inlines.Add(new Run(txtNos) { Foreground = Brushes.LightGreen, FontWeight = FontWeights.Black });
                    TxtLaburpenaItems.Inlines.Add(new Run("  -  ") { Foreground = Brushes.White, FontWeight = FontWeights.Bold });
                    TxtLaburpenaItems.Inlines.Add(new Run(txtEll) { Foreground = Brushes.IndianRed, FontWeight = FontWeights.Black });
                }

                if (OverlayLaburpena != null)
                    OverlayLaburpena.Visibility = Visibility.Visible;
            });

            await Task.Delay(TimeSpan.FromSeconds(5));

            Dispatcher.Invoke(() =>
            {
                if (OverlayLaburpena != null)
                    OverlayLaburpena.Visibility = Visibility.Collapsed;
            });

            _laburpenaBuffer.Clear();
        }

        private enum UiSeat
        {
            Yo,
            Front,
            Left,
            Right
        }

        private async void PintarAsientosAsync(AsientosPartida a)
        {
            try
            {
                _seatToUi = new Dictionary<int, UiSeat>
                {
                    [a.Yo.Seat] = UiSeat.Yo,
                    [a.Front.Seat] = UiSeat.Front,
                    [a.Left.Seat] = UiSeat.Left,
                    [a.Right.Seat] = UiSeat.Right,
                };

                _firebaseToUiSeat.Clear();
                _firebaseToUiSeat[a.Yo.FirestoreId] = UiSeat.Yo;
                _firebaseToUiSeat[a.Front.FirestoreId] = UiSeat.Front;
                _firebaseToUiSeat[a.Left.FirestoreId] = UiSeat.Left;
                _firebaseToUiSeat[a.Right.FirestoreId] = UiSeat.Right;

                var yoTask = ResolveUserProfileAsync(a.Yo.FirestoreId);
                var frontTask = ResolveUserProfileAsync(a.Front.FirestoreId);
                var leftTask = ResolveUserProfileAsync(a.Left.FirestoreId);
                var rightTask = ResolveUserProfileAsync(a.Right.FirestoreId);

                await Task.WhenAll(yoTask, frontTask, leftTask, rightTask);

                var yo = yoTask.Result;
                var front = frontTask.Result;
                var left = leftTask.Result;
                var right = rightTask.Result;

                Dispatcher.Invoke(() =>
                {
                    LblNombreYo.Text = $"{yo.Name} (Tú)";
                    LblNombreFront.Text = front.Name;
                    LblNombreLeft.Text = left.Name;
                    LblNombreRight.Text = right.Name;

                    ApplyAvatarToEllipse(ImgAvatarYo, yo.AvatarUrl);
                    ApplyAvatarToEllipse(ImgAvatarFront, front.AvatarUrl);
                    ApplyAvatarToEllipse(ImgAvatarLeft, left.AvatarUrl);
                    ApplyAvatarToEllipse(ImgAvatarRight, right.AvatarUrl);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PartidaView] Error PintarAsientosAsync: {ex}");
            }
        }

        private void ApplyAvatarToEllipse(Ellipse ellipse, string? avatarFile)
        {
            try
            {
                string file = string.IsNullOrWhiteSpace(avatarFile) ? "avadef.png" : avatarFile;
                var uri = new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute);
                var img = new BitmapImage(uri);

                ellipse.Fill = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
            }
            catch
            {
                // ignore
            }
        }

        private async Task<(string Name, string AvatarUrl)> ResolveUserProfileAsync(string firebaseId)
        {
            if (string.IsNullOrWhiteSpace(firebaseId)) return ("?", null);

            if (_userDataCache.TryGetValue(firebaseId, out var cached))
                return cached;

            try
            {
                var db = FirestoreService.Instance.Db;
                DocumentSnapshot doc = await db.Collection("Users").Document(firebaseId).GetSnapshotAsync();

                if (!doc.Exists)
                {
                    var fb = (Name: $"{firebaseId}", AvatarUrl: (string)null);
                    _userDataCache[firebaseId] = fb;
                    return fb;
                }

                string name = firebaseId;
                string avatar = null;

                if (doc.ContainsField("username")) name = doc.GetValue<string>("username");
                else if (doc.ContainsField("Username")) name = doc.GetValue<string>("Username");

                if (doc.ContainsField("avatarActual")) avatar = doc.GetValue<string>("avatarActual");

                name = string.IsNullOrWhiteSpace(name) ? firebaseId : name;

                var result = (Name: name, AvatarUrl: avatar);
                _userDataCache[firebaseId] = result;
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PartidaView] Error ResolveUserProfileAsync({firebaseId}): {ex.Message}");
                return ($"{firebaseId}", null);
            }
        }

        private async void ProcesarMensajeServer(string msg)
        {
            if (string.Equals(msg?.Trim(), "END_GAME", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg?.Trim(), "ENDGAME", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(msg?.Trim(), "end_game", StringComparison.OrdinalIgnoreCase))
            {
                MostrarEndGame("Jokalari bat atera da.");
                return;
            }

            if (msg.StartsWith("LABURPENA:", StringComparison.Ordinal))
            {
                try
                {
                    string payload = msg.Substring("LABURPENA:".Length).Trim();
                    var parts = payload.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 3)
                    {
                        string jokua = parts[0];
                        int totala = int.TryParse(parts[1], out var t) ? t : 0;
                        int talde = int.TryParse(parts[2], out var tal) ? tal : 0;

                        AddLaburpenaLine(jokua, totala, talde);

                        // NUEVO: ir sumando el total de la partida en el marcador principal
                        AddPuntosTotalesDesdeLaburpena(jokua, totala, talde);
                    }
                }
                catch
                {
                    // ignore
                }
                return;
            }

            if (msg.StartsWith("RONDA:", StringComparison.Ordinal))
            {
                _ultimaRondaRaw = msg.Substring("RONDA:".Length).Trim();
                _rondaGrandePendiente = true;
                MostrarRondaGrandeDesdeServer(_ultimaRondaRaw);

                // Asegurar que el popup sale cuando el server mande RONDA:AMAIERA
                if (EsAmaieraRondaRaw(_ultimaRondaRaw) && _laburpenaBuffer.Count > 0)
                {
                    await MostrarLaburpenaPopupAsync();
                }

                return;
            }

            if (msg.StartsWith("ERABAKIA|", StringComparison.Ordinal))
            {
                var payload = msg.Substring("ERABAKIA|".Length);
                if (!string.IsNullOrWhiteSpace(payload))
                    ProcesarErabakia(payload);
                return;
            }

            if (msg.StartsWith("TURN;", StringComparison.Ordinal) || msg.StartsWith("TURN:", StringComparison.Ordinal))
            {
                RestaurarRondaNormalSiHaceFalta();

                try
                {
                    char sep = msg.Contains(';') ? ';' : ':';
                    var parts = msg.Split(sep);
                    string firebaseId = parts.Length >= 2 ? parts[1].Trim() : string.Empty;
                    int? serverSeat = null;
                    if (parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int seatVal))
                        serverSeat = seatVal;

                    if (serverSeat.HasValue && _seatToUi.TryGetValue(serverSeat.Value, out var uiSeatBySeat))
                    {
                        StartTurnCountdown(uiSeatBySeat);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(firebaseId) && _firebaseToUiSeat.TryGetValue(firebaseId, out var uiSeatByFirebase))
                    {
                        StartTurnCountdown(uiSeatByFirebase);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(firebaseId) && firebaseId == _netService.MiIdFirestore)
                    {
                        StartTurnCountdown(UiSeat.Yo);
                        return;
                    }

                    Debug.WriteLine($"[PartidaView] TURN sin mapear. Raw='{msg}', firebaseId='{firebaseId}', serverSeat='{serverSeat}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PartidaView] Error parse TURN: {ex.Message}. Raw='{msg}'");
                }

                return;
            }

            if (msg == "TURN")
            {
                RestaurarRondaNormalSiHaceFalta();
                return;
            }

            if (msg == "ALL_MUS")
            {
                Dispatcher.Invoke(() =>
                {
                    LblInfoRonda.FontSize = RondaFontSizeNormal;
                    LblInfoRonda.Text = "DESKARTEAK";

                    OcultarTodosLosBotones();
                    Button btnDescarte = new Button
                    {
                        Content = "DESCARTAR",
                        Style = (Style)this.Resources["RoundedButton"],
                        Background = Brushes.DarkGreen,
                        Width = 150,
                        Height = 55,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White
                    };
                    btnDescarte.Click += (s, e) => EnviarDescarte(btnDescarte);
                    PanelBotones.Children.Add(btnDescarte);
                });
                return;
            }

            if (msg == "GRANDES" || msg == "PEQUEÑAS" || msg == "PARES" || msg == "JUEGO" || msg == "PUNTO")
            {
                Dispatcher.Invoke(() =>
                {
                    LblInfoRonda.FontSize = RondaFontSizeNormal;
                    LblInfoRonda.Text = NormalizeFaseEuskera(msg);
                });

                await Task.Delay(RoundSummaryDelay);
                await ManejarDecisionApuesta(msg);
                return;
            }

            if (msg.StartsWith("PUNTOS|"))
            {
                string[] partes = msg.Split('|');
                if (partes.Length == 5)
                    ActualizarMarcadorSimple(partes[1], partes[2], partes[3], partes[4]);
            }
            else if (msg.StartsWith("ACCION:", StringComparison.Ordinal))
            {
                MostrarAccionJugador(msg);
            }
            else
            {
                if (msg.Contains(';'))
                {
                    var parts = msg.Split(';');
                    if (parts.Length >= 3 && int.TryParse(parts[1].Trim(), out _))
                        ProcesarErabakia(msg);
                }
            }
        }

        private void ProcesarErabakia(string raw)
        {
            try
            {
                var parts = raw.Split(';', 3);
                if (parts.Length < 3) return;

                int serverId = int.TryParse(parts[1].Trim(), out var id) ? id : 0;
                string mensaje = parts[2].Trim();

                if (mensaje.EndsWith("PARES", StringComparison.OrdinalIgnoreCase))
                {
                    mensaje = mensaje.StartsWith("jokuaDaukat", StringComparison.OrdinalIgnoreCase)
                        ? "PAREAK DAUKAT"
                        : "PAREAK EZ DUT";
                }
                else if (mensaje.EndsWith("JUEGO", StringComparison.OrdinalIgnoreCase))
                {
                    mensaje = mensaje.StartsWith("jokuaDaukat", StringComparison.OrdinalIgnoreCase)
                        ? "JOKUA DAUKAT"
                        : "JOKUA EZ DUT";
                }

                if (int.TryParse(mensaje, out int apuesta))
                {
                    mensaje = apuesta == 2 ? "ENVIDO 2" : $"{apuesta} GEHIAGO";
                }

                if (_seatToUi.TryGetValue(serverId, out var uiSeat) && uiSeat != UiSeat.Yo)
                {
                    StartTurnCountdown(uiSeat);
                }

                MostrarPopupDecision(serverId, mensaje);
            }
            catch
            {
                // ignore
            }
        }

        private void ActualizarMisCartas(string[] cartas)
        {
            _misCartasActuales = cartas;
            Dispatcher.Invoke(() =>
            {
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
                catch (Exception ex) { Console.WriteLine("Error cargando imágenes: " + ex.Message); }
            });
        }

        private void Carta_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img && img.Tag != null)
            {
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
        }

        private void EnviarDescarte(Button btnOrigen)
        {
            string comando = _cartasSeleccionadas.Count == 0 ? "*"
                : string.Join("-", _cartasSeleccionadas.Select(i => _misCartasActuales[i]));

            _netService.EnviarComando(comando);
            OcultarTodosLosBotones();
        }

        private void BtnQuiero_Click(object sender, RoutedEventArgs e) => ResolverApuesta("quiero");

        private void BtnApuesta_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string valor = btn.Tag.ToString();
                ResolverApuesta(valor);
            }
        }

        private void BtnMas_Click(object sender, RoutedEventArgs e)
        {
            if (PanelApuestasAltas.Visibility == Visibility.Collapsed)
            {
                PanelApuestasAltas.Visibility = Visibility.Visible;
                BtnMas.Content = "➖";
            }
            else
            {
                PanelApuestasAltas.Visibility = Visibility.Collapsed;
                BtnMas.Content = "➕";
            }
        }

        private void MostrarAccionJugador(string msg)
        {
            try
            {
                var partes = msg.Split(':', 3);
                if (partes.Length < 3) return;

                if (!int.TryParse(partes[1], out int seat)) return;
                string texto = partes[2];

                MostrarPopupDecision(seat, texto);
            }
            catch
            {
                // ignore
            }
        }

        private void DesvincularEventos()
        {
            if (_netService != null)
            {
                _netService.OnCartasRecibidas -= ActualizarMisCartas;
                _netService.OnMiTurno -= ActivarControles;
                _netService.OnComandoRecibido -= ProcesarMensajeServer;
                _netService.OnPuntosRecibidos -= AlRecibirPuntos;
                _netService.OnAsientosCalculados -= PintarAsientosAsync;
            }
        }

        private void ActualizarMarcadorSimple(string e1, string e2, string z1, string z2)
        {
            Dispatcher.Invoke(() =>
            {
                int e1i = int.TryParse(e1, out var v1) ? v1 : 0;
                int e2i = int.TryParse(e2, out var v2) ? v2 : 0;
                int z1i = int.TryParse(z1, out var w1) ? w1 : 0;
                int z2i = int.TryParse(z2, out var w2) ? w2 : 0;

                int totalNos = (e1i * 5) + e2i;
                int totalEllos = (z1i * 5) + z2i;

                SetMarcador(totalNos, totalEllos);
            });
        }

        private void AlRecibirPuntos(int idTaldeaGanador, int puntosNuevos)
        {
            Dispatcher.Invoke(() =>
            {
                bool sonMios = (idTaldeaGanador == _netService.MiIdTaldea);

                var (nos, ellos) = GetPuntuacionPartidaActual();
                if (sonMios) nos += puntosNuevos; else ellos += puntosNuevos;

                SetMarcador(nos, ellos);

                if (nos >= 40 || ellos >= 40)
                {
                    string ganador = nos >= 40 ? "Etxekoak" : "Kanpokoak";
                    MostrarEndGame($"Partida amaituta! Irabazlea: {ganador}.");
                }
            });
        }

        private void MostrarEndGame(string texto)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    StopTurnCountdown();
                    OcultarTodosLosBotones();

                    if (TxtEndGameMsg != null)
                        TxtEndGameMsg.Text = string.IsNullOrWhiteSpace(texto) ? "Jokalari bat atera da." : texto;

                    if (OverlayEndGame != null)
                        OverlayEndGame.Visibility = Visibility.Visible;
                }
                catch
                {
                    // ignore
                }
            });
        }

        private void VolverAlHome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AbandonarPartida();

                var vm = Application.Current?.MainWindow?.DataContext as MainViewModel;
                vm?.Navegar("Home");
            }
            catch
            {
                // ignore
            }
        }

        private void BtnMus_Click(object sender, RoutedEventArgs e)
        {
            StopTurnCountdown();
            _netService.EnviarComando("mus");
            OcultarTodosLosBotones();
        }

        private void BtnPaso_Click(object sender, RoutedEventArgs e)
        {
            ResolverApuesta("paso");
        }

        private void EnviarAbandonoSiConectado()
        {
            if (_abandonSent) return;
            _abandonSent = true;

            try
            {
                if (_netService?.IsConnected == true)
                {
                    _netService.EnviarComando("ABANDONO");
                }
            }
            catch
            {
                // ignore
            }
        }

        private void AbandonarPartida()
        {
            try
            {
                StopTurnCountdown();
                EnviarAbandonoSiConectado();
                DesvincularEventos();
                _netService?.Desconectar();
            }
            catch
            {
                // ignore
            }
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            if (oldParent != null && VisualParent == null)
            {
                AbandonarPartida();
            }

            base.OnVisualParentChanged(oldParent);
        }

        public void Abandonar()
        {
            AbandonarPartida();
        }

        private Path? GetCountdownRing(UiSeat seat) => seat switch
        {
            UiSeat.Yo => AvatarCountdownRing,
            UiSeat.Front => AvatarCountdownRingFront,
            UiSeat.Left => AvatarCountdownRingLeft,
            UiSeat.Right => AvatarCountdownRingRight,
            _ => null
        };

        private static (double Cx, double Cy, double R) GetRingGeometryFor(UiSeat seat)
            => seat == UiSeat.Yo ? (35d, 35d, 30d) : (30d, 30d, 25d);

        private UiSeat SeatFromRing(Path ring)
        {
            if (ReferenceEquals(ring, AvatarCountdownRing)) return UiSeat.Yo;
            if (ReferenceEquals(ring, AvatarCountdownRingFront)) return UiSeat.Front;
            if (ReferenceEquals(ring, AvatarCountdownRingLeft)) return UiSeat.Left;
            if (ReferenceEquals(ring, AvatarCountdownRingRight)) return UiSeat.Right;
            return UiSeat.Yo;
        }

        private void UpdateCountdownRing(Path ring, double fraction)
        {
            fraction = Math.Clamp(fraction, 0, 1);

            if (fraction <= 0.001)
            {
                ring.Data = Geometry.Empty;
                return;
            }

            UiSeat seat = SeatFromRing(ring);
            var (cx, cy, r) = GetRingGeometryFor(seat);

            double startAngle = -90;
            double sweepAngle = 360 * fraction;
            double endAngle = startAngle + sweepAngle;

            Point start = PointOnCircle(cx, cy, r, startAngle);
            Point end = PointOnCircle(cx, cy, r, endAngle);

            bool isLargeArc = sweepAngle >= 180;

            var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(r, r),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc,
                RotationAngle = 0
            });

            var geo = new PathGeometry();
            geo.Figures.Add(fig);
            ring.Data = geo;
        }

        private async Task ManejarDecisionApuesta(string fase)
        {
            _decisionTaskSource?.TrySetCanceled();
            _decisionTaskSource = new TaskCompletionSource<string>();

            Dispatcher.Invoke(() =>
            {
                OcultarTodosLosBotones();

                BtnPaso.Visibility = Visibility.Visible;

                // QUIERO: por defecto invisible, solo se habilita si hay apuesta/envido.
                BtnQuiero.Visibility = Visibility.Collapsed;

                BtnEnvido.Visibility = Visibility.Visible;
                BtnMas.Visibility = Visibility.Visible;
                BtnMas.Content = "➕";
                PanelApuestasAltas.Visibility = Visibility.Collapsed;

                BtnEnvido.Content = "ENVIDO 2";
                if (BtnEnvido5 != null)
                    BtnEnvido5.Content = "5 GEHIAGO";
            });

            StartTurnCountdown(UiSeat.Yo);

            string respuesta = await _decisionTaskSource.Task;
            StopTurnCountdown();

            _netService.EnviarComando(respuesta);
            Dispatcher.Invoke(() => OcultarTodosLosBotones());
        }

        private void ResolverApuesta(string comando)
        {
            StopTurnCountdown();

            // Si el usuario ha lanzado un envido, a partir de ahí QUIERO debe estar visible
            // (porque ya hay apuesta activa).
            if (!string.IsNullOrWhiteSpace(comando))
            {
                bool esEnvido = comando.Equals("2", StringComparison.OrdinalIgnoreCase)
                                || comando.Equals("5", StringComparison.OrdinalIgnoreCase)
                                || comando.Equals("ordago", StringComparison.OrdinalIgnoreCase)
                                || comando.Contains("envido", StringComparison.OrdinalIgnoreCase);

                if (esEnvido)
                    SetQuieroVisible(true);
            }

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

        private void ActivarControles()
        {
            Dispatcher.Invoke(() =>
            {
                OcultarTodosLosBotones();
                BtnMus.Visibility = Visibility.Visible;
                BtnPaso.Visibility = Visibility.Visible;
            });

            StartTurnCountdown(UiSeat.Yo);
        }

        private void OcultarTodosLosBotones()
        {
            Dispatcher.Invoke(() =>
            {
                BtnMus.Visibility = Visibility.Collapsed;
                BtnPaso.Visibility = Visibility.Collapsed;
                BtnQuiero.Visibility = Visibility.Collapsed;
                BtnEnvido.Visibility = Visibility.Collapsed;
                BtnMas.Visibility = Visibility.Collapsed;

                if (PanelApuestasAltas != null)
                    PanelApuestasAltas.Visibility = Visibility.Collapsed;

                var descartes = PanelBotones.Children.OfType<Button>()
                    .Where(b => b.Content?.ToString() == "DESCARTAR").ToList();
                foreach (var d in descartes) PanelBotones.Children.Remove(d);

                StopTurnCountdown();
            });
        }

        private static Point PointOnCircle(double cx, double cy, double r, double angleDegrees)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point(cx + (r * Math.Cos(rad)), cy + (r * Math.Sin(rad)));
        }

        private void MostrarPopupDecision(int serverSeat, string texto)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_seatToUi.TryGetValue(serverSeat, out var uiSeat))
                    return;

                if (uiSeat == UiSeat.Yo)
                {
                    TxtMensajeYo.Text = texto;
                    BubbleYo.Visibility = Visibility.Visible;

                    var timerYo = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5000) };
                    timerYo.Tick += (s, e) =>
                    {
                        BubbleYo.Visibility = Visibility.Collapsed;
                        timerYo.Stop();
                    };
                    timerYo.Start();
                    return;
                }

                (Border bubble, TextBlock label) = uiSeat switch
                {
                    UiSeat.Front => (BubbleKidea, TxtMensajeKidea),
                    UiSeat.Left => (BubbleAurkari1, TxtMensajeAurkari1),
                    UiSeat.Right => (BubbleAurkari2, TxtMensajeAurkari2),
                    _ => (null, null)
                };

                if (bubble == null || label == null) return;

                label.Text = texto;
                bubble.Visibility = Visibility.Visible;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5000) };
                timer.Tick += (s, e) =>
                {
                    bubble.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            });
        }

        private void StartTurnCountdown(UiSeat? seatOverride = null)
        {
            Dispatcher.Invoke(() =>
            {
                StopTurnCountdown();

                _countdownSeat = seatOverride ?? UiSeat.Yo;

                UiSeat seatForThisTimer = _countdownSeat.Value;
                bool isMusPhaseForThisTimer = (seatForThisTimer == UiSeat.Yo) && BtnMus.Visibility == Visibility.Visible;

                _turnCountdownDuration = TimeSpan.FromSeconds(30);
                _turnCountdownStart = DateTime.UtcNow;
                _turnCountdownFired = false;

                UpdateTurnInfo(seatForThisTimer, secondsRemaining: 30, waitingDelay: false);
                TurnInfoPanel.Visibility = Visibility.Visible;

                foreach (var ring in new[] { AvatarCountdownRing, AvatarCountdownRingFront, AvatarCountdownRingLeft, AvatarCountdownRingRight })
                {
                    if (ring != null)
                    {
                        ring.Visibility = Visibility.Collapsed;
                        ring.Data = Geometry.Empty;
                    }
                }

                _turnCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
                _turnCountdownTimer.Tick += (_, __) =>
                {
                    var sinceStart = DateTime.UtcNow - _turnCountdownStart;

                    var elapsed = sinceStart; // TurnStartDelay es 0
                    double remainingFraction = Math.Max(0, 1.0 - (elapsed.TotalMilliseconds / _turnCountdownDuration.TotalMilliseconds));

                    var ring = GetCountdownRing(seatForThisTimer);
                    if (ring != null)
                    {
                        if (ring.Visibility != Visibility.Visible)
                        {
                            ring.Visibility = Visibility.Visible;
                            UpdateCountdownRing(ring, 1.0);
                        }
                        UpdateCountdownRing(ring, remainingFraction);
                    }

                    int secondsLeft = (int)Math.Ceiling(Math.Max(0, (_turnCountdownDuration - elapsed).TotalSeconds));
                    UpdateTurnInfo(seatForThisTimer, secondsRemaining: secondsLeft, waitingDelay: false);

                    if (!_turnCountdownFired && elapsed >= _turnCountdownDuration)
                    {
                        _turnCountdownFired = true;
                        StopTurnCountdown();

                        if (seatForThisTimer == UiSeat.Yo)
                        {
                            string cmd = isMusPhaseForThisTimer ? "mus" : "paso";
                            ResolverApuesta(cmd);
                        }
                    }
                };

                _turnCountdownTimer.Start();
            });
        }

        private void StopTurnCountdown()
        {
            Dispatcher.Invoke(() =>
            {
                if (_turnCountdownTimer != null)
                {
                    _turnCountdownTimer.Stop();
                    _turnCountdownTimer = null;
                }

                foreach (var ring in new[] { AvatarCountdownRing, AvatarCountdownRingFront, AvatarCountdownRingLeft, AvatarCountdownRingRight })
                {
                    if (ring != null)
                    {
                        ring.Visibility = Visibility.Collapsed;
                        ring.Data = Geometry.Empty;
                    }
                }

                if (TurnInfoPanel != null)
                    TurnInfoPanel.Visibility = Visibility.Collapsed;

                _countdownSeat = null;
            });
        }

        private void UpdateTurnInfo(UiSeat seat, int? secondsRemaining, bool waitingDelay)
        {
            string nombre = seat switch
            {
                UiSeat.Yo => LblNombreYo?.Text ?? "(Tú)",
                UiSeat.Front => LblNombreFront?.Text ?? "-",
                UiSeat.Left => LblNombreLeft?.Text ?? "-",
                UiSeat.Right => LblNombreRight?.Text ?? "-",
                _ => "-"
            };

            if (!string.IsNullOrWhiteSpace(nombre) && nombre.Contains("(Tú)", StringComparison.OrdinalIgnoreCase))
                nombre = nombre.Replace("(Tú)", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();

            LblTurnoJugador.Text = string.IsNullOrWhiteSpace(nombre) ? "-" : nombre;

            if (secondsRemaining is null)
            {
                LblTurnoTiempo.Text = string.Empty;
            }
            else
            {
                LblTurnoTiempo.Text = $"{secondsRemaining}s";
            }
        }

        private static bool FaseConApuesta(string fase)
        {
            if (string.IsNullOrWhiteSpace(fase)) return false;
            return fase.Equals("GRANDES", StringComparison.OrdinalIgnoreCase)
                || fase.Equals("PEQUEÑAS", StringComparison.OrdinalIgnoreCase)
                || fase.Equals("PARES", StringComparison.OrdinalIgnoreCase)
                || fase.Equals("JUEGO", StringComparison.OrdinalIgnoreCase)
                || fase.Equals("PUNTO", StringComparison.OrdinalIgnoreCase);
        }

        private async void PlayerAvatar_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is not FrameworkElement fe) return;

                string? clickedFirestoreId = fe.Name switch
                {
                    nameof(ImgAvatarYo) => _netService?.MiIdFirestore,
                    nameof(ImgAvatarFront) => GetFirestoreIdForSeat(UiSeat.Front),
                    nameof(ImgAvatarLeft) => GetFirestoreIdForSeat(UiSeat.Left),
                    nameof(ImgAvatarRight) => GetFirestoreIdForSeat(UiSeat.Right),
                    _ => null
                };

                if (string.IsNullOrWhiteSpace(clickedFirestoreId))
                    return;

                string currentUserId = FirestoreService.Instance.CurrentUserId;
                if (string.IsNullOrWhiteSpace(currentUserId))
                    currentUserId = Properties.Settings.Default.savedId;

                var svc = new FriendService(FirestoreService.Instance.Db);
                var stats = await svc.GetUserStatsAsync(clickedFirestoreId, currentUserId);
                if (stats == null) return;

                _statsTargetUserId = stats.Id;

                Dispatcher.Invoke(() =>
                {
                    TxtStatsUsername.Text = stats.Username;
                    TxtStatsEmail.Text = stats.Email;
                    TxtStatsPartidak.Text = stats.Partidak.ToString();
                    TxtStatsIrabaziak.Text = stats.PartidaIrabaziak.ToString();

                    if (stats.IsFriend)
                    {
                        TxtStatsFriendState.Text = "LAGUNA";
                        TxtStatsFriendState.Foreground = Brushes.LightGreen;
                        BtnSendFriendRequest.IsEnabled = false;
                        BtnSendFriendRequest.Visibility = Visibility.Collapsed;
                    }
                    else if (stats.RequestAlreadySent)
                    {
                        TxtStatsFriendState.Text = "ESKAERA BIDALITA";
                        TxtStatsFriendState.Foreground = Brushes.Gold;
                        BtnSendFriendRequest.IsEnabled = false;
                        BtnSendFriendRequest.Visibility = Visibility.Visible;
                    }
                    else if (stats.RequestAlreadyReceived)
                    {
                        TxtStatsFriendState.Text = "ESKAERA JASOTA";
                        TxtStatsFriendState.Foreground = Brushes.Gold;
                        BtnSendFriendRequest.IsEnabled = false;
                        BtnSendFriendRequest.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        TxtStatsFriendState.Text = string.Empty;
                        BtnSendFriendRequest.IsEnabled = true;
                        BtnSendFriendRequest.Visibility = Visibility.Visible;
                    }

                    OverlayPlayerStats.Visibility = Visibility.Visible;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PartidaView] PlayerAvatar_Click error: {ex.Message}");
            }
        }

        private string? GetFirestoreIdForSeat(UiSeat seat)
        {
            foreach (var kv in _firebaseToUiSeat)
            {
                if (kv.Value == seat)
                    return kv.Key;
            }
            return null;
        }

        private async void BtnSendFriendRequest_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_statsTargetUserId)) return;

                string currentUserId = FirestoreService.Instance.CurrentUserId;
                if (string.IsNullOrWhiteSpace(currentUserId))
                    currentUserId = Properties.Settings.Default.savedId;

                var svc = new FriendService(FirestoreService.Instance.Db);
                await svc.SendFriendRequestAsync(currentUserId, _statsTargetUserId);

                Dispatcher.Invoke(() =>
                {
                    TxtStatsFriendState.Text = "ESKAERA BIDALITA";
                    TxtStatsFriendState.Foreground = Brushes.Gold;
                    BtnSendFriendRequest.IsEnabled = false;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errorea: " + ex.Message);
            }
        }

        private void BtnClosePlayerStats_Click(object sender, RoutedEventArgs e)
        {
            OverlayPlayerStats.Visibility = Visibility.Collapsed;
            _statsTargetUserId = null;
        }

        private static string NormalizeFaseEuskera(string fase)
        {
            if (string.IsNullOrWhiteSpace(fase)) return "DESKARTEAK";

            string f = fase.Trim().ToUpperInvariant();

            return f switch
            {
                "GRANDES" or "GRANDIAK" => "GRANDIAK",
                "PEQUEÑAS" or "PEQUENAS" or "TXIKIAK" => "TXIKIAK",
                "PARES" or "PAREAK" => "PAREAK",
                "JUEGO" or "JOKUA" => "JOKUA",
                "PUNTO" or "PUNTUA" => "PUNTUA",
                "TURN" or "TURNO" or "TXANDA" => "TXANDA",
                "ALL_MUS" or "MUS" or "DESKARTEAK" => "DESKARTEAK",
                _ => f
            };
        }

        // Handler de prueba (referenciado en XAML). Mantener por compatibilidad.
        private void TestResumen_Click(object sender, RoutedEventArgs e)
        {
            // Mostrar un ejemplo rápido de LABURPENA para validar el overlay.
            AddLaburpenaLine("GRANDIAK", 2, _netService?.MiIdTaldea ?? 1);
            AddLaburpenaLine("PAREAK", 3, (_netService?.MiIdTaldea ?? 1) == 1 ? 2 : 1);
            _ = MostrarLaburpenaPopupAsync();
        }

        private void ResetTotalesPartida()
        {
            _totalNos = 0;
            _totalEllos = 0;
            SetMarcador(_totalNos, _totalEllos);
        }

        private void AddPuntosTotalesDesdeLaburpena(string item, int puntos, int talde)
        {
            // LABURPENA viene con el taldea que recibe los puntos.
            if (puntos < 0) puntos = 0;

            if (talde == (_netService?.MiIdTaldea ?? -1))
                _totalNos += puntos;
            else
                _totalEllos += puntos;

            SetMarcador(_totalNos, _totalEllos);
        }

        private void SetQuieroVisible(bool visible)
        {
            Dispatcher.Invoke(() =>
            {
                if (BtnQuiero != null)
                    BtnQuiero.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            });
        }
    }
}