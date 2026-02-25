using MusRoyalePC.Models;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;

namespace MusRoyalePC.Services
{
    public class MusClientService : IDisposable
    {
        private TcpClient? _client;
        private StreamReader? _reader;
        private StreamWriter? _writer;
        private CancellationTokenSource? _listenCts;
        private Task? _listenTask;

        public event Action<string[]>? OnCartasRecibidas;
        public event Action<int, int> OnPuntosRecibidos;
        public event Action? OnMiTurno;
        public event Action<string>? OnComandoRecibido;
        public event Action<Exception>? OnError;
        public event Action? OnDisconnected;

        // NUEVO: evento para que la UI coloque a cada uno en su sitio (arriba/izq/dcha)
        public event Action<AsientosPartida>? OnAsientosCalculados;

        // Datos PROPIOS (rellenados antes de conectar o al recibir INFO)
        public string MiIdFirestore { get; set; }  // <-- IMPORTANTE: Asignar esto en el Login
        public int MiIdTaldea { get; private set; }
        public int MiNumeroJugador { get; private set; }

        // Datos del COMPAÑERO
        public string IdCompanero { get; private set; }
        public int NumeroCompanero { get; private set; }

        // NUEVO: cache de jugadores de la partida
        private List<InfoJugadorPartida> _jugadores = new();

        public bool IsConnected => _client?.Connected == true;

        public async Task Conectar(string ip, int puerto, int timeoutMs = 8000, CancellationToken cancellationToken = default)
        {
            // 1. Limpieza agresiva antes de conectar
            Desconectar();

            try
            {
                _client = new TcpClient();

                // CONFIGURACIÓN CLAVE PARA EVITAR TIMEOUTS Y LAG
                _client.NoDelay = true; // Envío inmediato de paquetes (sin Nagle)
                _client.LingerState = new LingerOption(true, 0); // Hard Reset al cerrar (evita socket fantasma)
                _client.ReceiveTimeout = 0; // Infinito (evita cortes por inactividad de lectura)
                _client.SendTimeout = 10000;

                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                var connectTask = _client.ConnectAsync(ip, puerto);
                var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, linkedCts.Token));

                if (completed != connectTask)
                {
                    // Si entra aquí, es que el servidor no responde (probablemente por conexión fantasma previa)
                    throw new TimeoutException("El servidor no respondió a tiempo.");
                }

                await connectTask;

                var stream = _client.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Desconectar();
                OnError?.Invoke(ex);
                throw;
            }
        }

        private static void Trace(string message)
        {
            try
            {
                Debug.WriteLine(message);
                Console.WriteLine(message);
            }
            catch
            {
                // ignore
            }
        }

        private void ProcesarInfoInicial(string data)
        {
            Trace($"[CLIENTE] ProcesarInfoInicial: data='{data}'");

            // Formato real (observado):
            // "1KemlrTOxaiRnw3ubdjMI0" => taldea '1' + firestoreId + seat '0'
            // NOTA: firestoreId puede contener dígitos. Por eso el seat debe ser SOLO el último dígito.

            string[] tokens = data.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var jugadores = new List<InfoJugadorPartida>(4);

            foreach (string raw in tokens)
            {
                if (raw.Length < 3) continue;

                // 1) taldea (primer char)
                if (!int.TryParse(raw.AsSpan(0, 1), out int talde))
                    continue;

                // 2) seat (ÚLTIMO dígito)
                char last = raw[^1];
                if (!char.IsDigit(last))
                    continue;

                int seat = last - '0';

                // 3) firestoreId (entre taldea y seat)
                string firebaseId = raw.Substring(1, raw.Length - 2);
                if (string.IsNullOrWhiteSpace(firebaseId))
                    continue;

                jugadores.Add(new InfoJugadorPartida
                {
                    Talde = talde,
                    Seat = seat,
                    FirestoreId = firebaseId
                });
            }

            if (jugadores.Count != 4)
            {
                Trace($"[CLIENTE] INFO inválida. Esperaba 4 jugadores y recibí {jugadores.Count}. Data={data}");
                return;
            }

            _jugadores = jugadores;

            // 1) Encontrarme a mí
            var yo = _jugadores.FirstOrDefault(j => j.FirestoreId == MiIdFirestore);
            if (yo == null)
            {
                Trace($"[CLIENTE] No me encuentro en INFO. MiIdFirestore='{MiIdFirestore}'. Jugadores=[{string.Join(",", _jugadores.Select(x => x.FirestoreId))}] ");
                return;
            }

            MiIdTaldea = yo.Talde;
            MiNumeroJugador = yo.Seat;

            // 2) Encontrar compañero (mismo equipo, distinto id)
            var compa = _jugadores.FirstOrDefault(j => j.Talde == MiIdTaldea && j.FirestoreId != MiIdFirestore);
            if (compa != null)
            {
                IdCompanero = compa.FirestoreId;
                NumeroCompanero = compa.Seat;
            }

            // 3) Calcular asientos relativos.
            if (!_jugadores.All(j => j.Seat is >= 0 and <= 3))
            {
                Trace($"[CLIENTE] Seat fuera de rango 0..3 en INFO (después de parse). Seats=[{string.Join(",", _jugadores.Select(j => j.Seat))}]. Data={data}");
                return;
            }

            int seatFront = (MiNumeroJugador + 2) % 4;
            int seatRight = (MiNumeroJugador + 1) % 4;
            int seatLeft = (MiNumeroJugador + 3) % 4;

            var front = _jugadores.First(j => j.Seat == seatFront);
            var right = _jugadores.First(j => j.Seat == seatRight);
            var left = _jugadores.First(j => j.Seat == seatLeft);

            var asientos = new AsientosPartida
            {
                Yo = yo,
                Front = front,
                Left = left,
                Right = right,
                MiTalde = MiIdTaldea
            };

            Trace($"[CLIENTE] Yo seat={MiNumeroJugador} => Front={seatFront}, Right={seatRight}, Left={seatLeft}");
            Trace($"[CLIENTE] Disparando OnAsientosCalculados");
            OnAsientosCalculados?.Invoke(asientos);
        }

        public async Task<string> ConectarYUnirse(string ip, int puerto, string modo, string codigo = "")
        {
            Trace($"[CLIENTE] ConectarYUnirse modo={modo}, MiIdFirestore='{MiIdFirestore}', DocumentId(session)='{UserSession.Instance.DocumentId}'");

            if (_client == null || !_client.Connected)
            {
                await Conectar(ip, puerto);
            }

            // Enviamos el modo UNA SOLA VEZ
            await EnviarLineaAsync(modo);

            switch (modo)
            {
                case "CREAR_PRIVADA":
                    await EnviarLineaAsync(UserSession.Instance.DocumentId);

                    string confirmacion = await LeerLineaAsync(); // "CODIGO"
                    string codigoGenerado = await LeerLineaAsync();

                    IniciarEscucha();
                    return codigoGenerado;

                case "UNIRSE_PRIVADA":
                    string resp = await LeerLineaAsync(); // "PEDIR_CODIGO"
                    if (resp == "PEDIR_CODIGO")
                    {
                        await EnviarLineaAsync(codigo);
                        await EnviarLineaAsync(UserSession.Instance.DocumentId);

                        string finalOk = await LeerLineaAsync(); // "OK"
                        if (finalOk == "OK")
                        {
                            IniciarEscucha();
                            return "OK";
                        }
                    }
                    return "ERROR";

                case "PUBLICA":
                case "BIKOTEAK":
                case "ID_ESKATU":
                    // Mantener log visible de envío de ID (no quitar)
                    Console.WriteLine($"[CLIENTE] Enviando DocumentId para {modo}: {UserSession.Instance.DocumentId}");
                    Debug.WriteLine($"[CLIENTE] Enviando DocumentId para {modo}: {UserSession.Instance.DocumentId}");

                    await EnviarLineaAsync(UserSession.Instance.DocumentId);

                    string okPub = await LeerLineaAsync(); // "OK"
                    Trace($"[CLIENTE] Respuesta tras enviar ID ({modo}): '{okPub}'");
                    if (okPub == "OK")
                    {
                        IniciarEscucha();
                        return "OK";
                    }
                    return "ERROR";

                default:
                    return "ERROR_MODO_NO_SOPORTADO";
            }

            return "ERROR";
        }

        private void IniciarEscucha()
        {
            Trace("[CLIENTE] IniciarEscucha()");
            _listenCts?.Cancel();
            _listenCts = new CancellationTokenSource();
            _listenTask = Task.Run(() => EscucharServidor(_listenCts.Token));
        }

        private async Task EscucharServidor(CancellationToken ct)
        {
            try
            {
                Trace("[CLIENTE] EscucharServidor: iniciado");

                while (!ct.IsCancellationRequested && _client != null && _client.Connected)
                {
                    string? linea = await _reader.ReadLineAsync();

                    if (linea == null)
                    {
                        Trace("[CLIENTE] Servidor cerró la conexión (FIN). ");
                        break;
                    }

                    if (!EsCarta(linea))
                        Trace($"[CLIENTE] << {linea}");

                    switch (linea)
                    {
                        case "CARDS":
                            await LeerYEnviarCartas();
                            break;

                        case "TURN":
                            OnMiTurno?.Invoke();
                            break;

                        case "ERABAKIA":
                            {
                                // Siguiente línea: "uid;serverId;mensaje"
                                string payload = await LeerLineaAsync() ?? string.Empty;
                                Trace($"[CLIENTE] ERABAKIA payload='{payload}'");
                                // Enviar a UI en una sola línea para que no tenga que leer del socket
                                OnComandoRecibido?.Invoke($"ERABAKIA|{payload}");
                                break;
                            }

                        case "GRANDES":
                        case "PEQUEÑAS":
                        case "PARES":
                        case "JUEGO":
                            OnComandoRecibido?.Invoke(linea);
                            break;

                        case "PUNTUAKJASO":
                            string[] pts = new string[4];
                            for (int i = 0; i < 4; i++) pts[i] = await LeerLineaAsync() ?? "0";
                            OnComandoRecibido?.Invoke($"PUNTOS|{string.Join("|", pts)}");
                            break;

                        case "ALL_MUS":
                            OnComandoRecibido?.Invoke("ALL_MUS");
                            break;

                        default:
                            if (EsCarta(linea))
                            {
                                await LeerCartasSueltas(linea);
                            }
                            else if (linea.StartsWith("INFO:", StringComparison.Ordinal))
                            {
                                Trace($"[CLIENTE] Recibido INFO. MiIdFirestore='{MiIdFirestore}'. Data='{linea}'");
                                ProcesarInfoInicial(linea.Substring(5));
                            }
                            else if (linea.StartsWith("PUNTOS:", StringComparison.Ordinal))
                            {
                                try
                                {
                                    string[] partes = linea.Split(':');

                                    if (partes.Length >= 3)
                                    {
                                        int idTaldea = int.Parse(partes[1]);
                                        int cantidad = int.Parse(partes[2]);
                                        OnPuntosRecibidos?.Invoke(idTaldea, cantidad);
                                    }
                                }
                                catch
                                {
                                    Trace("[CLIENTE] Error leyendo formato de puntos");
                                }
                            }
                            else
                            {
                                OnComandoRecibido?.Invoke(linea);
                            }
                            break;
                    }
                }
            }
            catch (IOException ioEx)
            {
                Trace($"[CLIENTE] Error de socket: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Trace($"[CLIENTE] Error general escucha: {ex.Message}");
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    OnDisconnected?.Invoke();
                }
                Desconectar();
            }
        }

        private async Task EnviarLineaAsync(string mensaje)
        {
            if (_writer == null) return;
            _writer.WriteLine(mensaje);
        }

        private async Task<string> LeerLineaAsync()
        {
            if (_reader == null) return null;
            return await _reader.ReadLineAsync();
        }

        private async Task LeerCartasSueltas(string primeraCarta)
        {
            string[] cartas = new string[4];
            cartas[0] = primeraCarta;
            for (int i = 1; i < 4; i++)
            {
                cartas[i] = await LeerLineaAsync() ?? "";
            }
            OnCartasRecibidas?.Invoke(cartas);
        }

        private async Task LeerYEnviarCartas()
        {
            string[] cartas = new string[4];
            for (int i = 0; i < 4; i++)
            {
                cartas[i] = await LeerLineaAsync() ?? "";
            }
            OnCartasRecibidas?.Invoke(cartas);
        }

        private bool EsCarta(string linea)
        {
            if (string.IsNullOrEmpty(linea)) return false;
            string[] palos = { "oro", "copa", "espada", "basto" };
            foreach (var palo in palos)
            {
                if (linea.StartsWith(palo, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public void EnviarComando(string comando)
        {
            try
            {
                if (_writer != null && _client?.Connected == true)
                {
                    _writer.WriteLine(comando);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al enviar: " + ex.Message);
            }
        }

        public void Desconectar()
        {
            try
            {
                _listenCts?.Cancel();

                _writer?.Close();
                _reader?.Close();

                if (_client != null)
                {
                    _client.Close();
                    _client.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error al desconectar: {ex.Message}");
            }
            finally
            {
                _writer = null;
                _reader = null;
                _client = null;
                _listenCts = null;
            }
        }

        public void Dispose() => Desconectar();
    }

    public sealed class InfoJugadorPartida
    {
        public required string FirestoreId { get; init; }
        public required int Talde { get; init; }
        public required int Seat { get; init; } // 0..3 en mesa
    }

    public sealed class AsientosPartida
    {
        public required InfoJugadorPartida Yo { get; init; }
        public required InfoJugadorPartida Front { get; init; }
        public required InfoJugadorPartida Left { get; init; }
        public required InfoJugadorPartida Right { get; init; }
        public required int MiTalde { get; init; }

        public bool EsMiEquipo(InfoJugadorPartida j) => j.Talde == MiTalde;
    }
}