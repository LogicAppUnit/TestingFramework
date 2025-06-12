using LogicAppUnit.Hosting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace LogicAppUnit.Wrapper
{
    /// <summary>
    /// Wrapper class to manage the workflow definition file.
    /// </summary>
    internal class WorkflowDefinitionWrapper
    {
        // The HTTP trigger has a trigger type of 'Request'
        private const string HttpTriggerType = "Request";

        private readonly string _workflowName;
        private readonly JObject _jObjectWorkflow;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowDefinitionWrapper"/> class.
        /// </summary>
        /// <param name="workflowName">Name of the workflow.</param>
        /// <param name="workflowContent">The contents of the workflow definition file.</param>
        public WorkflowDefinitionWrapper(string workflowName, string workflowContent)
        {
            if (string.IsNullOrEmpty(workflowName))
                throw new ArgumentNullException(nameof(workflowName));
            if (string.IsNullOrEmpty(workflowContent))
                throw new ArgumentNullException(nameof(workflowContent));

            _workflowName = workflowName;
            _jObjectWorkflow = JObject.Parse(workflowContent);
        }

        /// <summary>
        /// Returns the workflow definition content.
        /// </summary>
        /// <returns>The workflow definition content.</returns>
        public override string ToString()
        {
            return _jObjectWorkflow.ToString();
        }

        /// <summary>
        /// Gets the name of the workflow.
        /// </summary>
        public string WorkflowName
        {
            get
            {
                return _workflowName;
            }
        }

        /// <summary>
        /// Gets the type of the workflow, either <i>Stateless</i> or <i>Stateful</i>.
        /// </summary>
        public WorkflowType WorkflowType
        {
            get
            {
                return Enum.Parse<WorkflowType>(_jObjectWorkflow["kind"].ToString());
            }
        }

        /// <summary>
        /// Gets the name of the first HTTP trigger.
        /// </summary>
        public string HttpTriggerName
        {
            get
            {
                var trigger = _jObjectWorkflow.SelectTokens("$.definition.triggers.*").Where(x => x["type"].ToString() == HttpTriggerType).FirstOrDefault();
                if (trigger == null)
                    return null;
                else
                    return ((JProperty)trigger.Parent).Name;
            }
        }

        /// <summary>
        /// Update all HTTP actions to include a retry policy of 'none' so that when making HTTP calls from the workflow, any configured retries for a failed HTTP call won't happen.
        /// </summary>
        /// <remarks>
        /// This change will reduce the time that it takes to execute a workflow when testing scenarios that include failed HTTP calls.
        /// </remarks>
        public void ReplaceHttpRetryPoliciesWithNone()
        {
            ReplaceRetryPoliciesWithNone("Http", "workflow HTTP actions");
        }

        /// <summary>
        /// Update all actions using managed API connections to include a retry policy of 'none' so that when making API calls from the workflow, any configured retries for a failed call won't happen.
        /// </summary>
        /// <remarks>
        /// This change will reduce the time that it takes to execute a workflow when testing scenarios that include failed API calls.
        /// </remarks>
        public void ReplaceManagedApiConnectionRetryPoliciesWithNone()
        {
            ReplaceRetryPoliciesWithNone("ApiConnection", "workflow actions that use a managed API connection");
        }

        /// <summary>
        /// Update all actions of a specific type to include a retry policy of 'none'.
        /// </summary>
        /// <paramref name="actionType">The type of action to be processed.</paramref>
        /// <paramref name="msg">Message to be included in the logs.</paramref>
        private void ReplaceRetryPoliciesWithNone(string actionType, string msg)
        {
            var actions = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == actionType).Select(x => x["inputs"] as JObject).ToList();

            if (actions.Count > 0)
            {
                Console.WriteLine($"Updating {msg} to remove any existing Retry policies and replace with a 'none' policy:");
                var retryObj = new { type = "none" };

                actions.ForEach(x =>
                {
                    // Remove any retryPolicy block in case it is there already
                    x.Remove("retryPolicy");
                    // Now add our retryPolicy block
                    x.Add("retryPolicy", JObject.FromObject(retryObj));

                    Console.WriteLine($"    {((JProperty)x.Parent.Parent.Parent).Name}");
                });
            }
        }

        /// <summary>
        /// Replace any non-HTTP trigger with a HTTP trigger so that the workflow can be started by the testing framework using a HTTP request.
        /// </summary>
        public void ReplaceTriggersWithHttp()
        {
            var trigger = _jObjectWorkflow.SelectTokens("$.definition.triggers.*").Where(x => x["type"].ToString() != HttpTriggerType).FirstOrDefault();

            if (trigger != null)
            {
                string triggerName = ((JProperty)trigger.Parent).Name;
                Console.WriteLine($"Replacing workflow trigger '{triggerName}' with a HTTP Request trigger.");

                var triggerBlock = _jObjectWorkflow.SelectToken("$.definition.triggers") as JObject;

                triggerBlock.RemoveAll();
                triggerBlock.Add(triggerName, JObject.FromObject(new
                {
                    type = HttpTriggerType,
                    kind = "Http",
                    inputs = new
                    {
                        schema = new { }
                    }
                }));
            }
        }

        /// <summary>
        /// Replace <i>Invoke Workflow</i> actions with HTTP actions so that the invoked workflow can be easily mocked.
        /// </summary>
        public void ReplaceInvokeWorkflowActionsWithHttp()
        {
            var invokeActions = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Workflow").Select(x => x as JObject).ToList();

            if (invokeActions.Count > 0)
            {
                Console.WriteLine("Updating Workflow Invoke actions to replace call to child workflow with a HTTP action for the mock test server:");

                invokeActions.ForEach(currentAction =>
                {
                    // Copy the 'inputs' object into the HTTP request, this includes the name of the invoked workflow and the JSON body and headers that are passed to the HTTP trigger of the called workflow
                    var newAction = JObject.FromObject(new
                    {
                        type = "Http",
                        inputs = new
                        {
                            method = "POST",
                            uri = TestEnvironment.FlowV2MockTestHostUri + "/" + WebUtility.UrlEncode(((JProperty)currentAction.Parent).Name),
                            body = currentAction["inputs"].Value<object>(),
                            retryPolicy = new { type = "none" }
                        },
                        runAfter = currentAction["runAfter"],
                        operationOptions = "DisableAsyncPattern"
                    });

                    Console.WriteLine($"    {((JProperty)currentAction.Parent).Name}:");
                    Console.WriteLine($"      Mocked URL: {newAction["inputs"]["uri"]}");

                    ((JProperty)currentAction.Parent).Value = newAction;
                });
            }
        }

        /// <summary>
        /// Replace actions using Built-In connectors with HTTP actions so that their dependencies can be easily mocked.
        /// </summary>
        /// <param name="builtInConnectorsToMock">List of the built-in connector types to be mocked.</param>
        /// <remarks>
        /// The body of the HTTP POST request represents the <i>parameters</i> section of Built-In connector which allows test cases to assert this information.
        /// </remarks>
        public void ReplaceBuiltInConnectorActionsWithHttp(List<string> builtInConnectorsToMock)
        {
            // All actions using built-in connectors have a 'type' of 'ServiceProvider'
            var invokeActions = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "ServiceProvider").Select(x => x as JObject).ToList();

            // Remove any actions that use connector types that are not included in the configuration
            invokeActions.RemoveAll(x => !builtInConnectorsToMock.Contains(x["inputs"]["serviceProviderConfiguration"]["operationId"].Value<string>()));

            if (invokeActions.Count > 0)
            {
                Console.WriteLine("Replacing workflow actions using a built-in connector with a HTTP action for the mock test server:");

                invokeActions.ForEach(currentAction =>
                {
                    var newAction = JObject.FromObject(new
                    {
                        type = "Http",
                        inputs = new
                        {
                            method = "POST",
                            uri = TestEnvironment.FlowV2MockTestHostUri + "/" + WebUtility.UrlEncode(((JProperty)currentAction.Parent).Name),
                            body = currentAction["inputs"]["parameters"].Value<object>(),
                            retryPolicy = new { type = "none" }
                        },
                        runAfter = currentAction["runAfter"],
                        operationOptions = "DisableAsyncPattern"
                    });

                    JToken serviceProviderConfig = currentAction["inputs"]["serviceProviderConfiguration"];
                    Console.WriteLine($"    {((JProperty)currentAction.Parent).Name}:");
                    Console.WriteLine($"      Connector Type: {serviceProviderConfig["serviceProviderId"].Value<string>()}/{serviceProviderConfig["operationId"].Value<string>()}");
                    Console.WriteLine($"      Mocked URL: {newAction["inputs"]["uri"]}");

                    ((JProperty)currentAction.Parent).Value = newAction;
                });
            }
        }

        /// <summary>
        /// Remove the chunking configuration from all HTTP actions.
        /// </summary>
        /// <remarks>
        /// HTTP chunking requires that the mock HTTP server and the mock response configuration support the Logic App chunking protocol, which it does not.
        /// </remarks>
        public void RemoveHttpChunkingConfiguration()
        {
            var httpActionsWithChunking = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Http")
                .Where(x => x["runtimeConfiguration"]?["contentTransfer"]?["transferMode"].ToString() == "Chunked").Select(x => x["runtimeConfiguration"] as JObject).ToList();

            if (httpActionsWithChunking.Count > 0)
            {
                Console.WriteLine("Updating workflow HTTP actions to remove chunking configuration:");

                httpActionsWithChunking.ForEach(x =>
                {
                    x.Remove("contentTransfer");

                    Console.WriteLine($"    {((JProperty)x.Parent.Parent.Parent).Name}");
                });
            }

        }

        /// <summary>
        /// Replace <i>Call a Local Function</i> actions with HTTP actions so that the invoked function can be easily mocked.
        /// </summary>
        public void ReplaceCallLocalFunctionActionsWithHttp()
        {
            var callLocalFunctionActions = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "InvokeFunction").Select(x => x as JObject).ToList();

            if (callLocalFunctionActions.Count > 0)
            {
                Console.WriteLine("Updating Call Local Function actions to replace call with a HTTP action for the mock test server:");

                callLocalFunctionActions.ForEach(currentAction =>
                {
                    // Copy the 'inputs' object into the HTTP request, this includes the name of the invoked function and the parameters
                    var newAction = JObject.FromObject(new
                    {
                        type = "Http",
                        inputs = new
                        {
                            method = "POST",
                            uri = TestEnvironment.FlowV2MockTestHostUri + "/" + WebUtility.UrlEncode(((JProperty)currentAction.Parent).Name),
                            body = currentAction["inputs"].Value<object>(),
                            retryPolicy = new { type = "none" }
                        },
                        runAfter = currentAction["runAfter"],
                        operationOptions = "DisableAsyncPattern"
                    });

                    Console.WriteLine($"    {((JProperty)currentAction.Parent).Name}:");
                    Console.WriteLine($"      Mocked URL: {newAction["inputs"]["uri"]}");

                    ((JProperty)currentAction.Parent).Value = newAction;
                });
            }
        }

        /// <summary>
        /// Update all HTTP actions with authentication type <i>ManagedServiceIdentity</i> to use <i>None</i> as the authentication type.
        /// </summary>
        /// <remarks>
        /// The <i>ManagedServiceIdentity</i> is not supported.
        /// </remarks>
        public void ReplaceManagedIdentityAuthenticationTypeWithNone()
        {
            var httpActionsWithManagedIdentityAuthenticationType = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Http")
                .Where(x => x["inputs"]?["authentication"]?["type"].ToString() == "ManagedServiceIdentity").Select(x => x["inputs"]?["authentication"] as JObject).ToList();

            if (httpActionsWithManagedIdentityAuthenticationType.Count > 0)
            {
                Console.WriteLine("Updating workflow HTTP actions to replace authentication type `ManagedServiceIdentity` with `None`:");

                httpActionsWithManagedIdentityAuthenticationType.ForEach(x =>
                {
                    x["type"] = "None";

                    Console.WriteLine($"    {((JProperty)x.Parent.Parent.Parent.Parent.Parent).Name}");
                });
            }
        }
    }
}
