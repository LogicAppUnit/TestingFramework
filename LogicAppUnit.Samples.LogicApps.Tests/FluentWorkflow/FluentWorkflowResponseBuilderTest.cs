using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace LogicAppUnit.Samples.LogicApps.Tests.FluentWorkflow
{
    /// <summary>
    /// Test cases for the <i>fluent-workflow</i> workflow and the Response Builder features.
    /// </summary>
    [TestClass]
    public class FluentWorkflowResponseBuilderTest : WorkflowTestBase
    {
        private const string JsonContentType = "application/json";
        private const string PlainTextContentType = "text/plain";
        private const string ClueInfoXmlContentType = "application/clue_info+xml";

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
        /// Tests the response builder with a response of <see cref="HttpStatusCode.NoContent"/> using the fluent API operation <see cref="IMockResponseBuilder.WithNoContent()"/>.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_StatusCodeNoContent()
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
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.MediaType);
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.CharSet);
                Assert.AreEqual(string.Empty, workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder with a response of <see cref="HttpStatusCode.ResetContent"/> using the fluent API operation <see cref="IMockResponseBuilder.WithStatusCode(HttpStatusCode)"/>.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_StatusCodeResetContent()
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
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.MediaType);
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.CharSet);
                Assert.AreEqual(string.Empty, workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder with a response that includes headers. The headers are copied to the workflow response so that they can be validated in this test.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_Header()
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
                        .WithSuccess()
                        .WithHeader("oneHeader", "oneValueThatIsChangedLaterInThisResponseBuilder")
                        .WithHeader("twoHeader", "twoValue")
                        .WithHeader("threeHeader", "threeValue")
                        .WithHeader("oneHeader", "oneValue"));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.MediaType);
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.CharSet);
                Assert.AreEqual(string.Empty, workflowResponse.Content.ReadAsStringAsync().Result);

                // Check workflow response headers               
                Assert.AreEqual("oneValue", workflowResponse.Headers.GetValues("oneHeader").FirstOrDefault());
                Assert.AreEqual("twoValue", workflowResponse.Headers.GetValues("twoHeader").FirstOrDefault());
                Assert.AreEqual("threeValue", workflowResponse.Headers.GetValues("threeHeader").FirstOrDefault());
            }
        }

        /// <summary>
        /// Tests the response builder using JSON content that is a serialised from a dynamic object.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentAsJsonObject()
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
                        .WithContentAsJson(responseAsDynamicObject));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(JsonContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(Encoding.UTF8.WebName, workflowResponse.Content.Headers.ContentType.CharSet);
                Assert.AreEqual(
                    JsonConvert.SerializeObject(responseAsDynamicObject),
                    workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using JSON content that is a serialised from a class instance.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentAsJsonClass()
        {
            Rocket responseAsClass = new() { Name = "Starship", Manufacturer = "SpaceX", Diameter = 9, Height = 120, MassToLeo = 150, VolumeToLeo = 1000 };

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
                        .WithContentAsJson(responseAsClass));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(JsonContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(Encoding.UTF8.WebName, workflowResponse.Content.Headers.ContentType.CharSet);
                Assert.AreEqual(
                    JsonConvert.SerializeObject(responseAsClass),
                    workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using JSON content from an embedded resource.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentAsJsonResource()
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
                        .WithSuccess()
                        .WithContentAsJson($"{GetType().Namespace}.MockData.Response.json", Assembly.GetExecutingAssembly()));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(JsonContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(Encoding.UTF8.WebName, workflowResponse.Content.Headers.ContentType.CharSet);
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Response.json")),
                    ContentHelper.FormatJson(workflowResponse.Content.ReadAsStringAsync().Result));
            }
        }

        /// <summary>
        /// Tests the response builder using plain text content.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentAsPlainTextString()
        {
            string textMsg = "This is some plain text that we can use as a text. It is not very interesting. But it doesn't have to be interested to prove that this test works as expected.";

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
                        .WithContentAsPlainText(textMsg));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(PlainTextContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(Encoding.UTF8.WebName, workflowResponse.Content.Headers.ContentType.CharSet);
                Assert.AreEqual(
                    textMsg,
                    workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using plain text content from an embedded resource.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentAsPlainTextResource()
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
                        .WithSuccess()
                        .WithContentAsPlainText($"{GetType().Namespace}.MockData.Response.txt", Assembly.GetExecutingAssembly()));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(PlainTextContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(Encoding.UTF8.WebName, workflowResponse.Content.Headers.ContentType.CharSet);
                Assert.AreEqual(
                    ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Response.txt"),
                    workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using content with a custom content type and an <see cref="Encoding.ASCII"/> encoding.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentString()
        {
            string xmlMsg = "<root><msg>This is some XML that we can use as a text. It is not very interesting. But it doesn't have to be interested to prove that this test works as expected.</msg></root>";

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
                        .WithContent(xmlMsg, ClueInfoXmlContentType, Encoding.ASCII));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(ClueInfoXmlContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.AreEqual(Encoding.ASCII.WebName, workflowResponse.Content.Headers.ContentType.CharSet);
                Assert.AreEqual(
                    xmlMsg,
                    workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response builder using content from an embedded resource with a custom content type.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_ContentResource()
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
                        .WithSuccess()
                        .WithContent($"{GetType().Namespace}.MockData.Response.ClueXml.xml", Assembly.GetExecutingAssembly(), ClueInfoXmlContentType));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual(ClueInfoXmlContentType, workflowResponse.Content.Headers.ContentType.MediaType);
                Assert.IsNull(workflowResponse.Content.Headers.ContentType?.CharSet);
                Assert.AreEqual(
                    ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Response.ClueXml.xml"),
                    workflowResponse.Content.ReadAsStringAsync().Result);
            }
        }

        /// <summary>
        /// Tests the response when neither a response builder, nor a delegate response, have been configured. A HTTP 200 (OK) response should be received with no content.
        /// </summary>
        [TestMethod]
        public void FluentWorkflowTest_ResponseBuilder_NoResponseConfigured()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);
                Assert.AreEqual("", workflowResponse.Content.ReadAsStringAsync().Result);
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