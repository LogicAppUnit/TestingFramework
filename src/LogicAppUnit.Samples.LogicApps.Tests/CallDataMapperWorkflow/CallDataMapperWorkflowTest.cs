using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.CallDataMapperWorkflow
{
    /// <summary>
    /// Test cases for the <i>call-data-mapper-workflow</i> workflow which calls a XSLT map that exists in the same Logic App.
    /// </summary>
    [TestClass]
    public class CallDataMapperWorkflowTest : WorkflowTestBase
    {
        private const string XmlContentType = "application/xml";

        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.CALL_DATA_MAPPER_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests the workflow when the calling of the map is successful.
        /// This test can only be run on Windows:
        ///     https://learn.microsoft.com/en-us/azure/logic-apps/create-maps-data-transformation-visual-studio-code#limitations-and-known-issues
        /// </summary>
        [TestMethod]
        [TestCategory("WindowsOnly")]
        public void CallDataMapperWorkflowTest_When_Successful()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    ContentHelper.CreateStreamContent(ResourceHelper.GetAssemblyResourceAsStream($"{GetType().Namespace}.MockData.WorkflowRequest.xml"), XmlContentType),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(XmlContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(
                    ContentHelper.FormatXml(ResourceHelper.GetAssemblyResourceAsStream($"{GetType().Namespace}.MockData.WorkflowResponse.xml")),
                    ContentHelper.FormatXml(workflowResponse.Content.ReadAsStreamAsync().Result));

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Transform_using_Data_Mapper"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Response_Success"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Response_Failure"));
            }
        }
    }
}