using OmronFinsNetStandard.Enums;

namespace OmronFinsNetStandard
{
    class FinsCommandBuilder
    {
        private static byte GetMemoryCode(PlcMemory memory, MemoryType memoryType)
        {
            return (byte)(memoryType == MemoryType.Bit
                ? memory switch
                {
                    PlcMemory.CIO => 0x30, PlcMemory.WR => 0x31, PlcMemory.HR => 0x32, PlcMemory.AR => 0x33, PlcMemory.DM => 0x02, _ => 0x00
                }
                : memory switch
                {
                    PlcMemory.CIO => 0xB0, PlcMemory.WR => 0xB1, PlcMemory.HR => 0xB2, PlcMemory.AR => 0xB3, PlcMemory.DM => 0x82, _ => 0x00
                });
        }

        public static byte[] HandShake()
        {
            byte[] array = new byte[20];
            array[0] = 0x46;
            array[1] = 0x49;
            array[2] = 0x4E;
            array[3] = 0x53;
            array[4] = 0x00;
            array[5] = 0x00;
            array[6] = 0x00;
            array[7] = 0x0C;
            array[8] = 0x00;
            array[9] = 0x00;
            array[10] = 0x00;
            array[11] = 0x00;
            array[12] = 0x00;
            array[13] = 0x00;
            array[14] = 0x00;
            array[15] = 0x00;
            array[16] = 0x00;
            array[17] = 0x00;
            array[18] = 0x00;
            array[19] = 0x00;
            return array;
        }

        public static byte[] FinsCmd(ReadOrWrite rw, PlcMemory mr, MemoryType mt, short startAdress, short offset, short count, byte plcNode,
            byte pcNode)
        {
            byte[] array = new byte[34];

            // Header FINS
            array[0] = 0x46;
            array[1] = 0x49;
            array[2] = 0x4E;
            array[3] = 0x53;
            array[4] = 0x00;
            array[5] = 0x00;

            if (rw == ReadOrWrite.Read)
            {
                array[6] = 0x00;
                array[7] = 0x1A;
            }
            else
            {
                if (mt == MemoryType.Word)
                {
                    array[6] = (byte)((count * 2 + 26) / 256);
                    array[7] = (byte)((count * 2 + 26) % 256);
                }
                else
                {
                    array[6] = 0x00;
                    array[7] = 0x1B;
                }
            }

            array[8] = 0x00;
            array[9] = 0x00;
            array[10] = 0x00;
            array[11] = 0x02;
            array[12] = 0x00;
            array[13] = 0x00;
            array[14] = 0x00;
            array[15] = 0x00;

            // Command Frame Header
            array[16] = 0x80; // ICF
            array[17] = 0x00; // RSV
            array[18] = 0x02; // GCT
            array[19] = 0x00; // DNA

            array[20] = plcNode; // DA1
            array[21] = 0x00;
            array[22] = 0x00;
            array[23] = pcNode; // SA1

            array[24] = 0x00;
            array[25] = 0xFF;

            // Command Code
            if (rw == ReadOrWrite.Read)
            {
                array[26] = 0x01;
                array[27] = 0x01;
            }
            else
            {
                array[26] = 0x01;
                array[27] = 0x02;
            }

            // Memory Address
            array[28] = GetMemoryCode(mr, mt);
            array[29] = (byte)(startAdress / 256);
            array[30] = (byte)(startAdress % 256);
            array[31] = (byte)offset;

            array[32] = (byte)(count / 256);
            array[33] = (byte)(count % 256);

            return array;
        }
    }
}