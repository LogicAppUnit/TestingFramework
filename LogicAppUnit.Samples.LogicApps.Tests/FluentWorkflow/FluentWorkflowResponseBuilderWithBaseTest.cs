using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.FluentWorkflow
{
    /// <summary>
    /// Test cases for the <i>fluent-workflow</i> workflow and the Response Builder features, when the test base class defines a mock response.
    /// </summary>
    [TestClass]
    public class FluentWorkflowResponseBuilderWithBaseTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.FLUENT_REQUEST_MATCHING_WORKFLOW);

            // Configure mock responses for all tests
            // The request matcher will match all requests because there are no match criteria
            AddMockResponse("DefinedInTestClass",
                MockRequestMatcher.Create())
            .RespondWith(
                MockResponseBuilder.Create()
                .WithNoContent());
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests the response builder when no mock response is configured in the test and therefore the mock response in the test class is matched.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_NoTestCaseMock()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Do not configure mock responses, the test base mock response should match

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.NoContent, workflowResponse.StatusCode);
                Assert.AreEqual(string.Empty, workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder when a mock response is configured in the test and matches, therefore the mock response in the test class is not used.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_WithTestCaseMockThatMatches()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse("DefinedInTestCase",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithStatusCode(HttpStatusCode.Accepted)
                        .WithContentAsPlainText("Your request has been queued for processing"));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.Accepted, workflowResponse.StatusCode);
                Assert.AreEqual("Your request has been queued for processing", workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder when a mock response is configured in the test and does not match, therefore the mock response in the test class is matched.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_TestCaseMockNotMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse("DefinedInTestCase",
                        MockRequestMatcher.Create()
                        .WithPath(PathMatchType.Contains, "HelloWorld"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithStatusCode(HttpStatusCode.InternalServerError)
                        .WithContentAsPlainText("It all went wrong!"));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.NoContent, workflowResponse.StatusCode);
                Assert.AreEqual(string.Empty, workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        private static StringContent GetRequest()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                name = "",
                manufacturer = "Virgin Orbit"
            });
        }
    }
}