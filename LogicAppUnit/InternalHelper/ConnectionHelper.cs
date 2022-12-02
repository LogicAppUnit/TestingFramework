using Newtonsoft.Json.Linq;
using LogicAppUnit.Hosting;
using System;
using System.Linq;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class to manage the <i>connections.json</i> file.
    /// </summary>
    internal static class ConnectionHelper
    {
        /// <summary>
        /// Update the <i>connections</i> by replacing all URL references to managed API connectors with the URL reference for the mock test server.
        /// </summary>
        internal static void ReplaceManagedApiConnectionUrlsToFallBackOnMockServer(ref string connections)
        {
            // Not every Logic App has a connection file
            if (string.IsNullOrEmpty(connections))
                return;

            var jObjectConnection = JObject.Parse(connections);

            var managedApiConnections = jObjectConnection.SelectToken("managedApiConnections").Children<JProperty>().ToList();
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

            connections = jObjectConnection.ToString();
        }
    }
}
