using System;

namespace OmronFinsNetStandard.Errors
{
    /// <summary>
    /// Represents an error returned by the PLC in the FINS response.
    /// </summary>
    public class FinsError : Exception
    {
        /// <summary>
        /// The main error code.
        /// </summary>
        public byte MainCode { get; }

        /// <summary>
        /// The sub error code.
        /// </summary>
        public byte SubCode { get; }

        /// <summary>
        /// The description of the error.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Indicates whether the program can continue reading data from the PLC after this error.
        /// </summary>
        public bool CanContinue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsError"/> class with the specified codes, description, and continuation flag.
        /// </summary>
        /// <param name="mainCode">The main error code.</param>
        /// <param name="subCode">The sub error code.</param>
        /// <param name="description">The description of the error.</param>
        /// <param name="canContinue">Indicates whether the program can continue reading data from the PLC.</param>
        public FinsError(byte mainCode, byte subCode, string description, bool canContinue = false)
        {
            MainCode = mainCode;
            SubCode = subCode;
            Description = description;
            CanContinue = canContinue;
        }

        /// <summary>
        /// Returns a string that represents the current <see cref="FinsError"/>.
        /// </summary>
        /// <returns>A string that represents the current <see cref="FinsError"/>.</returns>
        public override string ToString()
        {
            return $"Error Code: {MainCode:X2}-{SubCode:X2}, Description: {Description}, Can Continue: {CanContinue}";
        }
    }
}
