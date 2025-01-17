﻿using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using NLog;
using OmronFinsNetStandard.Enums;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    /// <summary>
    /// Provides methods to manage Ethernet TCP connections to the PLC and perform read/write operations.
    /// </summary>
    public class EthernetPlcClient : IDisposable
    {
        private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Initializes a new instance of the <see cref="EthernetPlcClient"/> class.
        /// </summary>
        public EthernetPlcClient()
        {
            BasicClass.Client = new TcpClient();
            Logger.Debug("EthernetPlcClient initialized.");
        }


        /// <summary>
        /// Establishes a TCP connection to the PLC.
        /// </summary>
        /// <param name="ipAddress">The IP address of the PLC.</param>
        /// <param name="port">The port number for the connection.</param>
        /// <param name="timeout">The timeout in milliseconds.</param>
        /// <returns>A task that represents the asynchronous connect operation.</returns>
        public async Task<bool> ConnectAsync(string ipAddress, int port, int timeout = 300)
        {
            Logger.Debug("Starting connection process to PLC at {0}:{1} with timeout {2}ms.", ipAddress, port, timeout);
            try
            {
                Logger.Debug("Performing ping check to {0} with timeout {1}ms.", ipAddress, timeout);
                bool isPingSuccessful = await BasicClass.PingCheckAsync(ipAddress, timeout);
                if (!isPingSuccessful)
                {
                    throw new FinsCommunicationException($"Ping to PLC at {ipAddress} failed.");
                }
                Logger.Debug("Ping to PLC at {0} successful.", ipAddress);

                await BasicClass.ConnectAsync(ipAddress, port);
                Logger.Debug("TCP connection to PLC at {0}:{1} established successfully.", ipAddress, port);

                await BasicClass.SendDataAsync(FinsCommandBuilder.HandShake());
                Logger.Debug("Handshake command sent to PLC.");

                byte[] buffer = new byte[24];
                int bytesRead = await BasicClass.ReceiveDataAsync(buffer);

                if (bytesRead < 24)
                {
                    Logger.Error("Incomplete handshake response from PLC. Expected at least 24 bytes, received {0} bytes.", bytesRead);
                    throw new FinsError(0xFF, 0xFF, "Incomplete handshake response from PLC.");
                }

                if (buffer[15] != 0)
                {
                    Logger.Error("Handshake failed with error code: {0:X2}{1:X2}.", buffer[15], buffer[16]);
                    throw new FinsError(buffer[15], buffer[16], "Handshake failed with error code.");
                }

                BasicClass.PCNode = buffer[19];
                BasicClass.PLCNode = buffer[23];
                Logger.Debug("PLC Nodes set successfully. PC Node: {0}, PLC Node: {1}.", BasicClass.PCNode, BasicClass.PLCNode);

                Logger.Info("Connection to PLC at {0}:{1} established successfully.", ipAddress, port);
                return true;
            }
            catch (FinsCommunicationException ex)
            {
                Logger.Fatal(ex, "Critical communication error while connecting to PLC at {0}:{1}.", ipAddress, port);
                return false;
            }
            catch (FinsError ex)
            {
                Logger.Error(ex, "FINS protocol error while connecting to PLC at {0}:{1}.", ipAddress, port);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "Unexpected error while connecting to PLC at {0}:{1}.", ipAddress, port);
                return false;
            }
        }


        /// <summary>
        /// Closes the TCP connection to the PLC.
        /// </summary>
        /// <returns>A task that represents the asynchronous close operation.</returns>
        public async Task CloseAsync()
        {
            Logger.Debug("Starting to close TCP connection to PLC.");

            try
            {
                Logger.Debug("Executing disconnect operation.");
                await Task.Run(() =>
                {
                    BasicClass.Disconnect();
                });
                Logger.Info("TCP connection to PLC closed successfully.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred while closing TCP connection to PLC.");
            }
        }


        /// <summary>
        /// Reads a single word from the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to read from.</param>
        /// <param name="address">The starting address.</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the read word.</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task<short> ReadWordAsync(PlcMemory memory, ushort address)
        {
            short[] result = await ReadWordsAsync(memory, address, 1);
            return result[0];
        }

        /// <summary>
        /// Writes a single word to the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to write to.</param>
        /// <param name="address">The starting address.</param>
        /// <param name="data">The word to write.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task WriteWordAsync(PlcMemory memory, ushort address, short data)
        {
            short[] dataArray = new short[] { data };
            await WriteWordsAsync(memory, address, dataArray);
        }

        /// <summary>
        /// Reads the state of a single bit from the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to read from.</param>
        /// <param name="address">The bit address in the format "000.00" (e.g., "100.5").</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the bit state (0 or 1).</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task<short> GetBitStateAsync(PlcMemory memory, string address)
        {
            short bs = 0;
            byte[] buffer = new byte[31]; // Buffer size can configure to necessary size
            short cnInt = short.Parse(address.Split('.')[0]);
            short cnBit = short.Parse(address.Split('.')[1]);

            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Bit, cnInt, cnBit, 1);
            await BasicClass.SendDataAsync(command);

            int bytesRead = await BasicClass.ReceiveDataAsync(buffer);
            if (bytesRead < 31)
            {
                Logger.Error("Incomplete bit read response from PLC. Expected at least 31 bytes, received {0} bytes.", bytesRead);
                throw new FinsError(0xFF, 0xFF, $"Incomplete bit read response from PLC. Expected at least 31 bytes, received {bytesRead} bytes.");
            }

            // Check for errors
            CheckAndThrowErrors(buffer);

            // Data parsing
            bs = (short)buffer[30];
            return bs;
        }

        /// <summary>
        /// Sets the state of a single bit in the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to write to.</param>
        /// <param name="address">The bit address in the format "000.00" (e.g., "100.5").</param>
        /// <param name="state">The desired bit state (0 or 1).</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task SetBitStateAsync(PlcMemory memory, string address, BitState state)
        {
            byte[] buffer = new byte[30];
            short cnInt = short.Parse(address.Split('.')[0]);
            short cnBit = short.Parse(address.Split('.')[1]);

            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Write, memory, MemoryType.Bit, cnInt, cnBit, 1);
            byte[] fullCommand = new byte[command.Length + 1];
            Buffer.BlockCopy(command, 0, fullCommand, 0, command.Length);
            fullCommand[command.Length] = (byte)state;

            await BasicClass.SendDataAsync(fullCommand);

            int bytesRead = await BasicClass.ReceiveDataAsync(buffer);
            if (bytesRead < 30)
            {
                Logger.Error("Incomplete bit write response from PLC. Expected at least 30 bytes, received {0} bytes.", bytesRead);
                throw new FinsError(0xFF, 0xFF, $"Incomplete bit write response from PLC. Expected at least 30 bytes, received {bytesRead} bytes.");
            }

            // Check for errors
            CheckAndThrowErrors(buffer);
        }

        /// <summary>
        /// Reads a single real (float) value from the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to read from.</param>
        /// <param name="address">The starting address (reads two consecutive words).</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the read float value.</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task<float> ReadRealAsync(PlcMemory memory, ushort address)
        {
            byte[] buffer = new byte[34]; // 30 + 4 (2 words)
            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Read, memory, MemoryType.Word, (short)address, 0, 2);
            await BasicClass.SendDataAsync(command);

            int bytesRead = await BasicClass.ReceiveDataAsync(buffer);
            if (bytesRead < 34)
            {
                Logger.Error("Incomplete real read response from PLC. Expected at least 34 bytes, received {0} bytes.", bytesRead);
                throw new FinsError(0xFF, 0xFF, $"Incomplete real read response from PLC. Expected at least 34 bytes, received {bytesRead} bytes.");
            }

            // Check for errors
            CheckAndThrowErrors(buffer);

            // Data parsing
            byte[] temp = new byte[] { buffer[31], buffer[30], buffer[33], buffer[32] }; // Right order
            float reData = BitConverter.ToSingle(temp, 0);
            return reData;
        }

        /// <summary>
        /// Checks for errors in the response buffer and throws appropriate exceptions.
        /// </summary>
        /// <param name="buffer">The response buffer from the PLC.</param>
        /// <exception cref="FinsError">Thrown when an error is detected in the response.</exception>
        private void CheckAndThrowErrors(byte[] buffer)
        {
            // Checks for head error
            HeadErrorCode headError = ErrorCode.CheckHeadError(buffer[11]);
            if (headError == HeadErrorCode.CommandNotSupported)
            {
                headError = ErrorCode.CheckHeadError(buffer[15]);
                if (headError != HeadErrorCode.Success)
                {
                    Logger.Error("Head Error detected: {0}. Error Code: {1} or {2}", headError, buffer[11], buffer[15]);
                    throw new FinsError(mainCode: buffer[11], subCode: buffer[15], $"Head Error: {headError}");
                }
            }

            // Checks for end error
            FinsError endError = ErrorCode.CheckEndCode(mainCode: buffer[28], subCode: buffer[29]);
            if (endError != null)
            {
                if (endError.CanContinue)
                {
                    Logger.Warn("End Error detected but operation can continue: {0}", endError);
                }
                else
                {
                    Logger.Error("End Error detected: {0}", endError);
                    throw new FinsError(endError.MainCode, endError.SubCode, $"End Error: {endError}");
                }
            }
        }


        /// <summary>
        /// Reads multiple words from the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to read from.</param>
        /// <param name="address">The starting address.</param>
        /// <param name="count">The number of words to read.</param>
        /// <returns>A task that represents the asynchronous read operation. The task result contains the read data.</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task<short[]> ReadWordsAsync(PlcMemory memory, ushort address, ushort count)
        {
            byte[] command = FinsCommandBuilder.FinsCmd(rw: ReadOrWrite.Read, mr: memory, mt: MemoryType.Word, startAdress: (short)address, offset: 0, count: (short)count);
            await BasicClass.SendDataAsync(command);

            byte[] buffer = new byte[30 + count * 2];
            int bytesRead = await BasicClass.ReceiveDataAsync(buffer);
            if (bytesRead < 30 + count * 2)
            {
                Logger.Error("Incomplete read response from PLC. Expected at least {0} bytes, received {1} bytes.", 30 + count * 2, bytesRead);
                throw new FinsError(0xFF, 0xFF, $"Incomplete read response from PLC. Expected at least {30 + count * 2} bytes, received {bytesRead} bytes.");
            }

            // Check for errors 
            CheckAndThrowErrors(buffer);

            // Data parsing
            short[] reData = new short[count];
            for (int i = 0; i < count; i++)
            {
                int index = 30 + i * 2;
                reData[i] = BitConverter.ToInt16(new byte[] { buffer[index + 1], buffer[index] }, 0);
            }

            return reData;
        }

        /// <summary>
        /// Writes multiple words to the PLC asynchronously.
        /// </summary>
        /// <param name="memory">The PLC memory area to write to.</param>
        /// <param name="address">The starting address.</param>
        /// <param name="data">The data to write.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="FinsError">Thrown when the PLC returns an error.</exception>
        public async Task WriteWordsAsync(PlcMemory memory, ushort address, short[] data)
        {
            byte[] wdata = new byte[data.Length * 2];
            for (int i = 0; i < data.Length; i++)
            {
                byte[] temp = BitConverter.GetBytes(data[i]);
                wdata[i * 2] = temp[1]; // High byte first
                wdata[i * 2 + 1] = temp[0]; // Low byte
            }

            byte[] command = FinsCommandBuilder.FinsCmd(ReadOrWrite.Write, memory, MemoryType.Word, (short)address, 0, (short)data.Length);
            byte[] fullCommand = new byte[command.Length + wdata.Length];
            Buffer.BlockCopy(command, 0, fullCommand, 0, command.Length);
            Buffer.BlockCopy(wdata, 0, fullCommand, command.Length, wdata.Length);

            await BasicClass.SendDataAsync(fullCommand);

            byte[] buffer = new byte[30];
            int bytesRead = await BasicClass.ReceiveDataAsync(buffer);
            if (bytesRead < 30)
            {
                Logger.Error("Incomplete write response from PLC. Expected at least 30 bytes, received {0} bytes.", bytesRead);
                throw new FinsError(0xFF, 0xFF, $"Incomplete write response from PLC. Expected at least 30 bytes, received {bytesRead} bytes.");
            }

            // Check for errors
            CheckAndThrowErrors(buffer);
        }



        /// <summary>
        /// Releases all resources used by the <see cref="EthernetPlcClient"/>.
        /// </summary>
        public void Dispose()
        {
            BasicClass.Disconnect();
        }
    }
}
