using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.FluentWorkflow
{
    /// <summary>
    /// Test cases for the <i>fluent-workflow</i> workflow and the Response Builder features.
    /// </summary>
    [TestClass]
    public class FluentWorkflowResponseBuilderTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.FLUENT_REQUEST_MATCHING_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests the response builder with a response of <see cref="HttpStatusCode.NoContent"/>.
        /// </summary>
        [TestMethod]
        public void Test_ResponseBuilder_StatusCodeNoContent()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The request matcher will match all requests because there are no match criteria
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithNoContent());

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
        /// Tests the response builder with a response of <see cref="HttpStatusCode.ResetContent"/>.
        /// </summary>
        [TestMethod]
        public void Test_ResponseBuilder_StatusCodeResetContent()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The request matcher will match all requests because there are no match criteria
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithStatusCode(HttpStatusCode.ResetContent));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.ResetContent, workflowResponse.StatusCode);
                Assert.AreEqual(string.Empty, workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using JSON content that is a serialised from a dynamic object.
        /// </summary>
        [TestMethod]
        public void Test_ResponseBuilder_ContentAsJsonObject()
        {
            object responseAsDynamicObject = new { name = "Falcon 9", manufacturer = "SpaceX", diameter = 3.7, height = 70, massToLeo = 22.8 };

            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The request matcher will match all requests because there are no match criteria
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContentAsJsonObject(responseAsDynamicObject));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(JsonConvert.SerializeObject(responseAsDynamicObject), workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using JSON content that is a serialised from a class instance.
        /// </summary>
        [TestMethod]
        public void Test_ResponseBuilder_ContentAsJsonClass()
        {
            Rocket responseAsClass = new (){  Name = "Starship", Manufacturer = "SpaceX", Diameter = 9, Height = 120, MassToLeo = 150, VolumeToLeo = 1000 };

            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The request matcher will match all requests because there are no match criteria
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContentAsJsonObject(responseAsClass));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(JsonConvert.SerializeObject(responseAsClass), workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        private static StringContent GetRequest()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                name = "",
                manufacturer = "SpaceX"
            });
        }

        private class Rocket
        {
            public string Name { get; set; }
            public string Manufacturer { get; set; }
            public decimal Height { get; set; }
            public decimal Diameter { get; set; }
            public decimal MassToLeo { get; set; }
            public decimal? VolumeToLeo { get; set; }
        }
    }
}