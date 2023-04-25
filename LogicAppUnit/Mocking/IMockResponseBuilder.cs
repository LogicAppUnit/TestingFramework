using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Response Builder that is used to configure and build the mocked responses.
    /// </summary>
    public interface IMockResponseBuilder
    {
        /// <summary>
        /// Configures an OK (HTTP 200) status code for the response.
        /// </summary>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithSuccess();

        /// <summary>
        /// Configures a No Content (HTTP 204) status code for the response and removes any configured response content.
        /// </summary>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithNoContent();

        /// <summary>
        /// Configures an Unauthorized (HTTP 401) status code for the response.
        /// </summary>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithUnauthorized();

        /// <summary>
        /// Configures a Not Found (HTTP 404) status code for the response.
        /// </summary>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithNotFound();

        /// <summary>
        /// Configures an Internal Server Error (HTTP 500) status code for the response.
        /// </summary>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithInternalServerError();

        /// <summary>
        /// Configures a status code for the response.
        /// </summary>
        /// <param name="statusCode">The status code.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithStatusCode(HttpStatusCode statusCode);

        /// <summary>
        /// Configures a HTTP header for the response.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithHeader(string name, string value);

        /// <summary>
        /// Configures a delay (in seconds) before the response is returned to the workflow being tested.
        /// </summary>
        /// <param name="secondsDelay">The delay, in seconds.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithDelay(int secondsDelay);

        /// <summary>
        /// Configures a delay (as a <see cref="TimeSpan"/>) before the response is returned to the workflow being tested.
        /// </summary>
        /// <param name="delay">The delay, as a <see cref="TimeSpan"/>.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithDelay(TimeSpan delay);

        /// <summary>
        /// Configures response content using an implementation of <see cref="HttpContent"/>.
        /// </summary>
        /// <param name="content">The HTTP content.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithContent(HttpContent content);

        /// <summary>
        /// Configures JSON response content using a <see cref="Stream"/>.
        /// </summary>
        /// <param name="jsonStream">The stream to be used for HTTP content.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithContentAsJsonStream(Stream jsonStream);

        /// <summary>
        /// Configures JSON response content using a string value.
        /// </summary>
        /// <param name="jsonString">JSON string to be used for HTTP content.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithContentAsJsonString(string jsonString);

        /// <summary>
        /// Configures plain-text response content using a string value.
        /// </summary>
        /// <param name="value">String to be used for HTTP content.</param>
        /// <returns>The <see cref="IMockResponseBuilder"/>.</returns>
        IMockResponseBuilder WithContentAsPlainTextString(string value);
    }
}
