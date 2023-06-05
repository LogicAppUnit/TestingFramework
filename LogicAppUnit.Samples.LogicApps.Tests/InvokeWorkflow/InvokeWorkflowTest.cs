using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.InvokeWorkflow
{
    /// <summary>
    /// Test cases for the <i>invoke-workflow</i> workflow.
    /// </summary>
    [TestClass]
    public class InvokeWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.INVOKE_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that a standard customer message is processed correctly.
        /// </summary>
        [TestMethod]
        public void InvokeWorkflowTest_When_Not_Priority_Successful()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Mock the HTTP calls and customize responses
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/Invoke_a_workflow_(not_Priority)" && request.Method == HttpMethod.Post)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = ContentHelper.CreatePlainStringContent("Upsert is successful");
                    }
                    else if (request.RequestUri.AbsolutePath == "/Delete_blob" && request.Method == HttpMethod.Post)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    return mockedResponse;
                };

                JObject x = JObject.Parse(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.WorkflowRequest.json"));
                ((JValue)x["name"]).Value = "Standard customer.json";

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    ContentHelper.CreateJsonStringContent(x.ToString()),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                // The workflow does not have a 'Response' action, so no content to validate
                Assert.AreEqual(HttpStatusCode.Accepted, workflowResponse.StatusCode);

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Invoke_a_workflow_(not_Priority)"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Invoke_a_workflow_(Priority)"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Delete_blob"));

                // Check request to Invoke Workflow
                var invokeWorkflowRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Invoke_a_workflow_(not_Priority)");
                Assert.AreEqual(HttpMethod.Post, invokeWorkflowRequest.Method);
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.InvokeWorkflowNotPriorityRequest.json")),
                    ContentHelper.FormatJson(invokeWorkflowRequest.Content));
            }
        }

        /// <summary>
        /// Tests that a priority customer message is processed correctly.
        /// </summary>
        [TestMethod]
        public void InvokeWorkflowTest_When_Priority_Successful()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Mock the HTTP calls and customize responses
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/Invoke_a_workflow_(Priority)" && request.Method == HttpMethod.Post)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = ContentHelper.CreatePlainStringContent("Upsert is successful");
                    }
                    else if (request.RequestUri.AbsolutePath == "/Add_customer_to_Priority_queue" && request.Method == HttpMethod.Post)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    else if (request.RequestUri.AbsolutePath == "/Delete_blob" && request.Method == HttpMethod.Post)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    return mockedResponse;
                };

                JObject x = JObject.Parse(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.WorkflowRequest.json"));
                ((JValue)x["name"]).Value = "Priority customer.json";

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    ContentHelper.CreateJsonStringContent(x.ToString()),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                // The workflow does not have a 'Response' action, so no content to validate
                Assert.AreEqual(HttpStatusCode.Accepted, workflowResponse.StatusCode);

                // Check action result
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Invoke_a_workflow_(not_Priority)"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Invoke_a_workflow_(Priority)"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Add_customer_to_Priority_queue"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Delete_blob"));

                // Check request to Invoke Workflow
                var invokeWorkflowRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Invoke_a_workflow_(Priority)");
                Assert.AreEqual(HttpMethod.Post, invokeWorkflowRequest.Method);
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.InvokeWorkflowPriorityRequest.json")),
                    ContentHelper.FormatJson(invokeWorkflowRequest.Content));

                // Check request to Add Customer to the storage queue
                var addToQueueRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Add_customer_to_Priority_queue");
                Assert.AreEqual(HttpMethod.Post, addToQueueRequest.Method);
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.AddToPriorityQueueRequest.json")),
                    ContentHelper.FormatJson(addToQueueRequest.Content));
            }
        }
    }
}
