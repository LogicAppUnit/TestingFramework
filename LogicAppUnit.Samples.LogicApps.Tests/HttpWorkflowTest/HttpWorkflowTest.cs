using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.HttpWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>http-test-workflow</i> workflow.
    /// </summary>
    [TestClass]
    public class HttpWorkflowTest : WorkflowTestBase
    {
        private const string _WebHookRequestApiKey = "serviceone-auth-webhook-apikey";

        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.HTTP_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when an incorrect value for the X-API-Key header is used with the webhook request.
        /// </summary>
        [TestMethod]
        public void HttpWorkflowTest_When_Wrong_API_Key_In_Request()
        {
            using (var testRunner = CreateTestRunner())
            {
                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post,
                    new Dictionary<string, string> { { "x-api-key", "wrong-key" } });

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.Unauthorized, workflowResponse.StatusCode));
                Assert.AreEqual("Invalid/No authorization header passed", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Unauthorized_Response"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service One API to get the customer details fails.
        /// </summary>
        [TestMethod]
        public void HttpWorkflowTest_When_Get_Customer_Details_Fails()
        {
            using (var testRunner = CreateTestRunner())
            {
                // Mock the HTTP calls and customize responses
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/api/v1/customers/54617" && request.Method == HttpMethod.Get)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.InternalServerError;
                        mockedResponse.Content = ContentHelper.CreatePlainStringContent("Internal server error detected in System One");
                    }
                    return mockedResponse;
                };

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post,
                    new Dictionary<string, string> { { "x-api-key", _WebHookRequestApiKey } });

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.InternalServerError, workflowResponse.StatusCode));
                Assert.AreEqual("Unable to get customer details: Internal server error detected in System One", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Unauthorized_Response"));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Failed_Get_Response"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service Two API to update the customer details fails.
        /// </summary>
        [TestMethod]
        public void HttpWorkflowTest_When_Update_Customer_Fails()
        {
            // Override one of the settings in the local settings file
            var settingsToOverride = new Dictionary<string, string>() { { "ServiceTwo-DefaultAddressType", "physical" } };

            using (var testRunner = CreateTestRunner(settingsToOverride))
            {
                // Mock the HTTP calls and customize responses
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/api/v1/customers/54617" && request.Method == HttpMethod.Get)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = GetCustomerResponse();
                    }
                    else if (request.RequestUri.AbsolutePath == "/api/v1.1/membership/customers/54617" && request.Method == HttpMethod.Put)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.InternalServerError;
                        mockedResponse.Content = ContentHelper.CreatePlainStringContent("System Two has died");
                    }
                    return mockedResponse;
                };

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post,
                    new Dictionary<string, string> { { "x-api-key", _WebHookRequestApiKey } });

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.InternalServerError, workflowResponse.StatusCode));
                Assert.AreEqual("Unable to update customer details: System Two has died", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Unauthorized_Response"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Failed_Update_Response"));

                // Check request to System Two Membership API
                var systemTwoRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/api/v1.1/membership/customers/54617");
                Assert.AreEqual(HttpMethod.Put, systemTwoRequest.Method);
                Assert.AreEqual("application/json", systemTwoRequest.ContentHeaders["Content-Type"].First());
                Assert.AreEqual("ApiKey servicetwo-auth-apikey", systemTwoRequest.Headers["x-api-key"].First());
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.SystemTwo_Request.json")),
                    ContentHelper.FormatJson(systemTwoRequest.Content));

                JToken parseCustomerInput = testRunner.GetWorkflowActionInput("Parse_Customer");
                JToken parseCustomerOutput = testRunner.GetWorkflowActionOutput("Parse_Customer");
                Assert.IsNotNull(parseCustomerInput.ToString());
                Assert.IsNotNull(parseCustomerOutput.ToString());
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the HTTP call to the Service Two API to update the customer details is successful.
        /// </summary>
        [TestMethod]
        public void HttpWorkflowTest_When_Successful()
        {
            // Override one of the settings in the local settings file
            var settingsToOverride = new Dictionary<string, string>() { { "ServiceTwo-DefaultAddressType", "physical" } };

            using (var testRunner = CreateTestRunner(settingsToOverride))
            {
                // Mock the HTTP calls and customize responses
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/api/v1/customers/54617" && request.Method == HttpMethod.Get)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = GetCustomerResponse();
                    }
                    else if (request.RequestUri.AbsolutePath == "/api/v1.1/membership/customers/54617" && request.Method == HttpMethod.Put)
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = ContentHelper.CreatePlainStringContent("success");
                    }
                    return mockedResponse;
                };

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(
                    GetWebhookRequest(),
                    HttpMethod.Post,
                    new Dictionary<string, string> { { "x-api-key", _WebHookRequestApiKey } });

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));
                Assert.AreEqual("Webhook processed successfully", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Unauthorized_Response"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Failed_Update_Response"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Success_Response"));

                // Check request to System Two Membership API
                var systemTwoRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/api/v1.1/membership/customers/54617");
                Assert.AreEqual(HttpMethod.Put, systemTwoRequest.Method);
                Assert.AreEqual("application/json", systemTwoRequest.ContentHeaders["Content-Type"].First());
                Assert.AreEqual("ApiKey servicetwo-auth-apikey", systemTwoRequest.Headers["x-api-key"].First());
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.SystemTwo_Request.json")),
                    ContentHelper.FormatJson(systemTwoRequest.Content));
            }
        }

        private static StringContent GetWebhookRequest()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                id = "71fbcb8e-f974-449a-bb14-ac2400b150aa",
                correlationId = "c2ddb2f2-7bff-4cce-b724-ac2400b12760",
                webhookId = "4cf42534-0795-4191-880c-ac2400a46ccf",
                sourceSystem = "SystemOne",
                timestamp = "2022-08-27T08:45:00.1493711Z",
                type = "CustomerUpdated",
                customerId = 54617,
                resourceId = "54617",
                resourceURI = "https://external-service-one.testing.net/api/v1/customer/54617",
                expiryTime = "2022-08-28T08:45:00.1493711Z"
            });
        }

        private static StringContent GetCustomerResponse()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                id = 54624,
                title = "Mr",
                firstName = "Peter",
                lastName = "Smith",
                dateOfBirth = "1970-04-25",
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