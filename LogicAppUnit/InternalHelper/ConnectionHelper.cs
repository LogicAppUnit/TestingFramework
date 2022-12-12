using Newtonsoft.Json.Linq;
using LogicAppUnit.Hosting;
using System;
using System.Linq;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class to manage the <i>connections.json</i> file.
    /// </summary>
    internal class ConnectionHelper
    {
        private readonly JObject _jObjectConnection;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionHelper"/> class.
        /// </summary>
        /// <param name="connectionsContent">The contents of the connections file, or <c>null</c> if the file does not exist.</param>
        public ConnectionHelper(string connectionsContent)
        {
            if (!string.IsNullOrEmpty(connectionsContent))
            {
                _jObjectConnection = JObject.Parse(connectionsContent);
            }
        }

        /// <summary>
        /// Returns the connections content.
        /// </summary>
        /// <returns>The connections content.</returns>
        public override string ToString()
        {
            if (_jObjectConnection == null)
                return null;

            return _jObjectConnection.ToString();
        }

        /// <summary>
        /// Update the <i>connections</i> by replacing all URL references to managed API connectors with the URL reference for the mock test server.
        /// </summary>
        public void ReplaceManagedApiConnectionUrlsWithMockServer()
        {
            if (_jObjectConnection == null)
                return;

            var managedApiConnections = _jObjectConnection.SelectToken("managedApiConnections").Children<JProperty>().ToList();
            if (managedApiConnections.Count > 0)
            {
                Console.WriteLine("Updating connections file for managed API connectors:");

                managedApiConnections.ForEach((connection) => {
                    // Get the original connection URL that points to the Microsoft-hosted API connection
                    Uri connectionUrl = new Uri(connection.Value["connectionRuntimeUrl"].Value<string>());

                    // Replace the host with the mock URL
                    Uri newConnectionUrl = new Uri(new Uri(TestEnvironment.FlowV2MockTestHostUri), connectionUrl.AbsolutePath);
                    connection.Value["connectionRuntimeUrl"] = newConnectionUrl;

                    Console.WriteLine($"    {connection.Name}:");
                    Console.WriteLine($"      {connectionUrl} ->");
                    Console.WriteLine($"        {newConnectionUrl}");
                });
            }
        }
    }
}
