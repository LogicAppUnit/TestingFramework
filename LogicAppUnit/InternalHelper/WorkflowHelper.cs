using LogicAppUnit.Hosting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class to manage the workflow file.
    /// </summary>
    internal class WorkflowHelper
    {
        private readonly string _workflowName;
        private readonly JObject _jObjectWorkflow;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowHelper"/> class.
        /// </summary>
        /// <param name="workflowName">Name of the workflow.</param>
        /// <param name="workflowContent">The contents of the workflow definition file.</param>
        public WorkflowHelper(string workflowName, string workflowContent)
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
                return (WorkflowType)Enum.Parse(typeof(WorkflowType), _jObjectWorkflow["kind"].ToString());
            }
        }

        /// <summary>
        /// Update all HTTP actions to include a retry policy of 'none' so that when making HTTP calls from the workflow, any configured retries for a failed HTTP call won't happen.
        /// </summary>
        /// <remarks>
        /// This change will reduce the time that it takes to execute a workflow when testing scenarios that include failed HTTP calls.
        /// </remarks>
        public void ReplaceRetryPoliciesWithNone()
        {
            var httpActions = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Http").Select(x => x["inputs"] as JObject).ToList();

            if (httpActions.Count > 0)
            {
                Console.WriteLine("Updating workflow HTTP actions to remove any existing Retry policies and replace with a 'none' policy:");
                var retryObj = new { type = "none" };

                httpActions.ForEach(x => {
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
            // The HTTP trigger is called a 'Request' action
            const string HttpTriggerName = "Request";

            var trigger = _jObjectWorkflow.SelectTokens("$.definition.triggers.*").Where(x => x["type"].ToString() != HttpTriggerName).FirstOrDefault();

            if (trigger != null)
            {
                Console.WriteLine($"Replacing workflow trigger '{((JProperty)trigger.Parent).Name}' with a HTTP Request trigger.");

                var triggerBlock = _jObjectWorkflow.SelectToken("$.definition.triggers") as JObject;
                triggerBlock.RemoveAll();
                triggerBlock.Add("manual", JObject.FromObject(new
                {
                    type = HttpTriggerName,
                    kind = "Http",
                    inputs = new
                    {
                        //This empty schema type is required because we want to allow generic schema type
                        schema = new { }
                    }
                }));
            }
        }

        /// <summary>
        /// Replace <i>Invoke Workflow</i> actions with HTTP actions so that the invoked workflow can be easily mocked.
        /// </summary>
        public void ReplaceInvokeWorkflowWithHttp()
        {
            var invokeActions = _jObjectWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Workflow").Select(x => x as JObject).ToList();

            if (invokeActions.Count > 0)
            {
                Console.WriteLine("Updating Workflow Invoke actions to replace call to child workflow with a HTTP action for the mock test server:");

                invokeActions.ForEach(currentAction => {

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
                        operationOptions = "DisableAsyncPattern, SuppressWorkflowHeaders"
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
            Console.WriteLine("Replacing workflow actions using a built-in connector with a HTTP action for the mock test server:");

            // Start with the 'top-level' actions, then move recursively down through the workflow definition to replace all of the built-in connectors
            var actionsBlock = _jObjectWorkflow.SelectToken("$.definition.actions") as JObject;
            ReplaceBuiltInActionsWithHttpActions(actionsBlock, builtInConnectorsToMock);
        }

        /// <summary>
        /// Replace actions using Built-In connectors with HTTP actions.
        /// </summary>
        /// <param name="block">The set of actions to be processed.</param>
        /// <param name="builtInConnectorsToMock">List of the built-in connector types to be mocked.</param>
        private static void ReplaceBuiltInActionsWithHttpActions(JObject block, List<string> builtInConnectorsToMock)
        {
            Dictionary<string, JObject> newReplacedHttpActions = new Dictionary<string, JObject>();

            foreach (var item in block)
            {
                var newReplacedAction = LoopThroughAllNestedPathsOfAction(item, builtInConnectorsToMock);
                if (newReplacedAction != null)
                {
                    newReplacedHttpActions.Add(item.Key, newReplacedAction);
                }
            }

            // Now replace each action with the mocked HTTP action
            foreach (var newAction in newReplacedHttpActions)
            {
                JToken serviceProviderConfig = block[newAction.Key]["inputs"]["serviceProviderConfiguration"];
                Console.WriteLine($"    {newAction.Key}:");
                Console.WriteLine($"      Connector Type: {serviceProviderConfig["serviceProviderId"].Value<string>()}/{serviceProviderConfig["operationId"].Value<string>()}");
                Console.WriteLine($"      Mocked URL: {newAction.Value["inputs"]["uri"]}");

                block.Remove(newAction.Key);
                block.Add(newAction.Key, newAction.Value);
            }
        }

        /// <summary>
        /// Traverse through all the paths of current action block such as control blocks (if, else and switch cases) and recursively try to replace them. 
        /// </summary>
        /// <param name="currentAction">The current action to be processed.</param>
        /// <param name="builtInConnectorsToMock">List of the built-in connector types to be mocked.</param>
        /// <returns></returns>
        private static JObject LoopThroughAllNestedPathsOfAction(KeyValuePair<string, JToken> currentAction, List<string> builtInConnectorsToMock)
        {
            var currentActionJObject = currentAction.Value as JObject;

            if (currentActionJObject.ContainsKey("actions"))
            {
                var currentActionsNestedActions = currentAction.Value["actions"] as JObject;
                ReplaceBuiltInActionsWithHttpActions(currentActionsNestedActions, builtInConnectorsToMock);
            }

            if (currentActionJObject.ContainsKey("else"))
            {
                var currentActionsNestedActions = currentAction.Value["else"]["actions"] as JObject;
                ReplaceBuiltInActionsWithHttpActions(currentActionsNestedActions, builtInConnectorsToMock);
            }

            if (currentActionJObject.ContainsKey("cases"))
            {
                var casesWithNestedActions = currentAction.Value["cases"] as JObject;
                foreach (var eachCase in casesWithNestedActions)
                {
                    if ((eachCase.Value as JObject).ContainsKey("actions"))
                    {
                        ReplaceBuiltInActionsWithHttpActions(eachCase.Value["actions"] as JObject, builtInConnectorsToMock);
                    }
                }
            }

            if (currentActionJObject.ContainsKey("default"))
            {
                var currentActionsNestedActions = currentAction.Value["default"]["actions"] as JObject;
                ReplaceBuiltInActionsWithHttpActions(currentActionsNestedActions, builtInConnectorsToMock);
            }

            return CreateMockHttpActionIfUsingBuiltInConnector(currentAction, builtInConnectorsToMock);
        }

        /// <summary>
        /// Create Mock HTTP action for in-built action by keeping all the other parameters inside http action body.  
        /// </summary>
        /// <param name="currentAction"></param>
        /// <param name="builtInConnectorsToMock">List of the built-in connector types to be mocked.</param>
        /// <returns>The JObject representing the replacement mocked action, or <c>null</c> if the action does not use a built-in connector.</returns>
        private static JObject CreateMockHttpActionIfUsingBuiltInConnector(KeyValuePair<string, JToken> currentAction, List<string> builtInConnectorsToMock)
        {
            // All actions using built-in connectors have a 'type' of 'ServiceProvider'
            if (currentAction.Value["type"].ToString() != "ServiceProvider")
                return null;

            if (builtInConnectorsToMock == null)
                return null;
            if (!builtInConnectorsToMock.Contains(currentAction.Value["inputs"]["serviceProviderConfiguration"]["operationId"].Value<string>()))
                return null;

            return JObject.FromObject(new
            {
                type = "Http",
                inputs = new
                {
                    method = "POST",
                    uri = TestEnvironment.FlowV2MockTestHostUri + "/" + WebUtility.UrlEncode(currentAction.Key),
                    body = currentAction.Value["inputs"]["parameters"].Value<object>(),
                    retryPolicy = new { type = "none" }
                },
                runAfter = currentAction.Value["runAfter"],
                operationOptions = "DisableAsyncPattern, SuppressWorkflowHeaders"
            });
        }
    }
}
