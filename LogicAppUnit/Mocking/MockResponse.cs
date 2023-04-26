using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// A Mocked response consisting of a request matcher and a corresponding response builder.
    /// </summary>
    public class MockResponse : IMockResponse
    {
        private readonly MockRequestMatcher _mockRequestMatcher;
        private MockResponseBuilder _mockResponseBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockResponse"/> class using a request matcher.
        /// </summary>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        internal MockResponse(IMockRequestMatcher mockRequestMatcher)
        {
            if (mockRequestMatcher == null)
                throw new ArgumentNullException(nameof(mockRequestMatcher));

            _mockRequestMatcher = (MockRequestMatcher)mockRequestMatcher;
        }

        /// <inheritdoc cref="IMockResponse.RespondWith(IMockResponseBuilder)" />
        public void RespondWith(IMockResponseBuilder mockResponseBuilder)
        {
            if (mockResponseBuilder == null)
                throw new ArgumentNullException(nameof(mockResponseBuilder));

            _mockResponseBuilder = (MockResponseBuilder)mockResponseBuilder;
        }

        /// <inheritdoc cref="IMockResponse.RespondWithDefault()" />
        public void RespondWithDefault()
        {
            _mockResponseBuilder = (MockResponseBuilder)MockResponseBuilder.Create();
        }

        /// <summary>
        /// Match a HTTP request with a request matcher and create a response if there is a match.
        /// </summary>
        /// <param name="request">The HTTP request to be matched.</param>
        /// <param name="requestMatchingLog">Request matching log.</param>
        /// <returns>The response for the matching request, or <c>null</c> if there was no match.</returns>
        internal async Task<HttpResponseMessage> MatchRequestAndCreateResponseAsync(HttpRequestMessage request, List<string> requestMatchingLog)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (_mockRequestMatcher == null)
                throw new TestException("A request matcher has not been configured");
            if (_mockResponseBuilder == null)
                throw new TestException("A response builder has not been configured - use RespondWith() to create a response, or RespondWithDefault() to create a default response using a status code of 200 (OK) and no content");

            if (_mockRequestMatcher.MatchRequest(request))
            {
                requestMatchingLog.Add("    Matched");

                // Implement a delay if it is configured in the builder
                if (_mockResponseBuilder.Delay.HasValue && _mockResponseBuilder.Delay.Value.TotalMilliseconds > 0)
                {
                    requestMatchingLog.Add($"    Delay for {_mockResponseBuilder.Delay.Value.TotalMilliseconds} milliseconds");
                    await Task.Delay(_mockResponseBuilder.Delay.Value).ConfigureAwait(false);
                }

                return _mockResponseBuilder.BuildResponse(request);
            }
            else
            {
                requestMatchingLog.Add("    Not matched");
                return null;
            }
        }
    }
}
