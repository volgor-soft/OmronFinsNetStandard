using OmronFinsNetStandard.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace OmronFinsNetStandard.Interfaces
{
    public interface IFinsCommandBuilder
    {
        byte[] HandShake();
        byte[] FinsCmd(ReadOrWrite rw, PlcMemory mr, MemoryType mt, short ch, short offset, short cnt);
    }

}
