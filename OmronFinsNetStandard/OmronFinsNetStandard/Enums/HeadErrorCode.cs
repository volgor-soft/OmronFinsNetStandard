using System;
using System.Collections.Generic;
using System.Text;

namespace OmronFinsNetStandard.Enums
{
    /// <summary>
    /// Represents the error codes returned in the head of the FINS response.
    /// </summary>
    public enum HeadErrorCode
    {
        /// <summary>
        /// No error.
        /// </summary>
        Success = 0x00,

        /// <summary>
        /// The head is not 'FINS'.
        /// </summary>
        InvalidHead = 0x01,

        /// <summary>
        /// The data length is too long.
        /// </summary>
        DataLengthTooLong = 0x02,

        /// <summary>
        /// The command is not supported.
        /// </summary>
        CommandNotSupported = 0x03,

        /// <summary>
        /// Unknown error.
        /// </summary>
        Unknown = 0xFF
    }
}
