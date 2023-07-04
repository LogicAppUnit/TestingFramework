﻿using LogicAppUnit.Helper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Handles the matching of mock requests and generates the corresponding mock response.
    /// </summary>
    internal class MockDefinition
    {
        // <c>true</c> if the mock request matching logs are to be written to the test execution logs, otherwise <c>false</c>.
        private readonly bool _writeMockRequestMatchingLogs;

        // Request matchers and response builders that are configured using the fluent API
        private readonly List<MockResponse> _mockResponses;

        // Delegate function that creates a response from a request
        private Func<HttpRequestMessage, HttpResponseMessage> _mockResponseDelegate;

        // A log of the requests sent to the mock test server that are generated by the workflow during its execution.
        // Use a ConcurrentBag to store the requests during the test execution to ensure thread safety of this collection
        private readonly ConcurrentBag<MockRequestLog> _mockRequestLog;
        private List<MockRequest> _mockRequestsAsList;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockDefinition"/> class.
        /// <param name="writeMockRequestMatchingLogs">Indicates if the mock request matching logs are to be written to the test execution logs.</param>
        /// </summary>
        public MockDefinition(bool writeMockRequestMatchingLogs)
        {
            _writeMockRequestMatchingLogs = writeMockRequestMatchingLogs;

            _mockResponses = new List<MockResponse>();
            _mockRequestLog = new ConcurrentBag<MockRequestLog>();

            // Hook up a default mock delegate
            _mockResponseDelegate = (request) => WrapMockResponseDelegate(request);
        }

        /// <summary>
        /// Gets the thread-safe static <see cref="System.Random"/> instance.
        /// </summary>
        internal static Random Random { get; } = Random.Shared;

        /// <summary>
        /// Configures a delegate function that creates a mocked response based on a request.
        /// </summary>
        public Func<HttpRequestMessage, HttpResponseMessage> MockResponseDelegate
        {
            set => _mockResponseDelegate = (request) => WrapMockResponseDelegate(request, value);
        }

        /// <summary>
        /// Add a mocked response, consisting of a request matcher and a corresponding response builder.
        /// </summary>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        /// <returns>The mocked response.</returns>
        public IMockResponse AddMockResponse(IMockRequestMatcher mockRequestMatcher)
        {
            return AddMockResponse(null, mockRequestMatcher);
        }

        /// <summary>
        /// Add a named mocked response, consisting of a request matcher and a corresponding response builder.
        /// </summary>
        /// <param name="name">Name of the mock.</param>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        /// <returns>The mocked response.</returns>
        public IMockResponse AddMockResponse(string name, IMockRequestMatcher mockRequestMatcher)
        {
            if (!string.IsNullOrEmpty(name) && _mockResponses.Where(x => x.MockName == name).Any())
                throw new ArgumentException($"A mock response with the name '{name}' already exists.");

            var mockResponse = new MockResponse(name, mockRequestMatcher);
            _mockResponses.Add(mockResponse);
            return mockResponse;
        }

        /// <summary>
        /// Gets the mock requests that were created by the workflow during the test execution.
        /// </summary>
        /// <remarks>
        /// The requests are ordered in chronological order, with the most recent request at the start of the list.
        /// </remarks>
        public List<MockRequest> MockRequests
        {
            get => _mockRequestsAsList;
        }

        /// <summary>
        /// Called when the test execution has completed.
        /// </summary>
        public void TestRunComplete()
        {
            // Copy the collection of mock requests from the thread-safe collection into a List that is accessible to the test case
            // The requests in the list are ordered in chronological order
            // The list is not thread-safe but this does not matter because the test case is not multi-threaded
            _mockRequestsAsList = _mockRequestLog.Select(x => (MockRequest)x).OrderBy(x => x.Timestamp).ToList();

            // Write the request logs to the test output
            if (!_mockRequestLog.IsEmpty)
            {
                Console.WriteLine("Mocked requests:");
                foreach (MockRequestLog req in _mockRequestLog.OrderBy(x => x.Timestamp))
                {
                    Console.WriteLine($"    {req.Timestamp:HH:mm:ss.fff}: {req.Method} {req.RequestUri.AbsoluteUri}");

                    if (_writeMockRequestMatchingLogs && req.Log.Count > 0)
                    {
                        Console.WriteLine("    Mocked request matching logs:");
                        req.Log.ForEach(s => Console.WriteLine("      " + s));
                    }
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("No mocked requests were logged");
            }
        }

        /// <summary>
        /// Match a HTTP request message and return the corresponding HTTP response message.
        /// </summary>
        /// <param name="request">The HTTP request message./</param>
        /// <returns>The HTTP response message.</returns>
        public async Task<HttpResponseMessage> MatchRequestAndBuildResponseAsync(HttpRequestMessage request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            // Cache the mock request to enable test assertions
            // Include anything that might be useful to the test author to validate the workflow
            var requestLog = new MockRequestLog
            {
                RequestUri = request.RequestUri,
                Method = request.Method,
                Headers = CopyHeaders(request.Headers),
                Content = await request.Content.ReadAsStringAsync(),
                ContentHeaders = CopyHeaders(request.Content.Headers)
            };
            _mockRequestLog.Add(requestLog);

            // Use fluent mock reponses first, then the mock response delegate
            if (_mockResponses.Count > 0)
            {
                requestLog.Log.Add($"Checking {_mockResponses.Count} mock request matchers:");
                HttpResponseMessage fluentResponse = await GetResponseUsingFluentMocksAsync(request, requestLog.Log);

                if (fluentResponse != null)
                    return fluentResponse;
            }
            else
            {
                requestLog.Log.Add("No mock request matchers have been configured");
            }

            requestLog.Log.Add("Running mock response delegate because no requests were matched");
            return GetResponseUsingDelegate(request, requestLog.Log);
        }

        #region Private methods

        /// <summary>
        /// Match a HTTP request message against the set of request matchers and return the corresponding HTTP response message.
        /// </summary>
        /// <param name="request">The HTTP request message./</param>
        /// <param name="requestMatchingLog">Request matching log.</param>
        /// <returns>The HTTP response message.</returns>
        private async Task<HttpResponseMessage> GetResponseUsingFluentMocksAsync(HttpRequestMessage request, List<string> requestMatchingLog)
        {
            var mockRequestCache = new MockRequestCache(request);
            HttpResponseMessage matchedResponse = null;
            int count = 0;

            foreach (MockResponse mockResp in _mockResponses)
            {
                requestMatchingLog.Add($"  Checking mock request matcher #{++count}" + (string.IsNullOrEmpty(mockResp.MockName) ? ":" : $" ({mockResp.MockName}):"));

                try
                {
                    matchedResponse = await mockResp.MatchRequestAndCreateResponseAsync(request, mockRequestCache, requestMatchingLog);
                }
                catch (Exception ex)
                {
                    // This exception will flow up to the Mock HTTP Server which will then return a HTTP 500 (Internal Server Error) to the workflow being tested
                    requestMatchingLog.Add($"    EXCEPTION: {ex.Message}");
                    throw;
                }

                if (matchedResponse != null)
                    break;
            }

            return matchedResponse;
        }

        /// <summary>
        /// Match a HTTP request message using the delegate function and return the corresponding HTTP response message.
        /// </summary>
        /// <param name="request">The HTTP request message./</param>
        /// <param name="requestMatchingLog">Request matching log.</param>
        /// <returns>The HTTP response message.</returns>
        private HttpResponseMessage GetResponseUsingDelegate(HttpRequestMessage request, List<string> requestMatchingLog)
        {
            try
            {
                return _mockResponseDelegate(request);
            }
            catch (Exception ex)
            {
                // This exception will flow up to the Mock HTTP Server which will then return a HTTP 500 (Internal Server Error) to the workflow being tested
                requestMatchingLog.Add($"    EXCEPTION: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Wrap the mock delegate defined in the test case with additional functionality.
        /// </summary>
        /// <param name="httpRequestMessage">Request message for the mocked API call.</param>
        /// <param name="mockDefinedInTestCase">Delegate function that sets the response message for the mocked API call.</param>
        /// <returns>The response message.</returns>
        private static HttpResponseMessage WrapMockResponseDelegate(HttpRequestMessage httpRequestMessage, Func<HttpRequestMessage, HttpResponseMessage> mockDefinedInTestCase = null)
        {
            // Wire up the archive mock
            // THIS WILL BE REMOVED IN A FUTURE VERSION
            if (httpRequestMessage.RequestUri.AbsolutePath.Contains("Archive"))
                return GetMockArchiveResponse(httpRequestMessage);

            // And then wire up the mock responses defined in the test case
            // If there is no mock defined by the test case, return an empty response
            if (mockDefinedInTestCase == null)
                return new HttpResponseMessage();
            else
                return mockDefinedInTestCase(httpRequestMessage);
        }

        #endregion // Private methods

        #region Private static methods

        /// <summary>
        /// Copy a HTTP headers collection into a dictionary.
        /// </summary>
        /// <param name="headerCollection">The collection of headers.</param>
        /// <returns>A dictionary containing the headers.</returns>
        private static Dictionary<string, IEnumerable<string>> CopyHeaders(HttpHeaders headerCollection)
        {
            if (headerCollection == null)
                return null;

            Dictionary<string, IEnumerable<string>> col = new Dictionary<string, IEnumerable<string>>();
            foreach (var header in headerCollection)
            {
                col.Add(header.Key, header.Value);
            }
            return col;
        }

        /// <summary>
        /// Create a response message for the mocked archive API request.
        /// </summary>
        /// <param name="httpRequestMessage">Request message for the mocked API call.</param>
        /// <returns>The response message.</returns>
        private static HttpResponseMessage GetMockArchiveResponse(HttpRequestMessage httpRequestMessage)
        {
            if (httpRequestMessage == null)
                throw new ArgumentNullException(nameof(httpRequestMessage));

            var mockedResponse = new HttpResponseMessage
            {
                RequestMessage = httpRequestMessage,
                StatusCode = HttpStatusCode.OK,
                Content = ContentHelper.CreatePlainStringContent("archived")
            };

            return mockedResponse;
        }

        #endregion // Private static methods
    }
}
