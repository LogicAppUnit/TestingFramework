using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.FluentWorkflow
{
    /// <summary>
    /// Test cases for the <i>fluent-workflow</i> workflow and the Request Matching features.
    /// </summary>
    [TestClass]
    public class FluentWorkflowRequestMatchingTest : WorkflowTestBase
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
        public void Test_RequestMatcher_MethodAny()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using HTTP methods and the <see cref="IMockRequestMatcher.UsingPost()"/> method.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_MethodPost()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The first matcher will match PUT, DELETE, TRACE and HEAD, but not POST
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using the path.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_PathSingle()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                testRunner
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using multiple paths.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_PathMany()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                testRunner
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using the content type.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_ContentType()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
                testRunner
                    .AddMockResponse("ContentType-TextPlain",
                        MockRequestMatcher.Create()
                        .WithContentType("text/plain"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());
                testRunner
                    .AddMockResponse("ContentType-XML",
                        MockRequestMatcher.Create()
                        .WithContentType("application/xml; charset=utf-8"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());
                testRunner
                    .AddMockResponse("ContentType-JSON",
                        MockRequestMatcher.Create()
                        .WithContentType("application/json; charset=utf-8"))
                    .RespondWithDefault();
                testRunner
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using query parameters, when the first request is matched.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_QueryParamsMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using query parameters, when the first request is not matched.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_QueryParamsNotMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                    .AddMockResponse("Default-Error",
                        MockRequestMatcher.Create())
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError());

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using HTTP headers, when the first request is matched.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_HeadersMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
            }
        }

        /// <summary>
        /// Tests the matching of requests using HTTP headers, when the first request is not matched.
        /// </summary>
        [TestMethod]
        public void Test_RequestMatcher_HeadersNotMatched()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure mock responses
                // The last matcher is a 'catch-all' and only matches when the previous mock responses are not matched
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
                    GetRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);
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
    }
}