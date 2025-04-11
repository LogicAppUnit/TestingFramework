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
        private readonly ParametersWrapper _parameters;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionsWrapper"/> class.
        /// </summary>
        /// <param name="connectionsContent">The contents of the connections file, or <c>null</c> if the file does not exist.</param>
        /// <param name="localSettings">The local settings wrapper that is used to manage the local application settings.</param>
        /// <param name="parameters">The parameters wrapper that is used to manage the parameters.</param>
        public ConnectionsWrapper(string connectionsContent, LocalSettingsWrapper localSettings, ParametersWrapper parameters)
        {
            ArgumentNullException.ThrowIfNull(localSettings);

            if (!string.IsNullOrEmpty(connectionsContent))
            {
                _jObjectConnection = JObject.Parse(connectionsContent);
            }

            _localSettings = localSettings;
            _parameters = parameters;
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
        /// <param name="managedApisToMock">The list of managed API connections to mock, or <c>null</c> if all connectors are to be mocked.</param>
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
                    if (!connectionUrl.Contains("@appsetting") && !connectionUrl.Contains("@parameters"))
                    {
                        // This connection runtime URL must be a valid URL since it is not using any substitution
                        var isValidUrl = Uri.TryCreate(connectionUrl, UriKind.Absolute, out validatedConnectionUri);
                        if (!isValidUrl)
                            throw new TestException($"The connection runtime URL for managed connection '{connection.Name}' is not a valid URL. The URL is '{connectionUrl}'");
                    }
                    else
                    {
                        // Check that the expanded connection runtime URL is a valid URL
                        // Expand parameters first because parameters can reference app settings
                        string expandedConnectionUrl = _localSettings.ExpandAppSettingsValues(_parameters.ExpandParametersAsString(connectionUrl));
                        var isValidUrl = Uri.TryCreate(expandedConnectionUrl, UriKind.Absolute, out validatedConnectionUri);
                        if (!isValidUrl)
                            throw new TestException($"The connection runtime URL for managed connection '{connection.Name}' is not a valid URL, even when the parameters and app settings have been expanded. The expanded URL is '{expandedConnectionUrl}'");
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

            List<string> returnValue = new();
            var managedApiConnections = _jObjectConnection.SelectToken("managedApiConnections").Children<JProperty>().ToList();

            managedApiConnections.ForEach((connection) =>
            {
                JObject connAuthTypeObject = null;
                JToken connAuth = ((JObject)connection.Value)["authentication"];

                switch (connAuth.Type)
                {
                    case JTokenType.String:
                        // Connection object structure is parameterised
                        connAuthTypeObject = _parameters.ExpandParameterAsObject(connAuth.Value<string>());
                        break;
                    
                    case JTokenType.Object:
                        // Connection object structure is not parameterised
                        connAuthTypeObject = connAuth.Value<JObject>();
                        break;
                }

                if (connAuthTypeObject["type"].Value<string>() == "ManagedServiceIdentity")
                    returnValue.Add(connection.Name);
            });

            return returnValue;
        }
    }
}
