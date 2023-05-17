using LogicAppUnit.Helper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Response Builder that is used to configure and build the mocked responses. The default response status code is 200 (OK).
    /// </summary>
    public class MockResponseBuilder : IMockResponseBuilder
    {
        private HttpStatusCode _statusCode;
        private readonly Dictionary<string, string> _responseHeaders;
        private Func<TimeSpan> _delayDelegate;
        private Func<HttpContent> _contentDelegate;

        // TODO: (LOW) Allow users to set additional content headers

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
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrEmpty(value))
                throw new ArgumentNullException(nameof(value));

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

        /// <inheritdoc cref="IMockResponseBuilder.AfterDelay(Func{TimeSpan})" />
        public IMockResponseBuilder AfterDelay(Func<TimeSpan> delay)
        {
            if (delay == null)
                throw new ArgumentNullException(nameof(delay));

            _delayDelegate = delay;
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.AfterDelay(int)" />
        public IMockResponseBuilder AfterDelay(int secondsDelay)
        {
            return AfterDelay(() => TimeSpan.FromSeconds(secondsDelay));
        }

        /// <inheritdoc cref="IMockResponseBuilder.AfterDelay(TimeSpan)" />
        public IMockResponseBuilder AfterDelay(TimeSpan delay)
        {
            return AfterDelay(() => delay);
        }

        /// <inheritdoc cref="IMockResponseBuilder.AfterDelay(int, int)" />
        public IMockResponseBuilder AfterDelay(int secondsMin, int secondsMax)
        {
            if (secondsMax <= secondsMin)
                throw new ArgumentException("The 'min' seconds must be less than the 'max' seconds.", nameof(secondsMin));

            return AfterDelay(() => TimeSpan.FromSeconds(MockDefinition.Random.Next(secondsMin, secondsMax)));
        }

        /// <inheritdoc cref="IMockResponseBuilder.AfterDelay(TimeSpan, TimeSpan)" />
        public IMockResponseBuilder AfterDelay(TimeSpan min, TimeSpan max)
        {
            if (max <= min)
                throw new ArgumentException("The 'min' timespan must be less than the 'max' timespan.", nameof(min));

            return AfterDelay(() => TimeSpan.FromMilliseconds(MockDefinition.Random.Next((int)min.TotalMilliseconds, (int)max.TotalMilliseconds)));
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContent(Func{HttpContent})" />
        public IMockResponseBuilder WithContent(Func<HttpContent> content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            _contentDelegate = content;
            return this;
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsJsonString(string)" />
        public IMockResponseBuilder WithContentAsJsonString(string jsonString)
        {
            // TODO: Should this be an overload of 'WithContentAsJson()'? We seem to include the data type in the name which defeats the point of overloads
            // TODO: Rename to be "UsingContent..."?
            // TODO: There is no way to set the Content Type!
            return WithContent(() => ContentHelper.CreateJsonStringContent(jsonString));
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsJsonStream(Stream)" />
        public IMockResponseBuilder WithContentAsJsonStream(Stream jsonStream)
        {
            return WithContent(() => ContentHelper.CreateJsonStreamContent(jsonStream));
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsJsonObject(object)" />
        public IMockResponseBuilder WithContentAsJsonObject(object body)
        {
            return WithContent(() => ContentHelper.CreateJsonStringContent(body));
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsJsonResource(string, Assembly)" />
        public IMockResponseBuilder WithContentAsJsonResource(string resourceName, Assembly containingAssembly)
        {
            return WithContent(() => ContentHelper.CreateJsonStreamContent(ResourceHelper.GetAssemblyResourceAsStream(resourceName, containingAssembly)));
        }

        /// <inheritdoc cref="IMockResponseBuilder.WithContentAsPlainTextString(string)" />
        public IMockResponseBuilder WithContentAsPlainTextString(string value)
        {
            return WithContent(() => ContentHelper.CreatePlainStringContent(value));
        }

        #endregion // IMockResponseBuilder implementation

        #region Internal methods

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

        /// <summary>
        /// Executes a delay.
        /// </summary>
        /// <param name="requestMatchingLog">Request matching log.</param>
        internal Task ExecuteDelayAsync(List<string> requestMatchingLog)
        {
            if (_delayDelegate == null)
            {
                return Task.CompletedTask;
            }
            else
            {
                TimeSpan delay = _delayDelegate();
                requestMatchingLog.Add($"    Delay for {delay.TotalMilliseconds} milliseconds");
                return Task.Delay(delay);
            }
        }

        #endregion // Internal methods
    }
}