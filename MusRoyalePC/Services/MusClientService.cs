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
        public event Action? OnMiTurno;
        public event Action<string>? OnComandoRecibido;
        public event Action<Exception>? OnError;
        public event Action? OnDisconnected;

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