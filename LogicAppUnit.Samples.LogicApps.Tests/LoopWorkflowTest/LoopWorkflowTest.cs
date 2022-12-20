using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicAppUnit;
using LogicAppUnit.Helper;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace LogicAppUnit.Samples.LogicApps.Tests.LoopWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>loop-test-workflow</i> workflow.
    /// This workflow includes an <i>Until</i> loop action to demonstrate how the testing framework can be used to test actions that are run multiple times in a workflow run.
    /// </summary>
    [TestClass]
    public class LoopWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.LOOP_TEST_WORKFLOW);
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
                    if (request.RequestUri.AbsolutePath == "/api/v1/doSomethingInsideLoop" && request.Method == HttpMethod.Post && iterationCounter == 4)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.InternalServerError;
                        mockedResponse.Content = ContentHelper.CreatePlainStringContent("Internal server error detected in System One");
                    }
                    else
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
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

                // Check repetitions
                // We can check the number of iterations for the looping action (for example Until or ForEach) and the actions inside the loop
                Assert.AreEqual(1, testRunner.GetWorkflowActionRepetitionCount("Initialize_variable"));
                Assert.AreEqual(numberOfIterations, testRunner.GetWorkflowActionRepetitionCount("Loop_for_each_iteration"));
                Assert.AreEqual(numberOfIterations, testRunner.GetWorkflowActionRepetitionCount("Call_Service"));
                Assert.AreEqual(1, testRunner.GetWorkflowActionRepetitionCount("Response"));

                // Check action result for the actions that were not repeated
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Initialize_variable"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Response"));

                // Check action result for the actions that were repeated
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service", 1));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service", 2));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service", 3));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Call_Service", 4));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service", 5));

                // Get the action properties, we can then assert any of the content as needed
                JToken actionResponse = testRunner.GetWorkflowAction("Response");
                JToken actionCallService2 = testRunner.GetWorkflowActionRepetition("Call_Service", 2);
                JToken actionCallService4 = testRunner.GetWorkflowActionRepetition("Call_Service", 4);
            }
        }

        private static StringContent GetRequest(int numberOfIterations)
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                numberOfIterations
            });
        }
    }
}
