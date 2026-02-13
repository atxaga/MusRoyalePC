using MusRoyalePC.Models;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics; // Necesario para Debug.WriteLine

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

        // Datos PROPIOS (rellenados antes de conectar o al recibir INFO)
        public string MiIdFirestore { get; set; }  // <-- IMPORTANTE: Asignar esto en el Login
        public int MiIdTaldea { get; private set; }
        public int MiNumeroJugador { get; private set; }

        // Datos del COMPAÑERO
        public string IdCompanero { get; private set; }
        public int NumeroCompanero { get; private set; }

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

        private void ProcesarInfoInicial(string data)
        {
            // data llega así: "1AbCdEf0,2GhIjKl1,1MnOpQr2,2StUvWx3"
            string[] jugadores = data.Split(',');

            // Paso 1: Primero me busco a MÍ MISMO para saber mi equipo
            foreach (string j in jugadores)
            {
                if (j.Length < 3) continue; // Protección contra datos vacíos

                // Extraemos los datos "pegados"
                int talde = int.Parse(j.Substring(0, 1));                  // Primer carácter
                int playerNum = int.Parse(j.Substring(j.Length - 1, 1));   // Último carácter
                string id = j.Substring(1, j.Length - 2);                  // Lo del medio

                if (id == MiIdFirestore)
                {
                    this.MiIdTaldea = talde;
                    this.MiNumeroJugador = playerNum;
                    Console.WriteLine($"[CLIENTE] Soy yo ({id}). Equipo: {talde}, Sitio: {playerNum}");
                    break; // Ya me encontré, salgo del bucle
                }
            }

            // Paso 2: Ahora que sé mi equipo, busco a mi COMPAÑERO
            // (Es aquel que tiene MI mismo IdTaldea pero NO es mi IdFirestore)
            foreach (string j in jugadores)
            {
                if (j.Length < 3) continue;

                int talde = int.Parse(j.Substring(0, 1));
                int playerNum = int.Parse(j.Substring(j.Length - 1, 1));
                string id = j.Substring(1, j.Length - 2);

                if (talde == this.MiIdTaldea && id != this.MiIdFirestore)
                {
                    this.IdCompanero = id;
                    this.NumeroCompanero = playerNum;
                    Console.WriteLine($"[CLIENTE] Mi compañero es {id} en el sitio {playerNum}");
                }
            }
        }

        public async Task<string> ConectarYUnirse(string ip, int puerto, string modo, string codigo = "")
        {
            if (_client == null || !_client.Connected)
            {
                await Conectar(ip, puerto);
            }

            // Enviamos el modo UNA SOLA VEZ
            await EnviarLineaAsync(modo);

            switch (modo)
            {
                case "CREAR_PRIVADA":
                    await EnviarLineaAsync(UserSession.Instance.Username);

                    string confirmacion = await LeerLineaAsync(); // "CODIGO"
                    string codigoGenerado = await LeerLineaAsync();

                    IniciarEscucha();
                    return codigoGenerado;

                case "UNIRSE_PRIVADA":
                    string resp = await LeerLineaAsync(); // "PEDIR_CODIGO"
                    if (resp == "PEDIR_CODIGO")
                    {
                        await EnviarLineaAsync(codigo);
                        await EnviarLineaAsync(UserSession.Instance.Username);

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
                    await EnviarLineaAsync(UserSession.Instance.Username);

                    string okPub = await LeerLineaAsync(); // "OK"
                    if (okPub == "OK")
                    {
                        IniciarEscucha();
                        return "OK";
                    }
                    return "ERROR";

                default:
                    return "ERROR_MODO_NO_SOPORTADO";
            }
        }

        private void IniciarEscucha()
        {
            _listenCts?.Cancel();
            _listenCts = new CancellationTokenSource();
            _listenTask = Task.Run(() => EscucharServidor(_listenCts.Token));
        }

        private async Task EscucharServidor(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _client != null && _client.Connected)
                {
                    // CAMBIO IMPORTANTE: Quitamos .WaitAsync(ct).
                    // ReadLineAsync devuelve null si el servidor cierra la conexión.
                    string? linea = await _reader.ReadLineAsync();

                    if (linea == null)
                    {
                        Debug.WriteLine("Servidor cerró la conexión (FIN).");
                        break;
                    }

                    switch (linea)
                    {
                        case "CARDS":
                            await LeerYEnviarCartas();
                            break;

                        case "TURN":
                            OnMiTurno?.Invoke();
                            break;

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
                            // --- 1. NUEVO: Detectar Info Inicial para saber mi ID de Talde ---
                            else if (linea.StartsWith("INFO:"))
                            {
                                // Pasamos todo lo que hay después de "INFO:"
                                ProcesarInfoInicial(linea.Substring(5));
                            }
                            // --- 2. MODIFICADO: Leer Puntos con ID de Talde ---
                            // Asumimos que el server manda: "PUNTOS:IdTaldea:Cantidad" (Ej: "PUNTOS:1:5")
                            else if (linea.StartsWith("PUNTOS:"))
                            {
                                try
                                {
                                    string[] partes = linea.Split(':');
                                    // partes[0] es "PUNTOS"
                                    // partes[1] es IdTaldea
                                    // partes[2] es Cantidad

                                    if (partes.Length >= 3)
                                    {
                                        int idTaldea = int.Parse(partes[1]);
                                        int cantidad = int.Parse(partes[2]);

                                        // Disparamos el evento con AMBOS datos para que la Vista sepa a quién sumar
                                        OnPuntosRecibidos?.Invoke(idTaldea, cantidad);
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("Error leyendo formato de puntos");
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
                // Esto pasa si el cable se desconecta
                Debug.WriteLine($"Error de socket: {ioEx.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error general escucha: {ex.Message}");
                // No lanzamos OnError aquí para no spamear popups al cerrar la app
            }
            finally
            {
                // Avisamos a la UI para que vuelva al Home
                if (!ct.IsCancellationRequested)
                {
                    OnDisconnected?.Invoke();
                }
                Desconectar();
            }
        }

        // --- Helpers para evitar bloqueos ---

        private async Task EnviarLineaAsync(string mensaje)
        {
            if (_writer == null) return;
            await _writer.WriteLineAsync(mensaje);
            await _writer.FlushAsync();
        }

        // Wrapper seguro para leer. Si devuelve null, es desconexión.
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
                    // Importante: No hace falta Flush aquí porque pusimos AutoFlush = true en el writer
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

                // Cerrar Writers/Readers primero
                _writer?.Close();
                _reader?.Close();

                // Forzar cierre del socket
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
}