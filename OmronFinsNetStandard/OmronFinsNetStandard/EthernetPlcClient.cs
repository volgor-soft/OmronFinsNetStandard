using System;
using System.Threading.Tasks;
using NLog;
using OmronFinsNetStandard.Enums;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    public class EthernetPlcClient : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        private FinsConnection _connection;
        private string _ipAddress;
        private int _port;
        private bool _isDisposed = false;

        public EthernetPlcClient()
        {
            Logger.Debug("EthernetPlcClient initialized.");
        }

        public async Task<bool> ConnectAsync(string ipAddress, int port, int timeout = 300)
        {
            _ipAddress = ipAddress;
            _port = port;

            Logger.Debug("Requesting connection to PLC at {0}:{1}", ipAddress, port);

            // 1. Get connection from pool (Thread safe)
            _connection = ConnectionManager.GetConnection(ipAddress, port);

            try
            {
                // 2. Establish physical connection if not already connected
                // Note: If another thread connected it 1ms ago, this returns true immediately.
                bool connected = await _connection.ConnectPhysicalAsync(timeout);
                if (!connected)
                {
                    // Failed to connect physically. Release the reference we just took.
                    ConnectionManager.ReleaseConnection(_ipAddress, _port);
                    _connection = null;
                    throw new FinsCommunicationException($"Could not establish physical connection to {ipAddress}");
                }

                // 3. Handshake Logic
                // IMPORTANT: We only do handshake if we haven't determined Nodes yet.
                // Or if we want to ensure FINS level is up. 
                // Since this is a shared connection, we check if Nodes are already set (meaning handshake was done).

                if (_connection.PCNode != 0 && _connection.PLCNode != 0)
                {
                    Logger.Debug("Connection reused. Skipping Handshake. Nodes: PC={0}, PLC={1}", _connection.PCNode, _connection.PLCNode);
                    return true;
                }

                // Perform Handshake inside the Lock
                // We construct a specific specialized lock usage or use Transceive
                Logger.Debug("Performing FINS Handshake...");

                // We use TransceiveAsync to ensure atomic send/receive on the shared socket
                byte[] responseBuffer = new byte[24];
                int bytesRead = await _connection.TransceiveAsync(FinsCommandBuilder.HandShake(), responseBuffer);

                if (bytesRead < 24)
                {
                    throw new FinsError(0xFF, 0xFF, "Incomplete handshake response.");
                }

                if (responseBuffer[15] != 0)
                {
                    throw new FinsError(responseBuffer[15], responseBuffer[16], "Handshake failed.");
                }

                // Set nodes on the shared connection object
                _connection.PCNode = responseBuffer[19];
                _connection.PLCNode = responseBuffer[23];

                Logger.Info("Handshake successful. Nodes set: PC={0}, PLC={1}", _connection.PCNode, _connection.PLCNode);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Connection process failed.");
                // If we failed, ensure we release the pool reference
                if (_connection != null)
                {
                    ConnectionManager.ReleaseConnection(_ipAddress, _port);
                    _connection = null;
                }

                return false;
            }
        }

        public async Task CloseAsync()
        {
            if (_connection == null || _isDisposed) return;

            Logger.Debug("Releasing connection reference for {0}:{1}", _ipAddress, _port);
            await Task.Run(() =>
            {
                ConnectionManager.ReleaseConnection(_ipAddress, _port);
                _connection = null;
            });
            _isDisposed = true;
        }

        // --- Data Operations ---
        // Helper to get nodes for command builder
        private (byte pc, byte plc) GetNodes()
        {
            if (_connection == null || !_connection.IsConnected)
                throw new InvalidOperationException("Client is not connected.");
            return (_connection.PCNode, _connection.PLCNode);
        }

        public async Task<short> ReadWordAsync(PlcMemory memory, ushort address)
        {
            var res = await ReadWordsAsync(memory, address, 1);
            return res[0];
        }

        public async Task WriteWordAsync(PlcMemory memory, ushort address, short data)
        {
            await WriteWordsAsync(memory, address, new short[] { data });
        }

        public async Task<short> GetBitStateAsync(PlcMemory memory, string address)
        {
            var nodes = GetNodes();
            short cnInt = short.Parse(address.Split('.')[0]);
            short cnBit = short.Parse(address.Split('.')[1]);

            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Bit, cnInt, cnBit, 1, nodes.plc, nodes.pc);
            byte[] buffer = new byte[31];

            int bytesRead = await _connection.TransceiveAsync(command, buffer);

            ValidateResponse(buffer, bytesRead, 31);

            return (short)buffer[30];
        }

        public async Task SetBitStateAsync(PlcMemory memory, string address, BitState state)
        {
            var nodes = GetNodes();
            short cnInt = short.Parse(address.Split('.')[0]);
            short cnBit = short.Parse(address.Split('.')[1]);

            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Write, memory, MemoryType.Bit, cnInt, cnBit, 1, nodes.plc, nodes.pc);

            // Append data (BitState)
            byte[] fullCommand = new byte[command.Length + 1];
            Buffer.BlockCopy(command, 0, fullCommand, 0, command.Length);
            fullCommand[command.Length] = (byte)state;

            byte[] buffer = new byte[30];
            int bytesRead = await _connection.TransceiveAsync(fullCommand, buffer);

            ValidateResponse(buffer, bytesRead, 30);
        }

        public async Task<float> ReadRealAsync(PlcMemory memory, ushort address)
        {
            var nodes = GetNodes();
            byte[] buffer = new byte[34];
            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Word, (short)address, 0, 2, nodes.plc, nodes.pc);

            int bytesRead = await _connection.TransceiveAsync(command, buffer);
            ValidateResponse(buffer, bytesRead, 34);

            byte[] temp = new byte[] { buffer[31], buffer[30], buffer[33], buffer[32] };
            return BitConverter.ToSingle(temp, 0);
        }

        public async Task<short[]> ReadWordsAsync(PlcMemory memory, ushort address, ushort count)
        {
            var nodes = GetNodes();
            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Word, (short)address, 0, (short)count, nodes.plc,
                nodes.pc);

            // expected size calculation
            int expectedSize = 30 + count * 2;
            byte[] buffer = new byte[expectedSize];

            int bytesRead = await _connection.TransceiveAsync(command, buffer);
            ValidateResponse(buffer, bytesRead, expectedSize);

            short[] reData = new short[count];
            for (int i = 0; i < count; i++)
            {
                int index = 30 + i * 2;
                reData[i] = BitConverter.ToInt16(new byte[] { buffer[index + 1], buffer[index] }, 0);
            }

            return reData;
        }

        public async Task WriteWordsAsync(PlcMemory memory, ushort address, short[] data)
        {
            var nodes = GetNodes();
            byte[] wdata = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                byte[] temp = BitConverter.GetBytes(data[i]);
                wdata[i * 2] = temp[1];
                wdata[i * 2 + 1] = temp[0];
            }

            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Write, memory, MemoryType.Word, (short)address, 0, (short)data.Length, nodes.plc,
                nodes.pc);
            byte[] fullCommand = new byte[command.Length + wdata.Length];
            Buffer.BlockCopy(command, 0, fullCommand, 0, command.Length);
            Buffer.BlockCopy(wdata, 0, fullCommand, command.Length, wdata.Length);

            byte[] buffer = new byte[30];
            int bytesRead = await _connection.TransceiveAsync(fullCommand, buffer);
            ValidateResponse(buffer, bytesRead, 30);
        }

        private void ValidateResponse(byte[] buffer, int bytesRead, int minExpected)
        {
            // 1. Check strict buffer size
            // Note: If a TCP Head Error occurs, the response is usually just 16 bytes, 
            // regardless of what we expected for a full read command.
            if (bytesRead < 16)
            {
                throw new FinsError(0xFF, 0xFF, $"Response too short. Received {bytesRead} bytes.");
            }

            // 2. Check FINS/TCP Wrapper Error (Offset 15)
            // This is the "Head error without sub code" scenario you mentioned.
            FinsError? tcpError = ErrorCode.CheckTcpError(buffer[15]);
            if (tcpError != null)
            {
                Logger.Error($"TCP Wrapper Error: {tcpError.Message}");
                throw tcpError;
            }

            // 3. If TCP header is OK, check if we received enough data for the FINS frame
            if (bytesRead < minExpected)
            {
                throw new FinsError(0xFF, 0xFF, $"Incomplete FINS frame. Expected {minExpected}, got {bytesRead} bytes.");
            }

            // 4. Check FINS End Code (MRES/SRES at offset 28, 29 for standard frames)
            // Note: The offsets depend on your buffer structure. 
            // Usually: TCP Header (16) + FINS Header (10) + Command Code (2) = 28 bytes offset to MRES
            FinsError endError = ErrorCode.CheckEndCode(buffer[28], buffer[29]);
            if (endError != null && !endError.CanContinue)
            {
                Logger.Error($"FINS Logic Error: {endError.Message}");
                throw endError;
            }
        }

        public void Dispose()
        {
            // Fire and forget close logic to ensure release
            if (!_isDisposed)
            {
                CloseAsync().Wait();
            }
        }
    }
}