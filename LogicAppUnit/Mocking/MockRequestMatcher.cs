using System.Collections.Generic;
using System.Net.Http;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Request Matcher that is used to build the request match conditions for mocking.
    /// </summary>
    public class MockRequestMatcher : IMockRequestMatcher
    {
        private readonly List<HttpMethod> _requestMethods;
        private readonly List<MockRequestPath> _requestPaths;
        private readonly List<int> _requestCallIndexes;
        private readonly Dictionary<string, string> _requestHeaders;
        private readonly Dictionary<string, string> _requestQueryParams;

        private int _requestMatchCount = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="MockRequestMatcher"/> class.
        /// </summary>
        /// <remarks>
        /// Private constructor because instance creation is via the static factory method.
        /// </remarks>
        private MockRequestMatcher()
        {
            _requestMethods = new List<HttpMethod>();
            _requestPaths = new List<MockRequestPath>();
            _requestCallIndexes = new List<int>();
            _requestHeaders = new Dictionary<string, string>();              // TODO: Could allow multiple match values for each header or parameter (Dictionary<string, List<string>>)?
            _requestQueryParams = new Dictionary<string, string>();          // TODO: Could allow multiple match values for each header or parameter? Can have duplicates in query parameters.
        }

        /// <summary>
        /// Creates this instance.
        /// </summary>
        /// <returns>The <see cref="IMockRequestMatcher"/>.</returns>
        public static IMockRequestMatcher Create()
        {
            // TODO: Could replace this with a static version of Using()?????
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
            foreach (HttpMethod method in methods)
            {
                if (!_requestMethods.Contains(method))
                {
                    _requestMethods.Add(method);
                }
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
            if (_requestHeaders.ContainsKey(name))
            {
                _requestHeaders[name] = value;
            }
            else
            {
                _requestHeaders.Add(name, value);
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithPath(PathMatchType, string[])" />
        public IMockRequestMatcher WithPath(PathMatchType matchType, params string[] paths)
        {
            foreach (string path in paths)
            {
                _requestPaths.Add(new MockRequestPath() { Path = path, MatchType = matchType });
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
            if (_requestQueryParams.ContainsKey(name))
            {
                _requestQueryParams[name] = value;
            }
            else
            {
                _requestQueryParams.Add(name, value);
            }
            return this;
        }

        /// <inheritdoc cref="IMockRequestMatcher.WithCallIndex(int[])" />
        public IMockRequestMatcher WithCallIndex(params int[] callIndexes)
        {
            foreach (int callIndex in callIndexes)
            {
                if (!_requestCallIndexes.Contains(callIndex))
                {
                    _requestCallIndexes.Add(callIndex);
                }
            }
            return this;
        }

        #endregion // IMockRequestMatcher implementation

        /// <summary>
        /// Compares a set of request match conditions with a mocked HTTP request message. 
        /// </summary>
        /// <param name="request">The HTTP request message to be compared.</param>
        /// <returns><c>true</c> if the request match conditions match the HTTP request message, otherwise <c>false</c>.</returns>
        public bool MatchRequest(HttpRequestMessage request)
        {
            // Method
            if (_requestMethods.Count > 0 && !_requestMethods.Contains(request.Method))
                return false;

            // Paths
            foreach (MockRequestPath path in _requestPaths)
            {
                if ((path.MatchType == PathMatchType.AbsolutePath && request.RequestUri.AbsolutePath != path.Path) || (path.MatchType == PathMatchType.Contains && !request.RequestUri.AbsolutePath.Contains(path.Path)))
                    return false;
            }

            // Headers
            // TODO: Implement matching logic


            // Query parameters
            // TODO: Implement matching logic


            // Match count
            _requestMatchCount++;
            if (_requestCallIndexes.Count > 0 && !_requestCallIndexes.Contains(_requestMatchCount))
                return false;

            return true;
        }
    }

    /// <summary>
    /// A header for mock request matching.
    /// </summary>
    public class MockRequestPath
    {
        // TODO: Move to another file?

        /// <summary>
        /// 
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public PathMatchType MatchType { get; set; }

    }

    /// <summary>
    /// Path match type.
    /// </summary>
    public enum PathMatchType
    {
        // TODO: Move to another file?

        /// <summary>
        /// Match value is a complete match for the request URL absolute path, e.g. '\api\v1\this-service\this-operation'.
        /// </summary>
        AbsolutePath,

        /// <summary>
        /// Match value is contained within the request URL absolute path, e.g. 'this-service'.
        /// </summary>
        Contains
    }
}
