﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Request Matcher that is used to build the request match conditions for mocking.
    /// </summary>
    public class MockRequestMatcher : IMockRequestMatcher
    {
        private readonly List<HttpMethod> _requestMethods;
        private readonly List<string> _actionNames;
        private readonly List<MockRequestPath> _requestPaths;
        private readonly Dictionary<string, string> _requestHeaders;
        private readonly Dictionary<string, string> _requestQueryParams;
        private readonly List<string> _requestContentTypes;
        private readonly List<int> _requestMatchCounts;
        private readonly List<int> _requestMatchCountsNot;

        private Func<string, bool> _requestContentStringMatcherDelegate;
        private Func<JToken, bool> _requestContentJsonMatcherDelegate;

        private int _requestMatchCounter;  // default is 0

        /// <summary>
        /// Initializes a new instance of the <see cref="MockRequestMatcher"/> class.
        /// </summary>
        /// <remarks>
        /// Private constructor because instance creation is via the static factory method.
        /// </remarks>
        private MockRequestMatcher()
        {
            _requestMethods = new List<HttpMethod>();
            _actionNames = new List<string>();
            _requestPaths = new List<MockRequestPath>();
            _requestHeaders = new Dictionary<string, string>();
            _requestQueryParams = new Dictionary<string, string>();
            _requestContentTypes = new List<string>();
            _requestMatchCounts = new List<int>();
            _requestMatchCountsNot = new List<int>();
        }

        /// <summary>
        /// Gets the number of times that the request matcher has been matched.
        /// </summary>
        internal int MatchCount
        {
            get => _requestMatchCounter;
        }

        /// <summary>
        /// Creates a default instance of <see cref="IMockRequestMatcher"/> which matches all requests.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        public static IMockRequestMatcher Create()
        {
            return new MockRequestMatcher();
        }

        #region IMockRequestMatcher implementation

        /// <inheritdoc cref="IMockRequestMatcher.UsingAnyMethod" />
        public IMockRequestMatcher UsingAnyMethod()
        {
            _requestMethods.Clear();
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.UsingGet" />
        public IMockRequestMatcher UsingGet()
        {
            return UsingMethod(HttpMethod.Get);
        }

        /// <inheritdoc cref="IMockRequestMatcher.UsingPost" />
        public IMockRequestMatcher UsingPost()
        {
            return UsingMethod(HttpMethod.Post);
        }

        /// <inheritdoc cref="IMockRequestMatcher.UsingPut" />
        public IMockRequestMatcher UsingPut()
        {
            return UsingMethod(HttpMethod.Put);
        }

        /// <inheritdoc cref="IMockRequestMatcher.UsingPatch" />
        public IMockRequestMatcher UsingPatch()
        {
            return UsingMethod(HttpMethod.Patch);
        }

        /// <inheritdoc cref="IMockRequestMatcher.UsingDelete" />
        public IMockRequestMatcher UsingDelete()
        {
            return UsingMethod(HttpMethod.Delete);
        }

        /// <inheritdoc cref="IMockRequestMatcher.UsingMethod(HttpMethod[])" />
        public IMockRequestMatcher UsingMethod(params HttpMethod[] methods)
        {
            if (methods == null || methods.Length == 0)
                throw new ArgumentNullException(nameof(methods));

            foreach (HttpMethod method in methods)
            {
                if (!_requestMethods.Contains(method))
                {
                    _requestMethods.Add(method);
                }
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.FromAction(string[])" />
        public IMockRequestMatcher FromAction(string[] actionNames)
        {
            if (actionNames == null || actionNames.Length == 0)
                throw new ArgumentNullException(nameof(actionNames));

            foreach (string actionName in actionNames)
            {
                if (!_actionNames.Contains(actionName))
                {
                    _actionNames.Add(actionName);
                }
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithPath(PathMatchType, string[])" />
        public IMockRequestMatcher WithPath(PathMatchType matchType, params string[] paths)
        {
            if (paths == null || paths.Length == 0)
                throw new ArgumentNullException(nameof(paths));

            foreach (string path in paths)
            {
                _requestPaths.Add(new MockRequestPath() { Path = path, MatchType = matchType });
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithHeader(string)" />
        public IMockRequestMatcher WithHeader(string name)
        {
            return WithHeader(name, null);
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithHeader(string, string)" />
        public IMockRequestMatcher WithHeader(string name, string value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            _requestHeaders[name] = value;

            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithContentType(string[])" />
        public IMockRequestMatcher WithContentType(params string[] contentTypes)
        {
            if (contentTypes == null || contentTypes.Length == 0)
                throw new ArgumentNullException(nameof(contentTypes));

            foreach (string contentType in contentTypes)
            {
                if (!_requestContentTypes.Contains(contentType))
                {
                    _requestContentTypes.Add(contentType);
                }
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithQueryParam(string)" />
        public IMockRequestMatcher WithQueryParam(string name)
        {
            return WithQueryParam(name, null);
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithQueryParam(string, string)" />
        public IMockRequestMatcher WithQueryParam(string name, string value)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            _requestQueryParams[name] = value;

            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithMatchCount(int[])" />
        public IMockRequestMatcher WithMatchCount(params int[] matchCounts)
        {
            if (matchCounts == null || matchCounts.Length == 0)
                throw new ArgumentNullException(nameof(matchCounts));

            foreach (int matchCount in matchCounts)
            {
                if (!_requestMatchCounts.Contains(matchCount))
                {
                    _requestMatchCounts.Add(matchCount);
                }
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithNotMatchCount(int[])" />
        public IMockRequestMatcher WithNotMatchCount(params int[] matchCounts)
        {
            if (matchCounts == null || matchCounts.Length == 0)
                throw new ArgumentNullException(nameof(matchCounts));

            foreach (int matchCount in matchCounts)
            {
                if (!_requestMatchCountsNot.Contains(matchCount))
                {
                    _requestMatchCountsNot.Add(matchCount);
                }
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithContentAsString(Func{string, bool})" />
        public IMockRequestMatcher WithContentAsString(Func<string, bool> requestContentMatch)
        {
            ArgumentNullException.ThrowIfNull(requestContentMatch);

            _requestContentStringMatcherDelegate = requestContentMatch;
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithContentAsJson(Func{JToken, bool})" />
        public IMockRequestMatcher WithContentAsJson(Func<JToken, bool> requestContentMatch)
        {
            ArgumentNullException.ThrowIfNull(requestContentMatch);

            _requestContentJsonMatcherDelegate = requestContentMatch;
            return this;
        }

        #endregion // IMockRequestMatcher implementation

        #region Internal methods

        /// <summary>
        /// Compares a set of request match conditions with a mocked HTTP request message. 
        /// </summary>
        /// <param name="request">The HTTP request message to be compared.</param>
        /// <param name="requestCache">Cache for parts of the request for performance and efficiency.</param>
        /// <returns>A <see cref="MockRequestMatchResult"/> that contains the result of the request matching.</returns>
        internal async Task<MockRequestMatchResult> MatchRequestAsync(HttpRequestMessage request, MockRequestCache requestCache)
        {
            const string headerActionName = "x-ms-workflow-operation-name";

            // Method
            // This is OR logic when multiple methods are specified in the match
            if (_requestMethods.Count > 0 && !_requestMethods.Contains(request.Method))
                return new MockRequestMatchResult(false, $"The request method '{request.Method}' is not matched with {string.Join(", ", _requestMethods)}");

            // Action Names
            // This is OR logic when multiple names are specified in the match
            if (_actionNames.Count > 0)
            {
                if (!request.Headers.Any() || !request.Headers.Contains(headerActionName))
                    return new MockRequestMatchResult(false, $"The action name header '{headerActionName}' does not exist in the request so matching has failed - the header is only generated by the Logic Apps runtime when the 'Suppress workflow headers' setting for the action is off");

                string actualRequestHeaderValue = request.Headers.GetValues(headerActionName).FirstOrDefault() ?? "";
                if (!_actionNames.Contains(actualRequestHeaderValue))
                    return new MockRequestMatchResult(false, $"The action name '{actualRequestHeaderValue}' is not matched with {string.Join(", ", _actionNames.Select(x => $"'{x}'"))}");
            }

            // Absolute Paths
            // This is OR logic when multiple paths are specified in the match
            if (_requestPaths.Count > 0)
            {
                bool pathMatch = false;
                foreach (MockRequestPath path in _requestPaths)
                {
                    if ((path.MatchType == PathMatchType.Exact && request.RequestUri.AbsolutePath == path.Path) ||
                        (path.MatchType == PathMatchType.Contains && request.RequestUri.AbsolutePath.Contains(path.Path)) ||
                        (path.MatchType == PathMatchType.EndsWith && request.RequestUri.AbsolutePath.EndsWith(path.Path)))
                    {
                        pathMatch = true;
                        break;
                    }
                }
                if (!pathMatch)
                    return new MockRequestMatchResult(false, $"The request absolute path '{request.RequestUri.AbsolutePath}' is not matched");
            }

            // Headers
            // This is AND logic when multiple headers are specified in the match
            // Headers defined in a request matcher with a null value are only validated for their existance and not their value
            if (_requestHeaders.Count > 0)
            {
                if (!request.Headers.Any())
                    return new MockRequestMatchResult(false, $"The request does not have any headers so matching has failed");

                foreach (var requestHeader in _requestHeaders)
                {
                    if (!request.Headers.Contains(requestHeader.Key))
                        return new MockRequestMatchResult(false, $"The request does not contain a header named '{requestHeader.Key}'");

                    string actualRequestHeaderValue = request.Headers.GetValues(requestHeader.Key).FirstOrDefault() ?? "";
                    if (requestHeader.Value != null && requestHeader.Value != actualRequestHeaderValue)
                        return new MockRequestMatchResult(false, $"The request contains a header named '{requestHeader.Key}' but the value is '{actualRequestHeaderValue}' and the test is expecting a value of '{requestHeader.Value}'");
                }
            }

            // Query parameters
            // This is AND logic when multiple query parameters are specified in the match
            // Parameters defined in a request matcher with a null value are only validated for their existance and not their value
            if (_requestQueryParams.Count > 0)
            {
                var parsedParams = request.RequestUri.ParseQueryString();
                var parsedParamsAsDictionary = request.RequestUri.ParseQueryString().AllKeys.ToDictionary(k => k, k => parsedParams[k]);

                if (parsedParamsAsDictionary.Count == 0)
                    return new MockRequestMatchResult(false, $"The request does not have any query parameters so matching has failed");

                foreach (var requestParam in _requestQueryParams)
                {
                    if (!parsedParamsAsDictionary.ContainsKey(requestParam.Key))
                        return new MockRequestMatchResult(false, $"The request does not contain a query parameter named '{requestParam.Key}'");

                    string actualRequestParamValue = parsedParamsAsDictionary[requestParam.Key] ?? "";
                    if (requestParam.Value != null && requestParam.Value != actualRequestParamValue)
                        return new MockRequestMatchResult(false, $"The request contains a query parameter named '{requestParam.Key}' but the value is '{actualRequestParamValue}' and the test is expecting a value of '{requestParam.Value}'");
                }
            }

            // Content Types
            // This is OR logic when multiple content types are specified in the match
            if (_requestContentTypes.Count > 0 && !_requestContentTypes.Contains(request.Content.Headers.ContentType.ToString()))
                return new MockRequestMatchResult(false, $"The request content type '{request.Content.Headers.ContentType}' is not matched with {string.Join(", ", _requestContentTypes.Select(x => $"'{x}'"))}");

            // Content
            // This is AND logic when multiple delegate functions are specified in the match
            if (_requestContentStringMatcherDelegate != null && !_requestContentStringMatcherDelegate(await requestCache.ContentAsStringAsync()))
            {
                return new MockRequestMatchResult(false, $"The request content is not matched");
            }
            if (_requestContentJsonMatcherDelegate != null && !_requestContentJsonMatcherDelegate(await requestCache.ContentAsJsonAsync()))
            {
                return new MockRequestMatchResult(false, $"The JSON request content is not matched");
            }

            _requestMatchCounter++;

            // Match count
            // This is OR logic when multiple counts are specified in the match
            if (_requestMatchCounts.Count > 0 && !_requestMatchCounts.Contains(_requestMatchCounter))
                return new MockRequestMatchResult(false, $"The current request match count is {_requestMatchCounter} which is not matched with {string.Join(", ", _requestMatchCounts)}");
            if (_requestMatchCountsNot.Count > 0 && _requestMatchCountsNot.Contains(_requestMatchCounter))
                return new MockRequestMatchResult(false, $"The current request match count is {_requestMatchCounter} which is not matched with NOT {string.Join(", ", _requestMatchCountsNot)}");

            return new MockRequestMatchResult(true);
        }

        #endregion // Internal methods
    }
}
