using OmronFinsNetStandard.Enums;

namespace OmronFinsNetStandard.Errors
{
    /// <summary>
    /// Provides methods to handle and interpret error codes returned by the PLC.
    /// Separates TCP Wrapper errors (communication layer) from FINS Command errors (logic layer).
    /// </summary>
    internal static class ErrorCode
    {
        /// <summary>
        /// Checks the FINS/TCP Header Error Code (Byte 15 of the response).
        /// If this is not 0, the command was likely not processed by the PLC CPU.
        /// </summary>
        /// <param name="code">The error code from the TCP header (offset 15).</param>
        /// <returns>A <see cref="FinsError"/> if a wrapper error occurred; otherwise, <c>null</c>.</returns>
        public static FinsError? CheckTcpError(byte code)
        {
            return code switch
            {
                0x00 => null, // Success (Normal)
                0x01 => new FinsError(0x00, code, "FINS/TCP: The header is not 'FINS' (ASCII)."),
                0x02 => new FinsError(0x00, code, "FINS/TCP: The data length is too long."),
                0x03 => new FinsError(0x00, code, "FINS/TCP: The command is not supported."),

                // Connection/Session Errors (Critical for stability)
                0x20 => new FinsError(0x00, code, "FINS/TCP: All connections are in use."),
                0x21 => new FinsError(0x00, code, "FINS/TCP: The specified node is already connected."),
                0x22 => new FinsError(0x00, code, "FINS/TCP: Attempt to access a protected IP address."),
                0x23 => new FinsError(0x00, code, "FINS/TCP: The client FINS node address is out of range."),
                0x24 => new FinsError(0x00, code, "FINS/TCP: The same FINS node address is being used by the client and server."),
                0x25 => new FinsError(0x00, code, "FINS/TCP: No available node addresses."),

                _ => new FinsError(0x00, code, $"FINS/TCP: Unknown Header Error (0x{code:X2}).")
            };
        }

        /// <summary>
        /// Checks the head error code from the PLC response (Legacy/Enum support).
        /// </summary>
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
        /// Checks the end error codes (MRES/SRES) from the FINS Command body.
        /// </summary>
        public static FinsError CheckEndCode(byte mainCode, byte subCode)
        {
#pragma warning disable CS8603 // Possible null reference return.
            // Optimized switch expression for readability
            return (mainCode, subCode) switch
            {
                // --- 00: Normal Completion ---
                (0x00, 0x00) => null,
                (0x00, 0x40) => new FinsError(mainCode, subCode, "Alarm generated in PLC, data valid.", canContinue: true),
                (0x00, 0x01) => new FinsError(mainCode, subCode, "Service was interrupted."),

                // --- 01: Local Node Errors ---
                (0x01, 0x01) => new FinsError(mainCode, subCode, "Local node not part of Network."),
                (0x01, 0x02) => new FinsError(mainCode, subCode, "Token time-out."),
                (0x01, 0x03) => new FinsError(mainCode, subCode, "Retries exceeded."),
                (0x01, 0x04) => new FinsError(mainCode, subCode, "Max frames exceeded."),
                (0x01, 0x05) => new FinsError(mainCode, subCode, "Node number range error."),
                (0x01, 0x06) => new FinsError(mainCode, subCode, "Node number duplication."),

                // --- 02: Destination Node Errors ---
                (0x02, 0x01) => new FinsError(mainCode, subCode, "Dest. node not in Network."),
                (0x02, 0x02) => new FinsError(mainCode, subCode, "Node does not exist."),
                (0x02, 0x05) => new FinsError(mainCode, subCode, "Response time-out."),

                // --- 03: Controller Errors ---
                (0x03, 0x01) => new FinsError(mainCode, subCode, "Communications controller error."),
                (0x03, 0x02) => new FinsError(mainCode, subCode, "CPU error at destination."),
                (0x03, 0x03) => new FinsError(mainCode, subCode, "Controller error prevented response."),

                // --- 04: Not Executable ---
                (0x04, 0x01) => new FinsError(mainCode, subCode, "Undefined command."),
                (0x04, 0x02) => new FinsError(mainCode, subCode, "Wrong unit model/version."),

                // --- 05: Routing Errors ---
                (0x05, 0x01) => new FinsError(mainCode, subCode, "Dest. node not in routing table."),
                (0x05, 0x02) => new FinsError(mainCode, subCode, "Routing table not registered."),

                // --- 10: Format Errors ---
                (0x10, 0x01) => new FinsError(mainCode, subCode, "Command too long."),
                (0x10, 0x02) => new FinsError(mainCode, subCode, "Command too short."),
                (0x10, 0x03) => new FinsError(mainCode, subCode, "Data item count mismatch."),
                (0x10, 0x04) => new FinsError(mainCode, subCode, "Incorrect command format."),
                (0x10, 0x05) => new FinsError(mainCode, subCode, "Incorrect header (Relay table error)."),

                // --- 11: Parameter Errors (Very Common) ---
                (0x11, 0x01) => new FinsError(mainCode, subCode, "Memory area code error (No Expansion DM?)."),
                (0x11, 0x02) => new FinsError(mainCode, subCode, "Access size error or odd address."),
                (0x11, 0x03) => new FinsError(mainCode, subCode, "Address in inaccessible area."),
                (0x11, 0x04) => new FinsError(mainCode, subCode, "Address range exceeded."),
                (0x11, 0x0B) => new FinsError(mainCode, subCode, "Response too long (Max length exceeded)."),

                // --- 20: Read Impossible ---
                (0x20, 0x02) => new FinsError(mainCode, subCode, "Data protected (File upload/download conflict)."),
                (0x20, 0x05) => new FinsError(mainCode, subCode, "Program number does not exist."),

                // --- 21: Write Impossible ---
                (0x21, 0x01) => new FinsError(mainCode, subCode, "Area is Read-Only/Write-Protected."),
                (0x21, 0x02) => new FinsError(mainCode, subCode, "Data protected or Link active."),
                (0x21, 0x08) => new FinsError(mainCode, subCode, "Data cannot be changed."),

                // --- 22: Mode Errors ---
                (0x22, 0x01) => new FinsError(mainCode, subCode, "Wrong Mode: Executing."),
                (0x22, 0x03) => new FinsError(mainCode, subCode, "Wrong Mode: PLC is in PROGRAM mode."),
                (0x22, 0x04) => new FinsError(mainCode, subCode, "Wrong Mode: PLC is in DEBUG mode."),
                (0x22, 0x05) => new FinsError(mainCode, subCode, "Wrong Mode: PLC is in MONITOR mode."),
                (0x22, 0x06) => new FinsError(mainCode, subCode, "Wrong Mode: PLC is in RUN mode."),

                // --- 23: No Unit ---
                (0x23, 0x01) => new FinsError(mainCode, subCode, "File device missing."),
                (0x23, 0x02) => new FinsError(mainCode, subCode, "Memory does not exist."),

                // --- 25: Unit/System Errors ---
                (0x25, 0x02) => new FinsError(mainCode, subCode, "Parity/Checksum error."),
                (0x25, 0x05) => new FinsError(mainCode, subCode, "CPU Bus error."),
                (0x25, 0x0F) => new FinsError(mainCode, subCode, "Memory error (RAM/ROM/Card)."),

                // --- 26: Command Errors ---
                (0x26, 0x01) => new FinsError(mainCode, subCode, "Area not protected (Cannot clear protection)."),
                (0x26, 0x02) => new FinsError(mainCode, subCode, "Incorrect password."),
                (0x26, 0x05) => new FinsError(mainCode, subCode, "Service executing."),

                // --- 30: Access Rights ---
                (0x30, 0x01) => new FinsError(mainCode, subCode, "Access right held by another device."),

                // Default
                _ => new FinsError(mainCode, subCode, "Unknown FINS Error.")
            };
#pragma warning restore CS8603
        }
    }
}