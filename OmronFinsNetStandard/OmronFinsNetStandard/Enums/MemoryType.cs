namespace OmronFinsNetStandard.Enums
{
    /// <summary>
    /// Specifies the type of access for a memory area, either by individual bits or by words.
    /// </summary>
    public enum MemoryType
    {
        /// <summary>
        /// Access the memory area by individual bits.
        /// </summary>
        Bit,

        /// <summary>
        /// Access the memory area by words (16-bit units).
        /// </summary>
        Word
    }
}
