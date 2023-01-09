using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.ManagedApiConnectorWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>managed-api-connector-test-workflow</i> workflow.
    /// </summary>
    [TestClass]
    public class ManagedApiConnectorWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.MANAGED_API_CONNECTOR_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that the correct response is returned when the upsert in Salesforce is successful and the sending of the confirmation email is also successful.
        /// </summary>
        [TestMethod]
        public void ManagedApiConnectorWorkflowTest_When_Successful()
        {
            // Override one of the settings in the local settings file
            var settingsToOverride = new Dictionary<string, string>() { { "Outlook-SubjectPrefix", "TEST ENVIRONMENT" } };

            using (ITestRunner testRunner = CreateTestRunner(settingsToOverride))
            {
                // Mock the Salesforce and Outlook actions (that use a Managed API connector) and customize responses
                // For both types of actions, the URI in the request matches the 'connectionRuntimeUrl' in 'connections.json' and the 'path' configuration of the action
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath.EndsWith("/default/tables/Account_Staging__c/externalIdFields/External_Id__c/54624") && request.Method == HttpMethod.Patch)
                    {
                        // No response content for Salesforce actions
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    else if (request.RequestUri.AbsolutePath == "/v2/Mail" && request.Method == HttpMethod.Post)
                    {
                        // No response content for Send Email actions
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    return mockedResponse;
                };

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(GetRequest(), HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode));
                Assert.AreEqual("Upsert is successful", workflowResponse.Content.ReadAsStringAsync().Result);
                Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Upsert_Customer"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Send_a_confirmation_email"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Response"));

                // Check message sent to Salesforce
                var salesforceRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath.EndsWith("/default/tables/Account_Staging__c/externalIdFields/External_Id__c/54624"));
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Salesforce_Request.json")),
                    ContentHelper.FormatJson(salesforceRequest.Content));

                // Check message sent to Outlook
                var outlookRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath.EndsWith("/v2/Mail"));
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Outlook_Request.json")),
                    ContentHelper.FormatJson(outlookRequest.Content));
            }
        }

        /// <summary>
        /// Tests that the correct response is returned when the upsert in Salesforce is successful and the sending of the confirmation email fails.
        /// </summary>
        [TestMethod]
        public void ManagedApiConnectorWorkflowTest_When_Send_Email_Fails()
        {
            // Override one of the settings in the local settings file
            var settingsToOverride = new Dictionary<string, string>() { { "Outlook-SubjectPrefix", "TEST ENVIRONMENT" } };

            using (ITestRunner testRunner = CreateTestRunner(settingsToOverride))
            {
                // Mock the Salesforce and Outlook actions (that use a Managed API connector) and customize responses
                // For both types of actions, the URI in the request matches the 'connectionRuntimeUrl' in 'connections.json' and the 'path' configuration of the action
                // It might be easier to use 'AbsolutePath.EndsWith()' instead of an equality check since the URL can be quite long
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath.EndsWith("/default/tables/Account_Staging__c/externalIdFields/External_Id__c/54624") && request.Method == HttpMethod.Patch)
                    {
                        // No response content for Salesforce actions
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    else if (request.RequestUri.AbsolutePath.EndsWith("/v2/Mail") && request.Method == HttpMethod.Post)
                    {
                        // No response content for Send Email actions
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.Unauthorized;
                    }
                    return mockedResponse;
                };

                // Run the workflow
                var workflowResponse = testRunner.TriggerWorkflow(GetRequest(), HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);

                // Check workflow response
                // This workflow does not have a Catch block, or handle the failed 'Send Email' action, so the workflow will fail with a Bad Gateway error
                testRunner.ExceptionWrapper(() => Assert.AreEqual(HttpStatusCode.BadGateway, workflowResponse.StatusCode));

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Upsert_Customer"));
                Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Send_a_confirmation_email"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Response"));

                // Check message sent to Salesforce
                var salesforceRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath.EndsWith("/default/tables/Account_Staging__c/externalIdFields/External_Id__c/54624"));
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Salesforce_Request.json")),
                    ContentHelper.FormatJson(salesforceRequest.Content));

                // Check message sent to Outlook
                var outlookRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath.EndsWith("/v2/Mail"));
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.Outlook_Request.json")),
                    ContentHelper.FormatJson(outlookRequest.Content));
            }
        }

        private static HttpContent GetRequest()
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
                    addressLine1 = "Blossoms Pasture",
                    addressLine2 = "High Street",
                    addressLine3 = "Tinyville",
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