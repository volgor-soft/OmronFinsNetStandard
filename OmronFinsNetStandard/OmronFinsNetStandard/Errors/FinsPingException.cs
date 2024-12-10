using System;

namespace OmronFinsNetStandard.Errors
{
    /// <summary>
    /// Represents errors that occur during ping operations.
    /// </summary>
    public class FinsPingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FinsPingException"/> class.
        /// </summary>
        public FinsPingException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsPingException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FinsPingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsPingException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public FinsPingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
