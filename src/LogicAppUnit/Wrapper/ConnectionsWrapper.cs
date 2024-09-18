using LogicAppUnit.Hosting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogicAppUnit.Wrapper
{
    /// <summary>
    /// Wrapper class to manage the <i>connections.json</i> file.
    /// </summary>
    internal class ConnectionsWrapper
    {
        private readonly JObject _jObjectConnection;
        private readonly LocalSettingsWrapper _localSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionsWrapper"/> class.
        /// </summary>
        /// <param name="connectionsContent">The contents of the connections file, or <c>null</c> if the file does not exist.</param>
        /// <param name="localSettings">The settings helper that is used to manage the local application settings.</param>
        public ConnectionsWrapper(string connectionsContent, LocalSettingsWrapper localSettings)
        {
            if (localSettings == null)
                throw new ArgumentNullException(nameof(localSettings));

            if (!string.IsNullOrEmpty(connectionsContent))
            {
                _jObjectConnection = JObject.Parse(connectionsContent);
            }

            _localSettings = localSettings;
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
        public void ReplaceManagedApiConnectionUrlsWithMockServer(List<string> managedApisToMock)
        {
            if (_jObjectConnection == null)
                return;

            var managedApiConnections = _jObjectConnection.SelectToken("managedApiConnections").Children<JProperty>().ToList();

            // If no managed apis are specified then all managed apis are mocked
            if (managedApisToMock != null && managedApisToMock.Count > 0)
                managedApiConnections = managedApiConnections.Where(con => managedApisToMock.Contains(con.Name)).ToList();

            if (managedApiConnections.Count > 0)
            {
                Console.WriteLine("Updating connections file for managed API connectors:");

                managedApiConnections.ForEach((connection) =>
                {
                    // Get the original connection URL that points to the Microsoft-hosted API connection
                    string connectionUrl = connection.Value["connectionRuntimeUrl"].Value<string>();

                    Uri validatedConnectionUri;
                    if (!connectionUrl.Contains("@appsetting"))
                    {
                        // This connection runtime URL must be a valid URL since it is not using any appsetting substitution
                        var isValidUrl = Uri.TryCreate(connectionUrl, UriKind.Absolute, out validatedConnectionUri);
                        if (!isValidUrl)
                            throw new TestException($"The connection runtime URL for managed connection '{connection.Name}' is not a valid URL. The URL is '{connectionUrl}'");
                    }
                    else
                    {
                        // Check that the expanded connection runtime URL is a valid URL
                        string expandedConnectionUrl = _localSettings.ExpandAppSettingsValues(connectionUrl);
                        var isValidUrl = Uri.TryCreate(expandedConnectionUrl, UriKind.Absolute, out validatedConnectionUri);
                        if (!isValidUrl)
                            throw new TestException($"The connection runtime URL for managed connection '{connection.Name}' is not a valid URL, even when the app settings have been expanded. The expanded URL is '{expandedConnectionUrl}'");
                    }

                    // Replace the host with the mock URL
                    Uri newConnectionUrl = new Uri(new Uri(TestEnvironment.FlowV2MockTestHostUri), validatedConnectionUri.AbsolutePath);
                    connection.Value["connectionRuntimeUrl"] = newConnectionUrl;

                    Console.WriteLine($"    {connection.Name}:");
                    Console.WriteLine($"      {connectionUrl} ->");
                    Console.WriteLine($"        {newConnectionUrl}");
                });
            }
        }

        /// <summary>
        /// List all connections that are using the <i>ManagedServiceIdentity</i> authentication type.
        /// </summary>
        public IEnumerable<string> ListManagedApiConnectionsUsingManagedServiceIdentity()
        {
            if (_jObjectConnection == null)
                return null;

            var managedApiConnectionsUsingMsi = _jObjectConnection.SelectTokens("managedApiConnections.*").Where(x => x["authentication"]["type"].ToString() == "ManagedServiceIdentity").ToList();
            return managedApiConnectionsUsingMsi.Select(x => ((JProperty)x.Parent).Name);
        }
    }
}
