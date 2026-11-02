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

        /// <summary>
        /// The longest a command may wait for the PLC to answer. Without it, a PLC that stops
        /// answering keeps the socket lock forever and every later command waits behind it.
        /// </summary>
        private const int IoTimeoutMs = 5000;

        private TcpClient _client;
        private NetworkStream _stream;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1); // Mutex for socket access

        public string IpAddress { get; }
        public int Port { get; }

        /// <summary>
        /// True when the socket is open and the FINS handshake on it has completed.
        /// </summary>
        public bool IsConnected => _client != null && _stream != null && _client.Connected && PCNode != 0 && PLCNode != 0;

        // FINS Nodes are specific to this connection
        public byte PCNode { get; private set; }
        public byte PLCNode { get; private set; }

        // Reference counting to handle multiple parts of the app using the same connection
        private int _referenceCount = 0;
        public int ReferenceCount => _referenceCount;

        public FinsConnection(string ip, int port)
        {
            IpAddress = ip;
            Port = port;
        }

        public void IncrementUsage()
        {
            Interlocked.Increment(ref _referenceCount);
        }

        public int DecrementUsage()
        {
            return Interlocked.Decrement(ref _referenceCount);
        }

        /// <summary>
        /// Opens the socket and performs the FINS handshake, unless that is already done.
        /// </summary>
        /// <returns>False when the PLC cannot be reached. Throws <see cref="FinsError"/> when the PLC refuses the handshake.</returns>
        /// <remarks>
        /// The socket and the handshake are set up under one lock, so a command can never go out on
        /// a socket whose handshake another thread has not finished yet.
        /// </remarks>
        public async Task<bool> ConnectAsync(int timeout)
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

                // A socket that has been connected once cannot connect again: Windows answers
                // WSAEISCONN (10056) on every attempt, and the PLC stays unreachable until the
                // process restarts. A connect that timed out can also still complete later on the
                // old socket. So every attempt starts from a new socket.
                CloseSocket();
                TcpClient client = new TcpClient();
                _client = client;

                // TCP Connect
                try
                {
                    Task connectTask = client.ConnectAsync(IpAddress, Port);
                    if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
                    {
                        // Closing the socket below fails the pending connect; observe it so it
                        // does not surface as an unobserved task exception.
                        _ = connectTask.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                        Logger.Error($"Timeout connecting to {IpAddress}:{Port}");
                        CloseSocket();
                        return false;
                    }

                    await connectTask; // Await to propagate exceptions

                    _stream = client.GetStream();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Physical connection failed to {IpAddress}:{Port}");
                    CloseSocket();
                    return false;
                }

                // FINS Handshake: the PLC assigns the node addresses for this TCP connection.
                try
                {
                    Logger.Debug("Performing FINS Handshake...");
                    byte[] response = new byte[24];
                    int bytesRead = await ExchangeAsync(FinsCommandBuilder.HandShake(), response);

                    if (bytesRead < 24)
                    {
                        throw new FinsError(0xFF, 0xFF, "Incomplete handshake response.");
                    }

                    if (response[15] != 0)
                    {
                        throw new FinsError(response[15], response[16], "Handshake failed.");
                    }

                    PCNode = response[19];
                    PLCNode = response[23];
                    Logger.Info("Handshake successful. Nodes set: PC={0}, PLC={1}", PCNode, PLCNode);
                    return true;
                }
                catch
                {
                    CloseSocket();
                    throw;
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Executes a FINS command transaction (Send + Receive) thread-safely.
        /// </summary>
        /// <param name="buildCommand">Builds the command from the PLC node and the PC node of the current connection.</param>
        /// <param name="responseBuffer">Receives the response; its length is the expected response size.</param>
        /// <remarks>
        /// The command is built inside the lock, so it always carries the nodes of the socket it goes
        /// out on, even when another thread has reconnected in the meantime. Any failure closes the
        /// socket, so the next <see cref="ConnectAsync"/> opens a new one and repeats the handshake
        /// instead of reusing a connection the PLC has dropped.
        /// </remarks>
        public async Task<int> TransceiveAsync(Func<byte, byte, byte[]> buildCommand, byte[] responseBuffer)
        {
            await _lock.WaitAsync();
            try
            {
                if (!IsConnected) throw new InvalidOperationException("Client is not connected.");

                int bytesRead = await ExchangeAsync(buildCommand(PLCNode, PCNode), responseBuffer);
                if (bytesRead < responseBuffer.Length)
                {
                    // ExchangeAsync only stops short when the PLC closed the connection.
                    Logger.Warn($"Connection closed by {IpAddress}:{Port} after {bytesRead} of {responseBuffer.Length} bytes");
                    CloseSocket();
                }

                return bytesRead;
            }
            catch (Exception ex)
            {
                if (!(ex is InvalidOperationException)) Logger.Error(ex, "Error during Transceive");
                CloseSocket();
                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Sends a frame and reads the response. Must be called inside the lock.
        /// </summary>
        private async Task<int> ExchangeAsync(byte[] command, byte[] responseBuffer)
        {
            NetworkStream stream = _stream ?? throw new InvalidOperationException("Not connected");
            using (var timeout = new CancellationTokenSource(IoTimeoutMs))
            {
                try
                {
                    await stream.WriteAsync(command, 0, command.Length, timeout.Token);

                    int totalBytesRead = 0;
                    while (totalBytesRead < responseBuffer.Length)
                    {
                        int bytesRead = await stream.ReadAsync(responseBuffer, totalBytesRead,
                            responseBuffer.Length - totalBytesRead, timeout.Token);
                        if (bytesRead == 0) break; // Connection closed
                        totalBytesRead += bytesRead;
                    }

                    return totalBytesRead;
                }
                catch (OperationCanceledException ex) when (timeout.IsCancellationRequested)
                {
                    throw new FinsCommunicationException($"No response from {IpAddress}:{Port} within {IoTimeoutMs} ms.", ex);
                }
            }
        }

        /// <summary>
        /// Closes the socket and forgets the FINS nodes, which belong to that TCP connection only.
        /// </summary>
        private void CloseSocket()
        {
            try
            {
                _stream?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error closing the stream to {IpAddress}:{Port}");
            }

            try
            {
                _client?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Debug(ex, $"Error closing the socket to {IpAddress}:{Port}");
            }

            _stream = null;
            _client = null;
            PCNode = 0;
            PLCNode = 0;
        }

        public void DisconnectPhysical()
        {
            CloseSocket();
            Logger.Info($"Physical connection closed: {IpAddress}:{Port}");
        }

        public void Dispose()
        {
            DisconnectPhysical();
            _lock?.Dispose();
        }
    }
}
