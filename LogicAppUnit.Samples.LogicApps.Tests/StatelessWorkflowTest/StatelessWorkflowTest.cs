using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.StatelessWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>stateless-workflow</i> workflow.
    /// This workflow is stateless and includes a relative path in the trigger URL which includes the container name and the blob name.
    /// </summary>
    [TestClass]
    public class StatelessWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.STATELESS_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when the blob is successfully uploaded to the Storage container.
        /// </summary>
        [TestMethod]
        public void StatelessWorkflowTest_When_Successful()
        {
            const string containerName = "thisIsMyContainer";
            const string blobName = "thisIsMyBlob";

            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Run the workflow
                // The relative path must be URL-encoded by the test case, if needed
                var workflowResponse = testRunner.TriggerWorkflow(
                    ContentHelper.CreateJsonStreamContent(ResourceHelper.GetAssemblyResourceAsStream($"{GetType().Namespace}.MockData.WorkflowRequest.json")),
                    HttpMethod.Post,
                    $"{containerName}/{blobName}");

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));
                Assert.AreEqual($"Blob '{blobName}' has been uploaded to storage container '{containerName}'", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check the Client Tracking Id
                Assert.AreEqual($"{containerName}-{blobName}", testRunner.WorkflowClientTrackingId);

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Upload_Blob"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Success_Response"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Failed_Response"));

                // Check request to Blob Storage container
                var storageRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Upload_Blob");
                Assert.AreEqual(HttpMethod.Post, storageRequest.Method);
                Assert.AreEqual("application/json; charset=utf-8", storageRequest.ContentHeaders["Content-Type"].First());
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.UploadBlobRequest.json")),
                    ContentHelper.FormatJson(storageRequest.Content));
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the blob is not uploaded to the Storage container because it already exists.
        /// </summary>
        [TestMethod]
        public void StatelessWorkflowTest_When_UploadBlob_Fails()
        {
            const string containerName = "thisIsMyContainer";
            const string blobName = "thisIsMyBlob";

            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Mock the HTTP calls and customize responses
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/Upload_Blob" && request.Method == HttpMethod.Post)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.InternalServerError;
                        mockedResponse.Content = ContentHelper.CreateJsonStreamContent(ResourceHelper.GetAssemblyResourceAsStream($"{GetType().Namespace}.MockData.UploadBlobResponseFailed.json"));
                    }
                    return mockedResponse;
                };

                // Run the workflow
                // The relative path must be URL-encoded by the test case, if needed
                var workflowResponse = testRunner.TriggerWorkflow(
                    ContentHelper.CreateJsonStreamContent(ResourceHelper.GetAssemblyResourceAsStream($"{GetType().Namespace}.MockData.WorkflowRequest.json")),
                    HttpMethod.Post,
                    $"{containerName}/{blobName}");

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.InternalServerError, workflowResponse.StatusCode));
                Assert.AreEqual($"Blob '{blobName}' failed to upload to storage container '{containerName}'", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check the Client Tracking Id
                Assert.AreEqual($"{containerName}-{blobName}", testRunner.WorkflowClientTrackingId);

                // Check action result
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Upload_Blob"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Success_Response"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Failed_Response"));

                // Check request to Blob Storage container
                var storageRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Upload_Blob");
                Assert.AreEqual(HttpMethod.Post, storageRequest.Method);
                Assert.AreEqual("application/json; charset=utf-8", storageRequest.ContentHeaders["Content-Type"].First());
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.UploadBlobRequest.json")),
                    ContentHelper.FormatJson(storageRequest.Content));
            }
        }
    }
}