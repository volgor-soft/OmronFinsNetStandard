using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    /// <summary>
    /// Provides basic functionalities for interacting with the PLC, including connection management, sending, and receiving data.
    /// </summary>
    internal class BasicClass : IDisposable
    {
        /// <summary>
        /// The TCP client used for the connection to the PLC.
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// The network stream used for sending and receiving data.
        /// </summary>
        private NetworkStream _stream;

        /// <summary>
        /// The PC node address.
        /// </summary>
        public byte PCNode { get; set; }

        /// <summary>
        /// The PLC node address.
        /// </summary>
        public byte PLCNode { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BasicClass"/> class.
        /// </summary>
        /// <param name="pcNode">The PC node address.</param>
        /// <param name="plcNode">The PLC node address.</param>
        public BasicClass(byte pcNode, byte plcNode)
        {
            PCNode = pcNode;
            PLCNode = plcNode;
            _client = new TcpClient();
        }

        /// <summary>
        /// Checks the connectivity to the PLC by sending a ping.
        /// </summary>
        /// <param name="ip">The IP address of the PLC.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains <c>true</c> if the ping was successful; otherwise, <c>false</c>.</returns>
        /// <exception cref="FinsPingException">Thrown when the ping operation fails.</exception>
        public async Task<bool> PingCheckAsync(string ip, int timeout)
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
                    throw new FinsPingException($"Ping to {ip} failed.", ex);
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
        public async Task ConnectAsync(string ipAddress, int port)
        {
            try
            {
                await _client.ConnectAsync(ipAddress, port);
                _stream = _client.GetStream();
            }
            catch (SocketException ex)
            {
                throw new FinsCommunicationException($"Failed to connect to PLC at {ipAddress}:{port}.", ex);
            }
        }

        /// <summary>
        /// Disconnects from the PLC.
        /// </summary>
        public void Disconnect()
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
        public async Task SendDataAsync(byte[] data)
        {
            if (_stream == null)
                throw new InvalidOperationException("Not connected to the PLC.");

            try
            {
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch (SocketException ex)
            {
                throw new FinsCommunicationException("Failed to send data to the PLC.", ex);
            }
        }

        /// <summary>
        /// Receives data from the PLC asynchronously.
        /// </summary>
        /// <param name="buffer">The buffer to store the received data.</param>
        /// <returns>A task that represents the asynchronous receive operation. The task result contains the number of bytes read.</returns>
        /// <exception cref="InvalidOperationException">Thrown when not connected to the PLC.</exception>
        /// <exception cref="FinsCommunicationException">Thrown when receiving data fails.</exception>
        public async Task<int> ReceiveDataAsync(byte[] buffer)
        {
            if (_stream == null)
                throw new InvalidOperationException("Not connected to the PLC.");

            try
            {
                int bytesRead = 0;
                int totalBytesRead = 0;

                while (totalBytesRead < buffer.Length)
                {
                    bytesRead = await _stream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                    if (bytesRead == 0)
                        throw new FinsCommunicationException("Connection reset by peer during data reception.");
                    totalBytesRead += bytesRead;
                }

                return totalBytesRead;
            }
            catch (SocketException ex)
            {
                throw new FinsCommunicationException("Failed to receive data from the PLC.", ex);
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="BasicClass"/>.
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}
