using System;
using System.Threading.Tasks;
using OmronFinsNetStandard.Enums;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    /// <summary>
    /// Provides methods to generate FINS commands for reading and writing data to the PLC.
    /// </summary>
    internal class FinsCommandBuilder
    {
        private readonly BasicClass _basic;

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsCommandBuilder"/> class.
        /// </summary>
        /// <param name="basic">An instance of <see cref="BasicClass"/> for network operations.</param>
        public FinsCommandBuilder(BasicClass basic)
        {
            _basic = basic ?? throw new ArgumentNullException(nameof(basic));
        }

        /// <summary>
        /// Retrieves the memory area code based on the specified <see cref="PlcMemory"/> and <see cref="MemoryType"/>.
        /// </summary>
        /// <param name="memory">The PLC memory area.</param>
        /// <param name="memoryType">The type of memory access (bit or word).</param>
        /// <returns>The corresponding memory area code as a byte.</returns>
        private byte GetMemoryCode(PlcMemory memory, MemoryType memoryType)
        {
            return (byte)(memoryType == MemoryType.Bit
                ? memory switch
                {
                    PlcMemory.CIO => 0x30,
                    PlcMemory.WR => 0x31,
                    PlcMemory.HR => 0x32,
                    PlcMemory.AR => 0x33,
                    PlcMemory.DM => 0x02,
                    _ => 0x00,
                }
                : memory switch
                {
                    PlcMemory.CIO => 0xB0,
                    PlcMemory.WR => 0xB1,
                    PlcMemory.HR => 0xB2,
                    PlcMemory.AR => 0xB3,
                    PlcMemory.DM => 0x82,
                    _ => 0x00,
                });
        }

        /// <summary>
        /// Generates a handshake command to establish a connection with the PLC.
        /// </summary>
        /// <returns>A byte array representing the handshake command.</returns>
        public byte[] HandShake()
        {
            byte[] array = new byte[20];
            array[0] = 0x46; // 'F'
            array[1] = 0x49; // 'I'
            array[2] = 0x4E; // 'N'
            array[3] = 0x53; // 'S'

            array[4] = 0x00; // Command length high byte
            array[5] = 0x00; // Command length low byte
            array[6] = 0x00; // Sequence number high byte
            array[7] = 0x0C; // Sequence number low byte

            array[8] = 0x00; // Frame command
            array[9] = 0x00; // Frame command
            array[10] = 0x00; // Frame command
            array[11] = 0x00; // Frame command

            array[12] = 0x00; // Error code high byte
            array[13] = 0x00; // Error code low byte
            array[14] = 0x00; // Error code high byte
            array[15] = 0x00; // Error code low byte

            array[16] = 0x00; // Command option
            array[17] = 0x00; // Command option
            array[18] = 0x00; // Command option
            array[19] = 0x00; // Command option

            return array;
        }

        /// <summary>
        /// Generates a FINS command for reading or writing data.
        /// </summary>
        /// <param name="rw">The read or write operation type.</param>
        /// <param name="mr">The PLC memory area type.</param>
        /// <param name="mt">The memory access type (bit or word).</param>
        /// <param name="ch">The starting address.</param>
        /// <param name="offset">The bit offset (for bit access) or 0 (for word access).</param>
        /// <param name="cnt">The number of items to read or write.</param>
        /// <returns>A byte array representing the FINS command.</returns>
        public byte[] FinsCmd(ReadOrWrite rw, PlcMemory mr, MemoryType mt, short ch, short offset, short cnt)
        {
            // Get the command length
            // I haven't read enough of the documentation to fully implement this part.
            //int commandLength = rw == ReadOrWrite.Read ? 34 : 34 + (mt == MemoryType.Word ? cnt * 2 : cnt);
            //byte[] array = new byte[commandLength];
            byte[] array = new byte[34];

            // Command header
            array[0] = 0x46; // 'F'
            array[1] = 0x49; // 'I'
            array[2] = 0x4E; // 'N'
            array[3] = 0x53; // 'S'

            array[4] = 0x00; // Command length high byte
            array[5] = 0x00; // Command length low byte

            // Get the command length for read or write
            if (rw == ReadOrWrite.Read)
            {
                array[6] = 0x00;
                array[7] = 0x1A; // 26 byte for read
            }
            else
            {
                if (mt == MemoryType.Word)
                {
                    array[6] = (byte)((cnt * 2 + 26) / 256);
                    array[7] = (byte)((cnt * 2 + 26) % 256);
                }
                else
                {
                    array[6] = 0x00;
                    array[7] = 0x1B; // 27 byte for write
                }
            }

            // Frame command
            array[8] = 0x00;
            array[9] = 0x00;
            array[10] = 0x00;
            array[11] = 0x02;

            // Error code
            array[12] = 0x00;
            array[13] = 0x00;
            array[14] = 0x00;
            array[15] = 0x00;

            // Command frame header
            array[16] = 0x80; // ICF
            array[17] = 0x00; // RSV
            array[18] = 0x02; // GCT
            array[19] = 0x00; // DNA

            array[20] = _basic.PLCNode; // DA1
            array[21] = 0x00; // DA2, CPU unit
            array[22] = 0x00; // SNA, local network
            array[23] = _basic.PCNode; // SA1

            array[24] = 0x00; // SA2, CPU unit
            array[25] = 0xFF; // SID

            // Command code
            if (rw == ReadOrWrite.Read)
            {
                array[26] = 0x01; // Command Code for Read 0101
                array[27] = 0x01;
            }
            else
            {
                array[26] = 0x01; // Command Code for Write 0102
                array[27] = 0x02;
            }

            // Memory address
            array[28] = GetMemoryCode(mr, mt);
            array[29] = (byte)(ch / 256); // Address high byte
            array[30] = (byte)(ch % 256); // Address low byte
            array[31] = (byte)offset; // Bit offset або 0 для word

            array[32] = (byte)(cnt / 256); // Count high byte
            array[33] = (byte)(cnt % 256); // Count low byte

            //// Additional data for write operation
            //if (rw == ReadOrWrite.Write && mt == MemoryType.Word && cnt > 0)
            //{
            //    // For example, adding data after the main packet
            //    // It depends on the FINS specification
            //    // Example:
            //    // Array.Copy(data, 0, array, 34, data.Length);
            //}

            return array;
        }
    }
}
