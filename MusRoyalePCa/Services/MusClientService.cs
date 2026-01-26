using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

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

        // New: surface errors to the UI layer (log/show message/etc.)
        public event Action<Exception>? OnError;
        public event Action? OnDisconnected;

        public bool IsConnected => _client?.Connected == true;

        public async Task Conectar(string ip, int puerto, int timeoutMs = 8000, CancellationToken cancellationToken = default)
        {
            Desconectar(); // ensure clean state on reconnect attempts

            try
            {
                _client = new TcpClient();

                // Implement connect timeout + external cancellation
                using var timeoutCts = new CancellationTokenSource(timeoutMs);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

                var connectTask = _client.ConnectAsync(ip, puerto);

                var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, linkedCts.Token));
                if (completed != connectTask)
                {
                    throw new TimeoutException($"Timeout connecting to {ip}:{puerto} after {timeoutMs}ms.");
                }

                // Ensure any SocketException is observed
                await connectTask;

                var stream = _client.GetStream();
                _reader = new StreamReader(stream);
                _writer = new StreamWriter(stream) { AutoFlush = true };

                _listenCts = new CancellationTokenSource();
                _listenTask = Task.Run(() => EscucharServidor(_listenCts.Token));
            }
            catch (Exception ex) when (ex is SocketException || ex is IOException || ex is TimeoutException || ex is OperationCanceledException)
            {
                // Clean up partially-initialized state
                Desconectar();
                OnError?.Invoke(ex);
                throw; 
            }
        }

        private async Task EscucharServidor(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var reader = _reader ?? throw new InvalidOperationException("Reader not initialized.");
                    string? linea = await reader.ReadLineAsync().WaitAsync(ct);

                    if (linea is null) break;

                    // 1. Caso estándar: El servidor avisa con "CARDS"
                    if (linea == "CARDS")
                    {
                        await LeerYEnviarCartas(reader, ct);
                    }
                    // 2. Caso Especial Descarte: El servidor envía cartas "sueltas" 
                    // Si la línea no es un comando y parece una carta (ej: "oro", "copa"...)
                    else if (EsCarta(linea))
                    {
                        string[] cartas = new string[4];
                        cartas[0] = linea; // La primera ya la hemos leído
                        for (int i = 1; i < 4; i++)
                        {
                            cartas[i] = await reader.ReadLineAsync().WaitAsync(ct) ?? "";
                        }
                        OnCartasRecibidas?.Invoke(cartas);
                    }
                    else if (linea == "TURN")
                    {
                        OnMiTurno?.Invoke();
                    }
                    else if (linea == "GRANDES")
                    {
                        OnComandoRecibido?.Invoke("GRANDES");
                    }
                    else
                    {
                        OnComandoRecibido?.Invoke(linea);
                    }
                }
            }
            catch (Exception ex) { /* ... resto del catch ... */ }
        }

        // Método auxiliar para detectar si un string es una carta
        private bool EsCarta(string linea)
        {
            string[] palos = { "oro", "copa", "espada", "basto" };
            foreach (var palo in palos)
            {
                if (linea.StartsWith(palo, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // Método auxiliar para no repetir código
        private async Task LeerYEnviarCartas(StreamReader reader, CancellationToken ct)
        {
            string[] cartas = new string[4];
            for (int i = 0; i < 4; i++)
            {
                cartas[i] = await reader.ReadLineAsync().WaitAsync(ct) ?? "";
            }
            OnCartasRecibidas?.Invoke(cartas);
        }

        public void EnviarComando(string comando)
        {
            try
            {
                if (_writer != null && _client.Connected)
                {
                    _writer.WriteLine(comando);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error al enviar: " + ex.Message);
            }
        }

        public void Desconectar()
        {
            try { _listenCts?.Cancel(); } catch { /* ignore */ }

            _writer?.Dispose();
            _reader?.Dispose();
            _client?.Close();

            _writer = null;
            _reader = null;
            _client = null;

            _listenCts?.Dispose();
            _listenCts = null;

            _listenTask = null;
        }

        public void Dispose() => Desconectar();
    }
}