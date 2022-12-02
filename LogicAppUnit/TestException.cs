using System;

namespace LogicAppUnit
{
    /// <summary>
    /// Represents errors that occur within the testing framework.
    /// </summary>
    public class TestException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestException"/> class.
        /// </summary>
        public TestException() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The error message.</param>
        public TestException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestException"/> class with a specified error message and a reference to the inner exception that is the cause of the exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="inner">The inner exception.</param>
        public TestException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
