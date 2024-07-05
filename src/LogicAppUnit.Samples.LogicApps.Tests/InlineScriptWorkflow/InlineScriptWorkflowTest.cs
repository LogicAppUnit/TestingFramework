using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.InlineScriptWorkflow
{
    /// <summary>
    /// Test cases for the <i>http-workflow</i> workflow which uses a synchronous response for the HTTP trigger.
    /// </summary>
    [TestClass]
    public class InlineScriptWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.INLINE_SCRIPT_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service Two API to update the customer details is successful.
        /// </summary>
        [TestMethod]
        public void InlineScriptWorkflowTest_When_Successful()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Execute_CSharp_Script_Code"));
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Execute_CSharp_Script_Code_Output.json")),
                    ContentHelper.FormatJson(testRunner.GetWorkflowActionOutput("Execute_CSharp_Script_Code").ToString()));
            }
        }

        private static StringContent GetRequest()
        {
            return ContentHelper.CreateJsonStringContent(new {
                name = "Jane"
            });
        }
    }
}