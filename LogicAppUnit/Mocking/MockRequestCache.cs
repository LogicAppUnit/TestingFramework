using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// A cache for parts of the request for performance and efficiency. 
    /// </summary>
    internal class MockRequestCache
    {
        private HttpRequestMessage _request;
        private string _contentAsString;
        private JToken _contentAsJson;

        // TODO: Could integrate this more, by using it as a wrapper for all access to the request?

        /// <summary>
        /// Get the cached context as <see cref="string"/>.
        /// </summary>
        public async Task<string> ContentAsStringAsync()
        {
            if (string.IsNullOrEmpty(_contentAsString))
            {
                _contentAsString = await _request.Content.ReadAsStringAsync();
            }

            return _contentAsString;
        }

        /// <summary>
        /// Gets the cached JSON context as a <see cref="JToken"/>.
        /// </summary>
        public async Task<JToken> ContentAsJsonAsync()
        {
            if (_contentAsJson == null)
            {
                _contentAsJson = await _request.Content.ReadAsAsync<JToken>();
            }

            return _contentAsJson;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockRequestCache"/> class.
        /// </summary>
        /// <param name="request">The HTTP request to be matched.</param>
        public MockRequestCache(HttpRequestMessage request)
        {
            _request = request;
        }
    }
}
