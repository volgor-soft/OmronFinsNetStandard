using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    /// <summary>
    /// Provides basic functionalities for interacting with the PLC, including connection management, sending, and receiving data.
    /// </summary>
    class BasicClass
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private static TcpClient _client;
        /// <summary>
        /// The TCP client used for the connection to the PLC.
        /// </summary>
        public static TcpClient Client
        {
            set => _client = value;
        }

        /// <summary>
        /// The network stream used for sending and receiving data.
        /// </summary>
        private static NetworkStream _stream;

        private static byte _pcNode;
        /// <summary>
        /// The PC node address.
        /// </summary>
        public static byte PCNode
        {
            get => _pcNode;
            set => _pcNode = value;
        }

        private static byte _plcNode;
        /// <summary>
        /// The PLC node address.
        /// </summary>
        public static byte PLCNode
        {
            get => _plcNode;
            set => _plcNode = value;
        }

        /// <summary>
        /// Checks the connectivity to the PLC by sending a ping.
        /// </summary>
        /// <param name="ip">The IP address of the PLC.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the ping was successful; otherwise, <c>false</c>.</returns>
        /// <exception cref="FinsPingException">Thrown when the ping operation fails.</exception>
        public static async Task<bool> PingCheckAsync(string ip, int timeout)
        {
            using (var ping = new Ping())
            {
                try
                {
                    var reply = await ping.SendPingAsync(ip, timeout);
                    return reply.Status == IPStatus.Success;
                }
                catch (PingException ex)
                {
                    Logger.Error(ex, $"Ping to {ip} failed.");
                    return false;
                }
            }
        }

        /// <summary>
        /// Connects to the PLC asynchronously.
        /// </summary>
        /// <param name="ipAddress">The IP address of the PLC.</param>
        /// <param name="port">The port number for the connection.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        /// <exception cref="FinsCommunicationException">Thrown when the connection fails.</exception>
        public static async Task ConnectAsync(string ipAddress, int port)
        {
            try
            {
                await _client.ConnectAsync(ipAddress, port);
                _stream = _client.GetStream();
            }
            catch (SocketException ex)
            {
                Logger.Error(ex, $"Failed to connect to PLC at {ipAddress}:{port}.");
            }
        }

        /// <summary>
        /// Disconnects from the PLC.
        /// </summary>
        public static void Disconnect()
        {
            _stream?.Close();
            _client?.Close();
        }

        /// <summary>
        /// Sends data to the PLC asynchronously.
        /// </summary>
        /// <param name="data">The data to send.</param>
        /// <returns>A task that represents the asynchronous send operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to the PLC.</exception>
        /// <exception cref="FinsCommunicationException">Thrown when sending data fails.</exception>
        public static async Task SendDataAsync(byte[] data)
        {
            if (_stream == null)
            {
                Logger.Error("Not connected to the PLC.");
                return;
            }

            try
            {
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch (SocketException ex)
            {
                Logger.Error(ex, "Failed to send data to the PLC.");
            }
        }

        /// <summary>
        /// Receives data from the PLC asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer to store the received data.</param>
        /// <returns>A task that represents the asynchronous receive operation. The task result contains the number of bytes read.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to the PLC.</exception>
        /// <exception cref="FinsCommunicationException">Thrown when receiving data fails.</exception>
        public static async Task<int> ReceiveDataAsync(byte[] buffer)
        {
            if (_stream == null)
            {
                Logger.Error("Not connected to the PLC.");
                return -1;
            }

            try
            {
                int bytesRead = 0;
                int totalBytesRead = 0;

                while (totalBytesRead < buffer.Length)
                {
                    bytesRead = await _stream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                    if (bytesRead == 0) return totalBytesRead;
                    totalBytesRead += bytesRead;
                }

                return totalBytesRead;
            }
            catch (SocketException ex)
            {
                Logger.Error(ex, "Failed to receive data from the PLC.");
                return -1;
            }
        }
    }
}
