using Newtonsoft.Json.Linq;
using LogicAppUnit.Helper;
using LogicAppUnit.Hosting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class to manage the workflow file.
    /// </summary>
    internal static class WorkflowHelper
    {
        /// <summary>
        /// Carry out all the required modifications to the workflow so that it can be run it independently of any external dependencies.
        /// </summary>
        /// <param name="workflowTestInput">The workflow to be modified.</param>
        /// <param name="builtInConnectorsToMock">List of the built-in connector types to be mocked.</param>
        internal static void SetupWorkflowForExecution(ref WorkflowTestInput[] workflowTestInput, List<string> builtInConnectorsToMock)
        {
            AddJsonToSkipHttpRetry(ref workflowTestInput);
            ReplaceOtherTriggerTypesWithHttp(ref workflowTestInput);
            ReplaceBuiltInConnectorActionsWithHttp(ref workflowTestInput, builtInConnectorsToMock);
        }

        #region Mock External workflow calls

        /// <summary>
        /// We are creating and replacing all the nested external workflow calls with the dummy onces.We are doing this because we want to mock the external workflow 
        /// calls to test the current workflow independently. 
        /// </summary>
        /// <param name="workflowTestInput"></param>
        /// <param name="dummyWorkflowInput"></param>
        /// <exception cref="System.Exception"></exception>
        internal static void MockExternalWorkflowCalls(ref WorkflowTestInput[] workflowTestInput, List<ExternalWorkflowMock> dummyWorkflowInput)
        {
            // We don't have any external workflow call from this workflow so throw an exception.
            if (dummyWorkflowInput.Count <= 0)
            {
                throw new TestException("Need dummy workflow input to replace actual replace workflow calls.");
            }

            // Lets hold dummy workflow to real workflow name mapping in this dictionary.
            var realDummyWorkflowMap = AddWorkflowTestInputForDummyWorkflows(ref workflowTestInput, dummyWorkflowInput);

            // Now lets replace every real workflow hook from our main (parent) workflow with the dummy workflow names.
            var jObject = JObject.Parse(workflowTestInput[0].FlowDefinition);
            var realWorkflows = jObject.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Workflow").Select(x => x["inputs"] as JObject).ToList();
            if (realWorkflows.Count > 0)
            {
                realWorkflows.ForEach((workflow) => {
                    var dummyWorkflowName = realDummyWorkflowMap[workflow["host"]["workflow"]["id"].Value<string>()];
                    // Lets add link to dummy workflow
                    workflow.Remove("host");
                    workflow.Add("host", JObject.FromObject(new { workflow = new { id = dummyWorkflowName } }));
                });
            }
            workflowTestInput[0].FlowDefinition = jObject.ToString();
        }

        /// <summary>
        /// With this function we are creating multiple WorkflowTestInputs which will create multiple dummy workflow files for every real workflow in the directory.
        /// And every time when a real workflow is called, under the hood our dummy workflow will be executed.
        /// </summary>
        /// <param name="workflowTestInput"></param>
        /// <param name="dummyWorkflowInput"></param>
        /// <returns></returns>
        private static Dictionary<string, string> AddWorkflowTestInputForDummyWorkflows(ref WorkflowTestInput[] workflowTestInput, List<ExternalWorkflowMock> dummyWorkflowInput)
        {
            var realDummyWorkflowMap = new Dictionary<string, string>();
            var commonDummyWorkflowDefinition = ResourceHelper.GetAssemblyResourceAsString($"{typeof(WorkflowTestBase).Namespace}.TestConfigs.workflow.json", Assembly.GetExecutingAssembly());
            foreach (var input in dummyWorkflowInput.Select((value, i) => new { i, value }))
            {
                var jObjectDummyWorkflow = JObject.Parse(commonDummyWorkflowDefinition);
                var customizedDummyWorkflow = jObjectDummyWorkflow.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Response").Select(x => x["inputs"] as JObject).ToList();
                // Lets modify statuscode for dummy workflow as per the user's mock input
                customizedDummyWorkflow[0].Remove("statusCode");
                customizedDummyWorkflow[0].Add("statusCode", JToken.Parse(Convert.ToInt32(input.value.StatusCodeOfMockResponse, CultureInfo.InvariantCulture).ToString()));
                // Lets modify body for dummy workflow as per the user's mock input
                customizedDummyWorkflow[0].Remove("body");
                customizedDummyWorkflow[0].Add("body", JToken.Parse(input.value.BodyOfMockResponse.SerializeObject()));
                // Add newly created dummy mock in workflow list
                workflowTestInput = workflowTestInput.Concat(new WorkflowTestInput[] { new WorkflowTestInput($"dummy-workflow-{input.i}", jObjectDummyWorkflow.ToString()) }).ToArray();
                realDummyWorkflowMap.Add(input.value.WorkflowNameToMock, $"dummy-workflow-{input.i}");
            }
            return realDummyWorkflowMap;
        }

        #endregion // Mock External workflow calls

        /// <summary>
        /// Update all HTTP actions to include a retry policy of 'none' so that when we mock our HTTP calls from the workflow, any configured retries for a failed HTTP call won't happen.
        /// This change will reduce the time that it takes to execute a workflow when testing scenarios that include failed HTTP calls.
        /// </summary>
        /// <param name="workflowTestInput">The workflow to be modified.</param>
        private static void AddJsonToSkipHttpRetry(ref WorkflowTestInput[] workflowTestInput)
        {
            var jObject = JObject.Parse(workflowTestInput[0].FlowDefinition);
            var httpActions = jObject.SelectTokens("$..actions.*").Where(x => x["type"].ToString() == "Http").Select(x => x["inputs"] as JObject).ToList();

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

            workflowTestInput[0].FlowDefinition = jObject.ToString();
        }

        /// <summary>
        /// In order to test a logic app workflow free of any external dependencies, it must have a HTTP trigger. So replace any other trigger types with a HTTP trigger. 
        /// </summary>
        /// <param name="workflowTestInput">The workflow to be modified.</param>
        private static void ReplaceOtherTriggerTypesWithHttp(ref WorkflowTestInput[] workflowTestInput)
        {
            // The HTTP trigger is called a 'Request' action
            const string HttpTriggerName = "Request";

            var jObject = JObject.Parse(workflowTestInput[0].FlowDefinition);
            var trigger = jObject.SelectTokens("$.definition.triggers.*").Where(x => x["type"].ToString() != HttpTriggerName).FirstOrDefault();

            if (trigger != null)
            {
                Console.WriteLine($"Replacing workflow trigger '{((JProperty)trigger.Parent).Name}' with a HTTP Request trigger.");

                var triggerBlock = jObject.SelectToken("$.definition.triggers") as JObject;
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

            workflowTestInput[0].FlowDefinition = jObject.ToString();
        }

        /// <summary>
        /// Replace actions using Built-In connectors with HTTP actions so that their dependencies can be easily mocked.
        /// </summary>
        /// <param name="workflowTestInput">The workflow to be modified.</param>
        /// <param name="builtInConnectorsToMock">List of the built-in connector types to be mocked.</param>
        /// <remarks>
        /// The body of the HTTP POST request represents the <i>parameters</i> section of Built-In connector which allows test cases to assert this information.
        /// </remarks>
        private static void ReplaceBuiltInConnectorActionsWithHttp(ref WorkflowTestInput[] workflowTestInput, List<string> builtInConnectorsToMock)
        {
            var jObject = JObject.Parse(workflowTestInput[0].FlowDefinition);

            Console.WriteLine("Replacing workflow actions using a built-in connector with a HTTP action for the mock test server:");

            // Start with the 'top-level' actions, then move recursively down through the workflow definition to replace all of the built-in connectors
            var actionsBlock = jObject.SelectToken("$.definition.actions") as JObject;
            ReplaceBuiltInActionsWithHttpActions(actionsBlock, builtInConnectorsToMock);

            workflowTestInput[0].FlowDefinition = jObject.ToString();
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
                    uri = TestEnvironment.FlowV2MockTestHostUri + "/" + currentAction.Key,
                    body = currentAction.Value["inputs"]["parameters"].Value<object>(),
                    retryPolicy = new { type = "none" }
                },
                runAfter = currentAction.Value["runAfter"],
                operationOptions = "DisableAsyncPattern, SuppressWorkflowHeaders"
            });
        }
    }
}
