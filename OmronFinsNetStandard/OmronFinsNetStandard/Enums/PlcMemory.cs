namespace OmronFinsNetStandard.Enums
{
    /// <summary>
    /// Represents the different memory areas in an Omron PLC using the FINS protocol.
    /// Each memory area can be accessed either by bit or by word, as specified by the <see cref="MemoryType"/> enumeration.
    /// </summary>
    public enum PlcMemory
    {
        /// <summary>
        /// Common Input/Output memory area.
        /// </summary>
        /// <remarks>
        /// <para>CIO Word Address: 0xB0</para>
        /// <para>CIO Bit Address: 0x30</para>
        /// </remarks>
        CIO,

        /// <summary>
        /// Work Relay memory area.
        /// </summary>
        /// <remarks>
        /// <para>WR Word Address: 0xB1</para>
        /// <para>WR Bit Address: 0x31</para>
        /// </remarks>
        WR,

        /// <summary>
        /// Holding Relay memory area.
        /// </summary>
        /// <remarks>
        /// <para>HR Word Address: 0xB2</para>
        /// <para>HR Bit Address: 0x32</para>
        /// </remarks>
        HR,

        /// <summary>
        /// Auxiliary Relay memory area.
        /// </summary>
        /// <remarks>
        /// <para>AR Word Address: 0xB3</para>
        /// <para>AR Bit Address: 0x33</para>
        /// </remarks>
        AR,

        /// <summary>
        /// Data Memory area.
        /// </summary>
        /// <remarks>
        /// <para>DM Word Address: 0x82</para>
        /// <para>DM Bit Address: 0x02</para>
        /// </remarks>
        DM
    }
}
