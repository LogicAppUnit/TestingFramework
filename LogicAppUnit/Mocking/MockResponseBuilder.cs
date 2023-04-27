using LogicAppUnit.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Response Builder that is used to configure and build the mocked responses. The default response status code is 200 (OK).
    /// </summary>
    public class MockResponseBuilder : IMockResponseBuilder
    {
        private HttpStatusCode _statusCode;
        private TimeSpan? _delay;
        private readonly Dictionary<string, string> _responseHeaders;       // TODO: Could allow multiple match values for each header or parameter (Dictionary<string, List<string>>)?
        private Func<HttpContent> _contentDelegate;

        // TODO: Do we want to allow users to set additional content headers?

        /// <summary>
        /// Get the optional delay for the response, as a nullable <see cref="TimeSpan"/>.
        /// </summary>
        public TimeSpan? Delay
        {
            get
            {
                return _delay;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockResponseBuilder"/> class.
        /// </summary>
        /// <remarks>
        /// Private constructor because instance creation is via the static factory method.
        /// </remarks>
        private MockResponseBuilder()
        {
            _statusCode = HttpStatusCode.OK;
            _responseHeaders = new Dictionary<string, string>();
            _contentDelegate = () => null;
        }

        /// <summary>
        /// Creates this instance.
        /// </summary>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        public static IMockResponseBuilder Create()
        {
            return new MockResponseBuilder();
        }

        #region IMockResponseBuilder implementation

        /// <inheritdoc cref="IMockResponseBuilder.WithSuccess" />
        public IMockResponseBuilder WithSuccess()
        {
            return WithStatusCode(HttpStatusCode.OK);
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithNoContent" />
        public IMockResponseBuilder WithNoContent()
        {
            _contentDelegate = () => null;
            return WithStatusCode(HttpStatusCode.NoContent);
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithUnauthorized" />
        public IMockResponseBuilder WithUnauthorized()
        {
            return WithStatusCode(HttpStatusCode.Unauthorized);
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithNotFound" />
        public IMockResponseBuilder WithNotFound()
        {
            return WithStatusCode(HttpStatusCode.NotFound);
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithInternalServerError" />
        public IMockResponseBuilder WithInternalServerError()
        {
            return WithStatusCode(HttpStatusCode.InternalServerError);
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithStatusCode(HttpStatusCode)" />
        public IMockResponseBuilder WithStatusCode(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithHeader(string, string)" />
        public IMockResponseBuilder WithHeader(string name, string value)
        {
            if (_responseHeaders.ContainsKey(name))
            {
                _responseHeaders[name] = value;
            }
            else
            {
                _responseHeaders.Add(name, value);
            }
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithDelay(int)" />
        public IMockResponseBuilder WithDelay(int secondsDelay)
        {
            return WithDelay(TimeSpan.FromSeconds(secondsDelay));
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithDelay(TimeSpan)" />
        public IMockResponseBuilder WithDelay(TimeSpan delay)
        {
            _delay = delay;
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithDelay(TimeSpan, TimeSpan)" />
        public IMockResponseBuilder WithDelay(TimeSpan from, TimeSpan to)
        {
            if (to < from)
                throw new ArgumentException("The 'from' timespan must be less than or equal to the 'to' timespan.", nameof(from));

            _delay = TimeSpan.FromMilliseconds(new Random().Next((int)from.TotalMilliseconds, (int)to.TotalMilliseconds));
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContent(Func{HttpContent})" />
        public IMockResponseBuilder WithContent(Func<HttpContent> content)
        {
            _contentDelegate = content;
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsJsonString(string)" />
        public IMockResponseBuilder WithContentAsJsonString(string jsonString)
        {
            // TODO: WireMock allows you to enter dynamic JSON directly
            // TODO: Should this be an overload of 'WithContentAsJson()'?
            _contentDelegate = () => ContentHelper.CreateJsonStringContent(jsonString);
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsJsonStream(Stream)" />
        public IMockResponseBuilder WithContentAsJsonStream(Stream jsonStream)
        {
            _contentDelegate = () => ContentHelper.CreateJsonStreamContent(jsonStream);
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsPlainTextString(string)" />
        public IMockResponseBuilder WithContentAsPlainTextString(string value)
        {
            _contentDelegate = () => ContentHelper.CreatePlainStringContent(value);
            return this;
        }

        #endregion // IMockResponseBuilder implementation

        /// <summary>
        /// Build a HTTP response message using the builder configuration.
        /// </summary>
        /// <param name="request">The HTTP request message.</param>
        /// <returns>The HTTP response message.</returns>
        internal HttpResponseMessage BuildResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage();
            response.RequestMessage = request;
            response.StatusCode = _statusCode;
            response.Content = _contentDelegate();

            foreach (var header in _responseHeaders)
            {
                response.Headers.Add(header.Key, header.Value);
            }

            return response;
        }
    }
}