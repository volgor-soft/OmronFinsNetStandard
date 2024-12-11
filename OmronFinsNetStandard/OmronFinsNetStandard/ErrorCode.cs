using System;
using OmronFinsNetStandard.Enums;
using OmronFinsNetStandard.Errors;

namespace OmronFinsNetStandard
{
    /// <summary>
    /// Provides methods to handle and interpret error codes returned by the PLC.
    /// </summary>
    internal static class ErrorCode
    {
        /// <summary>
        /// Checks the head error code from the PLC response.
        /// </summary>
        /// <param name="code">The error code from the head.</param>
        /// <returns>A <see cref="HeadErrorCode"/> indicating the result of the check.</returns>
        public static HeadErrorCode CheckHeadError(byte code)
        {
            return code switch
            {
                0x00 => HeadErrorCode.Success,
                0x01 => HeadErrorCode.InvalidHead,
                0x02 => HeadErrorCode.DataLengthTooLong,
                0x03 => HeadErrorCode.CommandNotSupported,
                _ => HeadErrorCode.Unknown,
            };
        }

        /// <summary>
        /// Checks the end error codes from the PLC response.
        /// </summary>
        /// <param name="mainCode">The main error code.</param>
        /// <param name="subCode">The sub error code.</param>
        /// <returns>A <see cref="FinsError"/> containing error details if an error occurred; otherwise, <c>null</c>.</returns>
        public static FinsError CheckEndCode(byte mainCode, byte subCode)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return (mainCode, subCode) switch
            {
                #region Main Code 0x00
                // 00: Normal completion
                (0x00, 0x00) => null, // Success
                (0x00, 0x40) => new FinsError(mainCode, subCode, "Error code 0x40: Alarm generated in PLC, data can still be read.", canContinue: true),
                (0x00, 0x01) => new FinsError(mainCode, subCode, "Service was interrupted."),
                #endregion

                #region Main Code 0x01
                // 01: Local node error
                (0x01, 0x01) => new FinsError(mainCode, subCode, "Local node not part of Network."),
                (0x01, 0x02) => new FinsError(mainCode, subCode, "Token time-out, node number too large."),
                (0x01, 0x03) => new FinsError(mainCode, subCode, "Number of transmit retries exceeded."),
                (0x01, 0x04) => new FinsError(mainCode, subCode, "Maximum number of frames exceeded."),
                (0x01, 0x05) => new FinsError(mainCode, subCode, "Node number setting error (range)."),
                (0x01, 0x06) => new FinsError(mainCode, subCode, "Node number duplication error."),
                #endregion

                #region Main Code 0x02
                // 02: Destination node error
                (0x02, 0x01) => new FinsError(mainCode, subCode, "Destination node not part of Network."),
                (0x02, 0x02) => new FinsError(mainCode, subCode, "No node with the specified node number."),
                (0x02, 0x03) => new FinsError(mainCode, subCode, "Third node not part of Network.\nBroadcasting was specified."),
                (0x02, 0x04) => new FinsError(mainCode, subCode, "Busy error, destination node busy."),
                (0x02, 0x05) => new FinsError(mainCode, subCode, "Response time-out."),
                #endregion

                #region Main Code 0x03
                // 03: Communications controller error
                (0x03, 0x01) => new FinsError(mainCode, subCode, "Error occurred in the communications controller, ERC indicator is lit."),
                (0x03, 0x02) => new FinsError(mainCode, subCode, "CPU error occurred in the PC at the destination node."),
                (0x03, 0x03) => new FinsError(mainCode, subCode, "A controller error has prevented a normal response from being returned."),
                (0x03, 0x04) => new FinsError(mainCode, subCode, "Node number setting error."),
                #endregion

                #region Main Code 0x04
                // 04: Not executable
                (0x04, 0x01) => new FinsError(mainCode, subCode, "An undefined command has been used."),
                (0x04, 0x02) => new FinsError(mainCode, subCode, "Cannot process command because the specified unit model or version is wrong."),
                #endregion

                #region Main Code 0x05
                // 05: Routing error
                (0x05, 0x01) => new FinsError(mainCode, subCode, "Destination node number is not set in the routing table."),
                (0x05, 0x02) => new FinsError(mainCode, subCode, "Routing table isn’t registered."),
                (0x05, 0x03) => new FinsError(mainCode, subCode, "Routing table error."),
                (0x05, 0x04) => new FinsError(mainCode, subCode, "The maximum number of relay nodes (2) was exceeded in the command."),
                #endregion

                #region Main Code 0x10
                // 10: Command format error
                (0x10, 0x01) => new FinsError(mainCode, subCode, "The command is longer than the max. permissible length."),
                (0x10, 0x02) => new FinsError(mainCode, subCode, "The command is shorter than min. permissible length."),
                (0x10, 0x03) => new FinsError(mainCode, subCode, "The designated number of data items differs from the actual number."),
                (0x10, 0x04) => new FinsError(mainCode, subCode, "An incorrect command format has been used."),
                (0x10, 0x05) => new FinsError(mainCode, subCode, "An incorrect header has been used. (The local node’s relay table or relay node’s local network table is wrong.)"),
                #endregion

                #region Main Code 0x11
                // 11: Parameter error
                (0x11, 0x01) => new FinsError(mainCode, subCode, "A correct memory area code has not been used or Expansion Data Memory is not available."),
                (0x11, 0x02) => new FinsError(mainCode, subCode, "The access size specified in the command is wrong, or the first address is an odd number."),
                (0x11, 0x03) => new FinsError(mainCode, subCode, "The first address is in an inaccessible area."),
                (0x11, 0x04) => new FinsError(mainCode, subCode, "The end of specified word range exceeds the acceptable range."),
                (0x11, 0x06) => new FinsError(mainCode, subCode, "A non-existent program no. has been specified."),
                (0x11, 0x09) => new FinsError(mainCode, subCode, "The sizes of data items in the command block are wrong."),
                (0x11, 0x0A) => new FinsError(mainCode, subCode, "The IOM break function cannot be executed because it is already being executed."),
                (0x11, 0x0B) => new FinsError(mainCode, subCode, "The response block is longer than the max. permissible length."),
                (0x11, 0x0C) => new FinsError(mainCode, subCode, "An incorrect parameter code has been specified."),
                #endregion

                #region Main Code 0x20
                // 20: Read not possible
                (0x20, 0x02) => new FinsError(mainCode, subCode, "The data is protected.\nAn attempt was made to download a file that is being uploaded."),
                (0x20, 0x03) => new FinsError(mainCode, subCode, "The registered table does not exist or is incorrect.\nToo many files open."),
                (0x20, 0x04) => new FinsError(mainCode, subCode, "The corresponding search data does not exist."),
                (0x20, 0x05) => new FinsError(mainCode, subCode, "A non-existing program no. has been specified."),
                (0x20, 0x06) => new FinsError(mainCode, subCode, "A non-existing file has been specified."),
                (0x20, 0x07) => new FinsError(mainCode, subCode, "A verification error has occurred."),
                #endregion

                #region Main Code 0x21
                // 21: Write not possible
                (0x21, 0x01) => new FinsError(mainCode, subCode, "The specified area is read-only or is write-protected."),
                (0x21, 0x02) => new FinsError(mainCode, subCode, "The data is protected.\nAn attempt was made to simultaneously download and upload a file.\nThe data link table cannot be written manual because it is set for automatic generation."),
                (0x21, 0x03) => new FinsError(mainCode, subCode, "The number of files exceeds the maximum permissible.\nToo many files open."),
                (0x21, 0x05) => new FinsError(mainCode, subCode, "A non-existing program no. has been specified."),
                (0x21, 0x06) => new FinsError(mainCode, subCode, "A non-existent file has been specified."),
                (0x21, 0x07) => new FinsError(mainCode, subCode, "The specified file already exists."),
                (0x21, 0x08) => new FinsError(mainCode, subCode, "Data cannot be changed."),
                #endregion

                #region Main Code 0x22
                // 22: Not executable in current mode
                (0x22, 0x01) => new FinsError(mainCode, subCode, "The mode is wrong (executing).\nData links are active."),
                (0x22, 0x02) => new FinsError(mainCode, subCode, "The mode is wrong (stopped).\nData links are active."),
                (0x22, 0x03) => new FinsError(mainCode, subCode, "Wrong mode. The PC is in the PROGRAM mode."),
                (0x22, 0x04) => new FinsError(mainCode, subCode, "Wrong mode. The PC is in the DEBUG mode."),
                (0x22, 0x05) => new FinsError(mainCode, subCode, "Wrong mode. The PC is in the MONITOR mode."),
                (0x22, 0x06) => new FinsError(mainCode, subCode, "Wrong mode. The PC is in the RUN mode."),
                (0x22, 0x07) => new FinsError(mainCode, subCode, "The specified node is not the control node."),
                (0x22, 0x08) => new FinsError(mainCode, subCode, "The mode is wrong and the step cannot be executed."),
                #endregion

                #region Main Code 0x23
                // 23: No unit
                (0x23, 0x01) => new FinsError(mainCode, subCode, "A file device does not exist where specified."),
                (0x23, 0x02) => new FinsError(mainCode, subCode, "The specified memory does not exist."),
                (0x23, 0x03) => new FinsError(mainCode, subCode, "No clock exists."),
                #endregion

                #region Main Code 0x24
                // 24: Start/stop not possible
                (0x24, 0x01) => new FinsError(mainCode, subCode, "The data link table either hasn’t been created or is incorrect."),
                #endregion

                #region Main Code 0x25
                // 25: Unit error
                (0x25, 0x02) => new FinsError(mainCode, subCode, "Parity/checksum error occurred because of incorrect data"),
                (0x25, 0x03) => new FinsError(mainCode, subCode, "I/O setting error (The registered I/O configuration differs from the actual.)"),
                (0x25, 0x04) => new FinsError(mainCode, subCode, "Too many I/O points."),
                (0x25, 0x05) => new FinsError(mainCode, subCode, "CPU bus error (An error occurred during data transfer between the CPU and a CPU Bus Unit.)"),
                (0x25, 0x06) => new FinsError(mainCode, subCode, "I/O duplication error (A rack number, unit number, or I/O word allocation has been duplicated.)"),
                (0x25, 0x07) => new FinsError(mainCode, subCode, "I/O bus error (An error occurred during data transfer between the CPU and an I/O Unit.)"),
                (0x25, 0x09) => new FinsError(mainCode, subCode, "SYSMAC BUS/2 error (An error occurred during SYSMAC BUS/2 data transfer.)"),
                (0x25, 0x0A) => new FinsError(mainCode, subCode, "Special I/O Unit error (An error occurred during CPU Bus Unit data transfer.)"),
                (0x25, 0x0D) => new FinsError(mainCode, subCode, "Duplication in SYSMAC BUS word allocation."),
                (0x25, 0x0F) => new FinsError(mainCode, subCode, "A memory error has occurred in internal memory, in the Memory Card, or in Expansion DM during the error check."),
                (0x25, 0x10) => new FinsError(mainCode, subCode, "Terminator not connected in SYSMAC BUS System."),
                #endregion

                #region Main Code 0x26
                // 26: Command error
                (0x26, 0x01) => new FinsError(mainCode, subCode, "The specified area is not protected. This response code will be returned if an attempt is made to clear protection on an area that is not protected."),
                (0x26, 0x02) => new FinsError(mainCode, subCode, "An incorrect password has been specified."),
                (0x26, 0x04) => new FinsError(mainCode, subCode, "The specified area is protected.\nTo many commands at destination."),
                (0x26, 0x05) => new FinsError(mainCode, subCode, "The service is being executed."),
                (0x26, 0x06) => new FinsError(mainCode, subCode, "The service is not being executed."),
                (0x26, 0x07) => new FinsError(mainCode, subCode, "Service cannot be executed from local node because the local node is not part of the data link.\nA buffer error has prevented returning a normal response."),
                (0x26, 0x08) => new FinsError(mainCode, subCode, "Service cannot be executed because necessary settings haven’t been made."),
                (0x26, 0x09) => new FinsError(mainCode, subCode, "Service cannot be executed because necessary settings haven’t been made in the command data."),
                (0x26, 0x0A) => new FinsError(mainCode, subCode, "The specified action or transition number has already been registered."),
                (0x26, 0x0B) => new FinsError(mainCode, subCode, "Cannot clear error because the cause of the error still exists."),
                #endregion

                #region Main Code 0x30
                // 30: Access right error
                (0x30, 0x01) => new FinsError(mainCode, subCode, "The access right is held by another device."),
                #endregion

                #region Main Code 0x40
                // 40: Abort
                (0x40, 0x01) => new FinsError(mainCode, subCode, "Command was aborted with ABORT\r\ncommand."),
                #endregion

                #region Main Code Unknown
                _ => new FinsError(mainCode, subCode, "Unknown exception."),
                #endregion
            };
#pragma warning restore CS8603 // Possible null reference return.
        }
    }
}
