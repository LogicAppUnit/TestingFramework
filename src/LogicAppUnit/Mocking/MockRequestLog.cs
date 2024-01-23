using System.Collections.Generic;

namespace LogicAppUnit.Mocking
{
    internal class MockRequestLog : MockRequest
    {
        /// <summary>
        /// A log for request matching, this can be used to understand how requests are being matched, or not being matched!
        /// </summary>
        public List<string> Log { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockRequestLog"/> class.
        /// </summary>
        public MockRequestLog()
        {
            Log = new List<string>();
        }
    }
}
