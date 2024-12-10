using System;

namespace OmronFinsNetStandard.Errors
{
    /// <summary>
    /// Represents errors that occur during communication with the PLC using the FINS protocol.
    /// </summary>
    public class FinsCommunicationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FinsCommunicationException"/> class.
        /// </summary>
        public FinsCommunicationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsCommunicationException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FinsCommunicationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsCommunicationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public FinsCommunicationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
