using System;
using System.Collections.Generic;
using System.Net.Http;

namespace LogicAppUnit
{
    /// <summary>
    /// Represents a request that was sent from a workflow and received by the mock test server.
    /// </summary>
    public class MockRequest
    {
        /// <summary>
        /// The timestamp for the request, in local time.
        /// </summary>
        /// <remarks>
        /// Use local time because (i) this is more meaningful to a developer when they are not in UTC, and (ii) this value is only going to be used in the content of the test execution
        /// which lasts no more than a few minutes at most.
        /// </remarks>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// The name of the request, this is based on the name of the API that was called.
        /// </summary>
        /// <remarks>
        /// The request URI will not be unique in the collection of mock requests when the same API endpoint is called multiple times.
        /// </remarks>
        public Uri RequestUri { get; set; }

        /// <summary>
        /// The HTTP method for the request.
        /// </summary>
        public HttpMethod Method { get; set; }

        /// <summary>
        /// The set of headers for the request.
        /// </summary>
        public Dictionary<string, IEnumerable<string>> Headers { get; set; }

        /// <summary>
        /// The content of the request, as a <see cref="string"/> value.
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// The set of content headers for the request.
        /// </summary>
        public Dictionary<string, IEnumerable<string>> ContentHeaders { get; set; }
    }
}
