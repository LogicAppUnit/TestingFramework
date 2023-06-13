using LogicAppUnit.Helper;
using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace LogicAppUnit.Samples.LogicApps.Tests.HttpAsyncWorkflow
{
    /// <summary>
    /// Test cases for the <i>http-async-workflow</i> workflow which uses an asynchronous response for the HTTP trigger.
    /// </summary>
    [TestClass]
    public class HttpAsyncWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.HTTP_ASYNC_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service One API to get the customer details fails.
        /// </summary>
        [TestMethod]
        public void HttpAsyncWorkflowTest_When_Get_Customer_Details_Fails()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure async response handling
                testRunner.WaitForAsynchronousResponse(30);

                // Configure mock responses
                // Wait for 5 seconds to give a gap between (i) the trigger's sync response and (ii) the workflow's async response
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingGet()
                        .WithPath(PathMatchType.Exact, "/api/v1/customers/12345"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError()
                        .WithContentAsPlainText("Internal server error detected in System One")
                        .AfterDelay(5));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.InternalServerError, workflowResponse.StatusCode));
                Assert.AreEqual("Unable to get customer details: Internal server error detected in System One", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Success_Response_(Async)"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Failed_Get_Response_(Async)"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service Two API to update the customer details fails.
        /// </summary>
        [TestMethod]
        public void HttpAsyncWorkflowTest_When_Update_Customer_Fails()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure async response handling
                testRunner.WaitForAsynchronousResponse(30);

                // Configure mock responses
                // Wait for 5 seconds to give a gap between (i) the trigger's sync response and (ii) the workflow's async response
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingGet()
                        .WithPath(PathMatchType.Exact, "/api/v1/customers/12345"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContent(GetCustomerResponse)
                        .AfterDelay(5));
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPut()
                        .WithPath(PathMatchType.Exact, "/api/v1.1/membership/customers/12345"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithInternalServerError()
                        .WithContentAsPlainText("System Two is not feeling well today")
                        .AfterDelay(5));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                // Workflow has failed because the last action (Update Customer) has failed
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);

                // Check workflow response
                // The response is OK because this is an asynchronus response that is sent before the last action (Update Customer) fails
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));
                Assert.AreEqual("Webhook processed successfully", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Success_Response_(Async)"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Failed_Get_Response_(Async)"));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service Two API to update the customer details is successful.
        /// </summary>
        [TestMethod]
        public void HttpAsyncWorkflowTest_When_Successful_WaitForAsyncResponse()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Configure async response handling
                testRunner.WaitForAsynchronousResponse(30);

                // Configure mock responses
                // Wait for 5 seconds to give a gap between (i) the trigger's sync response and (ii) the workflow's async response and then (iii) the completion of the workflow
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingGet()
                        .WithPath(PathMatchType.Exact, "/api/v1/customers/12345"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContent(GetCustomerResponse)
                        .AfterDelay(5));
                testRunner
                    .AddMockResponse(
                        MockRequestMatcher.Create()
                        .UsingPut()
                        .WithPath(PathMatchType.Exact, "/api/v1.1/membership/customers/12345"))
                    .RespondWith(
                        MockResponseBuilder.Create()
                        .WithSuccess()
                        .WithContentAsPlainText("success")
                        .AfterDelay(5));

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));
                Assert.AreEqual("Webhook processed successfully", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Success_Response_(Async)"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Failed_Get_Response_(Async)"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));

                // Check tracked properties
                var trackedProps = testRunner.GetWorkflowActionTrackedProperties("Get_Customer_Details_from_Service_One");
                Assert.AreEqual("customer", trackedProps["recordType"]);
                Assert.AreEqual("12345", trackedProps["recordId"]);
                Assert.AreEqual("c2ddb2f2-7bff-4cce-b724-ac2400b12760", trackedProps["correlationId"]);
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
                customerId = 12345,
                resourceId = "12345",
                resourceURI = "https://external-service-one.testing.net/api/v1/customer/12345"
            });
        }

        private static StringContent GetCustomerResponse()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                id = 12345,
                title = "Mrs",
                firstName = "Sarah",
                lastName = "Smith",
                dateOfBirth = "1973-11-01",
                languageCode = "en-GB",
                address = new
                {
                    line1 = "8 High Street",
                    line2 = (string)null,
                    line3 = (string)null,
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
