using System.Net.Http;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Request Matcher that is used to build the request match conditions for mocking.
    /// </summary>
    public interface IMockRequestMatcher
    {
        /// <summary>
        /// Configure request matching using any HTTP method.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingAnyMethod();

        /// <summary>
        /// Configure request matching using HTTP GET.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingGet();

        /// <summary>
        /// Configure request matching using HTTP POST.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingPost();

        /// <summary>
        /// Configure request matching using HTTP PUT.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingPut();

        /// <summary>
        /// Configure request matching using HTTP PATCH.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingPatch();

        /// <summary>
        /// Configure request matching using HTTP DELETE.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingDelete();

        /// <summary>
        /// Configure request matching using one or more HTTP methods.
        /// </summary>
        /// <param name="methods">The HTTP methods to match.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher UsingMethod(params HttpMethod[] methods);

        /// <summary>
        /// Configure request matching based on the existance of a HTTP header. The value of the header is not considered in the match.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher WithHeader(string name);

        /// <summary>
        /// Configure request matching using a HTTP header and its value.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher WithHeader(string name, string value);

        /// <summary>
        /// Configure request matching using one or more URL absolute paths.
        /// </summary>
        /// <param name="matchType">The type of match to be used when matching the absolute paths.</param>
        /// <param name="paths">The absolute paths to match.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher WithPath(PathMatchType matchType, params string[] paths);

        /// <summary>
        /// Configure request matching based on the existance of a query parameter. The value of the parameter is not considered in the match.
        /// </summary>
        /// <param name="name">The query parameter name.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher WithQueryParam(string name);

        /// <summary>
        /// Configure request matching based on a query parameter and its value.
        /// </summary>
        /// <param name="name">The query parameter name.</param>
        /// <param name="value">The query parameter value.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher WithQueryParam(string name, string value);

        /// <summary>
        /// Configure request matching using one or more call indexes.
        /// </summary>
        /// <param name="callIndexes">The call indexes to match.</param>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        IMockRequestMatcher WithCallIndex(params int[] callIndexes);
    }
}
