using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.LoopWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>loop-test-workflow</i> workflow.
    /// This workflow includes actions in an <i>Until</i> loop and a <i>ForEach</i>/> loop to demonstrate how the testing framework can be used to test actions that are run multiple times in a workflow run.
    /// These types of actions are known as action repetitions.
    /// </summary>
    [TestClass]
    public class LoopWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.LOOP_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when there are five iterations of the <i>Until</i> loop action.
        /// </summary>
        [TestMethod]
        public void LoopWorkflowTest_With_Five_Iterations()
        {
            const int numberOfIterations = 5;

            using (var testRunner = CreateTestRunner())
            {
                // Mock the HTTP calls and customize responses
                int iterationCounter = 0;
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    iterationCounter += 1;
                    if (request.RequestUri.AbsolutePath == "/api/v1/doSomethingInsideUntilLoop" && request.Method == HttpMethod.Post && iterationCounter == 4)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.InternalServerError;
                        mockedResponse.Content = GetMockResponse(iterationCounter, "Internal server error detected in System One");
                    }
                    else if (request.RequestUri.AbsolutePath == "/api/v1/doSomethingInsideUntilLoop" && request.Method == HttpMethod.Post && iterationCounter != 4)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = GetMockResponse(iterationCounter, "All working in System One");
                    }
                    else if (request.RequestUri.AbsolutePath == "/api/v1.1/doSomethingInsideForEachLoop" && request.Method == HttpMethod.Post && iterationCounter == 7)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.BadRequest;
                        mockedResponse.Content = GetMockResponse(iterationCounter, "Bad request received by System Two");
                    }
                    else if (request.RequestUri.AbsolutePath == "/api/v1.1/doSomethingInsideForEachLoop" && request.Method == HttpMethod.Post && iterationCounter != 7)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = GetMockResponse(iterationCounter, "All working in System Two");
                    }

                    return mockedResponse;
                };

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(numberOfIterations),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Response.json")),
                    ContentHelper.FormatJson(workflowResponse.Content.ReadAsStringAsync().Result));

                // Check repetitions
                // We can check the number of iterations for the looping action (for example Until or ForEach) and the actions inside the loop
                Assert.AreEqual(1, testRunner.GetWorkflowActionRepetitionCount("Initialize_systemOneResponses"));
                Assert.AreEqual(1, testRunner.GetWorkflowActionRepetitionCount("Initialize_systemTwoResponses"));
                Assert.AreEqual(numberOfIterations, testRunner.GetWorkflowActionRepetitionCount("Until_Loop"));
                Assert.AreEqual(numberOfIterations, testRunner.GetWorkflowActionRepetitionCount("Call_Service_One"));
                Assert.AreEqual(numberOfIterations, testRunner.GetWorkflowActionRepetitionCount("For_Each_Loop"));
                Assert.AreEqual(numberOfIterations, testRunner.GetWorkflowActionRepetitionCount("Call_Service_Two"));
                Assert.AreEqual(1, testRunner.GetWorkflowActionRepetitionCount("Response"));

                // Check action result for the actions that were not repeated
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Initialize_systemOneResponses"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Initialize_systemTwoResponses"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Response"));

                // Check action results for the 'Call_Service_One' action that was repeated
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_One", 1));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_One", 2));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_One", 3));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Call_Service_One", 4));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_One", 5));

                // Check action results for the 'Call_Service_Two' action that was repeated
                // If your FoEach loop is set up to use parallel iterations, assertions like this might not be possible
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_Two", 1));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Call_Service_Two", 2));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_Two", 3));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_Two", 4));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service_Two", 5));

                // Get the action properties, we can then assert any of the content as needed
                JToken actionResponse = testRunner.GetWorkflowAction("Response");
                JToken actionCallService2 = testRunner.GetWorkflowActionRepetition("Call_Service_One", 2);
                JToken actionCallService4 = testRunner.GetWorkflowActionRepetition("Call_Service_One", 4);

                // Get action input and output
                JToken serviceOneInput2 = testRunner.GetWorkflowActionInput("Call_Service_One", 2);
                JToken serviceOneOutput2 = testRunner.GetWorkflowActionOutput("Call_Service_One", 2);
                Assert.IsNotNull(serviceOneInput2.ToString());
                Assert.IsNotNull(serviceOneOutput2.ToString());
            }
        }

        private static StringContent GetRequest(int numberOfIterations)
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                numberOfIterations
            });
        }

        private static StringContent GetMockResponse(int iterationNumber, string message)
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                iterationNumber,
                message
            });
        }
    }
}
