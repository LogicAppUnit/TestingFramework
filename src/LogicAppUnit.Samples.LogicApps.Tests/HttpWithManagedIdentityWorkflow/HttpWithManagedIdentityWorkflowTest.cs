using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.HttpWithManagedIdentityWorkflow
{
    /// <summary>
    /// Test cases for the <i>http-with-managed-identity-workflow</i> workflow which has an HTTP action with Managed Identity as the authentication type.
    /// </summary>
    [TestClass]
    public class HttpWithManagedIdentityWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.HTTP_WITH_MANAGED_IDENTITY_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service to get the customers is successful.
        /// </summary>
        [TestMethod]
        public void HttpWithManagedIdentityWorkflowTest_When_Successful()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingGet()
                        .WithPath(PathMatchType.Exact, "/api/v1/customers"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Customers_from_Service_One"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Success_Response"));
            }
        }
    }
}
