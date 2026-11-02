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
                // 2. Open the socket and perform the FINS handshake, unless another client already did.
                bool connected = await _connection.ConnectAsync(timeout);
                if (!connected)
                {
                    // Failed to connect physically. Release the reference we just took.
                    ConnectionManager.ReleaseConnection(_ipAddress, _port);
                    _connection = null;
                    throw new FinsCommunicationException($"Could not establish physical connection to {ipAddress}");
                }

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
        // The connection builds each command with the nodes of the socket it goes out on.
        private Task<int> TransceiveAsync(Func<byte, byte, byte[]> buildCommand, byte[] buffer)
        {
            FinsConnection connection = _connection ?? throw new InvalidOperationException("Client is not connected.");
            return connection.TransceiveAsync(buildCommand, buffer);
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
            short cnInt = short.Parse(address.Split('.')[0]);
            short cnBit = short.Parse(address.Split('.')[1]);

            byte[] buffer = new byte[31];

            int bytesRead = await TransceiveAsync(
                (plc, pc) => FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Bit, cnInt, cnBit, 1, plc, pc), buffer);

            ValidateResponse(buffer, bytesRead, 31);

            return (short)buffer[30];
        }

        public async Task SetBitStateAsync(PlcMemory memory, string address, BitState state)
        {
            short cnInt = short.Parse(address.Split('.')[0]);
            short cnBit = short.Parse(address.Split('.')[1]);

            byte[] buffer = new byte[30];
            int bytesRead = await TransceiveAsync((plc, pc) =>
            {
                byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Write, memory, MemoryType.Bit, cnInt, cnBit, 1, plc, pc);

                // Append data (BitState)
                byte[] fullCommand = new byte[command.Length + 1];
                Buffer.BlockCopy(command, 0, fullCommand, 0, command.Length);
                fullCommand[command.Length] = (byte)state;
                return fullCommand;
            }, buffer);

            ValidateResponse(buffer, bytesRead, 30);
        }

        public async Task<float> ReadRealAsync(PlcMemory memory, ushort address)
        {
            byte[] buffer = new byte[34];

            int bytesRead = await TransceiveAsync(
                (plc, pc) => FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Word, (short)address, 0, 2, plc, pc), buffer);
            ValidateResponse(buffer, bytesRead, 34);

            byte[] temp = new byte[] { buffer[31], buffer[30], buffer[33], buffer[32] };
            return BitConverter.ToSingle(temp, 0);
        }

        public async Task<short[]> ReadWordsAsync(PlcMemory memory, ushort address, ushort count)
        {
            // expected size calculation
            int expectedSize = 30 + count * 2;
            byte[] buffer = new byte[expectedSize];

            int bytesRead = await TransceiveAsync(
                (plc, pc) => FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Word, (short)address, 0, (short)count, plc, pc),
                buffer);
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
            byte[] wdata = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                byte[] temp = BitConverter.GetBytes(data[i]);
                wdata[i * 2] = temp[1];
                wdata[i * 2 + 1] = temp[0];
            }

            byte[] buffer = new byte[30];
            int bytesRead = await TransceiveAsync((plc, pc) =>
            {
                byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Write, memory, MemoryType.Word, (short)address, 0, (short)data.Length,
                    plc, pc);
                byte[] fullCommand = new byte[command.Length + wdata.Length];
                Buffer.BlockCopy(command, 0, fullCommand, 0, command.Length);
                Buffer.BlockCopy(wdata, 0, fullCommand, command.Length, wdata.Length);
                return fullCommand;
            }, buffer);
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