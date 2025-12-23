using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    /// <summary>
    /// Represents a single physical connection to a PLC via FINS/TCP.
    /// Thread-safe handling of the socket and handshake data.
    /// </summary>
    internal class FinsConnection : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1); // Mutex for socket access

        public string IpAddress { get; }
        public int Port { get; }
        public bool IsConnected => _client != null && _client.Connected;

        // FINS Nodes are specific to this connection
        public byte PCNode { get; set; }
        public byte PLCNode { get; set; }

        // Reference counting to handle multiple parts of the app using the same connection
        private int _referenceCount = 0;
        public int ReferenceCount => _referenceCount;

        public FinsConnection(string ip, int port)
        {
            IpAddress = ip;
            Port = port;
            _client = new TcpClient();
        }

        public void IncrementUsage()
        {
            Interlocked.Increment(ref _referenceCount);
        }

        public int DecrementUsage()
        {
            return Interlocked.Decrement(ref _referenceCount);
        }

        public async Task<bool> ConnectPhysicalAsync(int timeout)
        {
            // If already connected, do nothing
            if (IsConnected) return true;

            await _lock.WaitAsync();
            try
            {
                if (IsConnected) return true; // Double check inside lock

                // Ping Check
                using (var ping = new Ping())
                {
                    try
                    {
                        var reply = await ping.SendPingAsync(IpAddress, timeout);
                        if (reply.Status != IPStatus.Success)
                        {
                            Logger.Warn($"Ping to {IpAddress} failed: {reply.Status}");
                            return false;
                        }
                    }
                    catch (PingException ex)
                    {
                        Logger.Error(ex, $"Ping exception to {IpAddress}");
                        return false;
                    }
                }

                // TCP Connect
                try
                {
                    // Create new client if old one was disposed
                    if (_client == null || _client.Client == null) _client = new TcpClient();

                    var connectTask = _client.ConnectAsync(IpAddress, Port);
                    if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
                    {
                        Logger.Error($"Timeout connecting to {IpAddress}:{Port}");
                        return false;
                    }

                    await connectTask; // Await to propagate exceptions

                    _stream = _client.GetStream();
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Physical connection failed to {IpAddress}:{Port}");
                    return false;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SendAsync(byte[] data)
        {
            if (_stream == null) throw new InvalidOperationException("Not connected");
            await _stream.WriteAsync(data, 0, data.Length);
        }

        public async Task<int> ReceiveAsync(byte[] buffer)
        {
            if (_stream == null) throw new InvalidOperationException("Not connected");

            int totalBytesRead = 0;
            while (totalBytesRead < buffer.Length)
            {
                int bytesRead = await _stream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                if (bytesRead == 0) break; // Connection closed
                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }

        /// <summary>
        /// Executes a FINS command transaction (Send + Receive) thread-safely.
        /// </summary>
        public async Task<int> TransceiveAsync(byte[] command, byte[] responseBuffer)
        {
            await _lock.WaitAsync();
            try
            {
                await SendAsync(command);
                return await ReceiveAsync(responseBuffer);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during Transceive");
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void DisconnectPhysical()
        {
            try
            {
                _stream?.Close();
                _client?.Close();
                _client = null;
                Logger.Info($"Physical connection closed: {IpAddress}:{Port}");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error closing physical connection");
            }
        }

        public void Dispose()
        {
            DisconnectPhysical();
            _lock?.Dispose();
        }
    }
}