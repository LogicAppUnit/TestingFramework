using Microsoft.VisualStudio.TestTools.UnitTesting;
using LogicAppUnit;
using LogicAppUnit.Helper;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.InvokeWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>invoke-test-workflow</i> workflow.
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
        /// Tests that the correct response is returned when a child workflow is successfully invoked.
        /// </summary>
        [TestMethod]
        public void InvokeWorkflowTest_When_Successful()
        {
            using (var testRunner = CreateTestRunner())
            {
                // Run the workflow
                //var workflowResponse = testRunner.TriggerWorkflow(GetRequest(), HttpMethod.Post);

                // Check workflow run status
                //Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // TODO
            }
        }

        private HttpContent GetRequest()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                id = 54624,
                title = "Mr",
                firstName = "Peter",
                lastName = "Smith",
                dateOfBirth = "1970-04-25",
                address = new
                {
                    line1 = "Plossoms Pasture",
                    line2 = "High Street",
                    line3 = "Tinyville",
                    town = "Luton",
                    county = "Bedfordshire",
                    postcode = "LT12 6TY",
                    countryCode = "UK",
                    countryName = "United Kingdom"
                }
            });
        }
    }
}