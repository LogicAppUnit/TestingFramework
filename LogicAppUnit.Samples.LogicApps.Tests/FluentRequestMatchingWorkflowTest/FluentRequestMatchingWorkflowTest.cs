using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.FluentRequestMatchingWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>fluent-test-workflow</i> workflow.
    /// </summary>
    [TestClass]
    public class FluentRequestMatchingWorkflowTest : WorkflowTestBase
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
        /// Tests the matching of requests using HTTP methods and the <see cref="IMockRequestMatcher.UsingAnyMethod()"/> method.
        /// </summary>
        [TestMethod]
        public void Test_MethodAny()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse("PutMethod",
                        MockRequestMatcher.Create()
                        .UsingPut())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithUnauthorized());
                testRunner
                    .AddMockResponse("AnyMethod",
                        MockRequestMatcher.Create()
                        .UsingAnyMethod())
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse("DefaultError",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using HTTP methods and the <see cref="IMockRequestMatcher.UsingPost()"/> method.
        /// </summary>
        [TestMethod]
        public void Test_MethodPost()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The first matcher will match PUT, DELETE, TRACE and HEAD, but not POST
                testRunner
                    .AddMockResponse("NotAPostMethod",
                        MockRequestMatcher.Create()
                        .UsingPut()
                        .UsingDelete()
                        .UsingMethod(HttpMethod.Trace, HttpMethod.Head))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithUnauthorized());
                testRunner
                    .AddMockResponse("PostMethod",
                        MockRequestMatcher.Create()
                        .UsingPost())
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse("DefaultError",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using the path.
        /// </summary>
        [TestMethod]
        public void Test_Path()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse("PathNotMatched",
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.Contains, "notMatch"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());
                testRunner
                    .AddMockResponse("PathMatched",
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.Contains, "external-service-one.testing.net"))
                    .RespondWithDefault();

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using multiple paths.
        /// </summary>
        [TestMethod]
        public void Test_PathMany()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                testRunner
                    .AddMockResponse("PathNotMatched",
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.EndsWith, "thisPath", "another/path", "/notMatch"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());
                testRunner
                    .AddMockResponse("PathMatched",
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithPath(PathMatchType.EndsWith, "/api/v1/service"))
                    .RespondWithDefault();

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using query parameters, when the first request is matched.
        /// </summary>
        [TestMethod]
        public void Test_QueryParams_Matched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last mocked response is a 'catch-all' and only matches when the previous mock responses are not matched
                testRunner
                    .AddMockResponse("MockToTestQueryParameters",
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithQueryParam("one", "oneValue")
                        .WithQueryParam("two")
                        .WithQueryParam("three", "")
                        .WithQueryParam("five", "55555"))
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse("DefaultError",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using query parameters, when the first request is not matched.
        /// </summary>
        [TestMethod]
        public void Test_QueryParams_NotMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last mocked response is a 'catch-all' and only matches when the previous mock responses are not matched
                testRunner
                    .AddMockResponse("MockToTestQueryParameters",
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithQueryParam("one", "oneValue")
                        .WithQueryParam("two")
                        .WithQueryParam("three", "")
                        .WithQueryParam("ten", "ParameterThatIsNotMatched"))
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse("DefaultError",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using HTTP headers, when the first request is matched.
        /// </summary>
        [TestMethod]
        public void Test_Headers_Matched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last mocked response is a 'catch-all' and only matches when the previous mock responses are not matched
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithHeader("UserAgent", "LogicAppUnit")
                        .WithHeader("Expect", "application/json")
                        .WithHeader("Accept")
                        .WithHeader("MyCustomHeader", "MyValue"))
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using HTTP headers, when the first request is not matched.
        /// </summary>
        [TestMethod]
        public void Test_Headers_NotMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last mocked response is a 'catch-all' and only matches when the previous mock responses are not matched
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPost()
                        .WithHeader("UserAgent", "LogicAppUnit")
                        .WithHeader("Expect", "application/json")
                        .WithHeader("Accept")
                        .WithHeader("MyCustomHeader", "HeaderValueThatIsNotMatched"))
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);
            }
        }
        private static StringContent GetWebhookRequest()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                id = "71fbcb8e-f974-449a-bb14-ac2400b150aa",
                correlationId = "c2ddb2f2-7bff-4cce-b724-ac2400b12760",
                sourceSystem = "SystemOne",
                timestamp = "2022-08-27T08:45:00.1493711Z",
                type = "CustomerUpdated",
                customerId = 54617,
                resourceId = "54617",
                resourceURI = "https://external-service-one.testing.net/api/v1/customer/54617"
            });
        }
    }
}