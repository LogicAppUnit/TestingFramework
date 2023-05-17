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
        // TODO: (LOW) Could add a Verify() like moq!
        private readonly string _mockName;
        private readonly MockRequestMatcher _mockRequestMatcher;
        private MockResponseBuilder _mockResponseBuilder;

        internal string MockName
        {
            get => _mockName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockResponse"/> class using a request matcher.
        /// </summary>
        /// <param name="name">The name of the mock, or <c>null</c> if it does not have a name.</param>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        internal MockResponse(string name, IMockRequestMatcher mockRequestMatcher)
        {
            if (mockRequestMatcher == null)
                throw new ArgumentNullException(nameof(mockRequestMatcher));

            _mockName = name;
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

            MockRequestMatchResult matchResult = await _mockRequestMatcher.MatchRequestAsync(request);
            if (matchResult.IsMatch)
            {
                requestMatchingLog.Add("    Matched");
                await _mockResponseBuilder.ExecuteDelayAsync(requestMatchingLog);
                return _mockResponseBuilder.BuildResponse(request);
            }
            else
            {
                requestMatchingLog.Add($"    Not matched - {matchResult.MatchLog}");
                return null;
            }
        }
    }
}
