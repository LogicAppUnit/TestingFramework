using LogicAppUnit.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;

namespace LogicAppUnit.Samples.LogicApps.Tests.BuiltInConnectorWorkflowTest
{
    /// <summary>
    /// Test cases for the <i>built-in-connector-test-workflow</i> workflow.
    /// </summary>
    [TestClass]
    public class BuiltInConnectorWorkflowTest : WorkflowTestBase
    {
        [TestInitialize]
        public void TestInitialize()
        {
            Initialize(Constants.LOGIC_APP_TEST_EXAMPLE_BASE_PATH, Constants.BUILT_IN_CONNECTOR_WORKFLOW);
        }

        [ClassCleanup]
        public static void CleanResources()
        {
            Close();
        }

        /// <summary>
        /// Tests that a new message is succesfully written to a Service bus topic when there is no Language Code in the received message.
        /// </summary>
        [TestMethod]
        public void BuiltInConnectorWorkflowTest_When_No_Language_Code()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Mock the SQL and Service Bus actions and customize responses
                // For both types of actions, the URI in the request matches the action name
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/Send_message_to_Topic")
                    {
                        // No response content for Service Bus actions
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    return mockedResponse;
                };

                // Run the workflow
                // The Service Bus trigger has been replaced with a HTTP trigger that uses a POST method
                var workflowResponse = testRunner.TriggerWorkflow(GetServiceBusMessageForTriggerNoLanguageCode(), HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                // The workflow does not have a 'Response' action, so no content to validate
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);

                // Check action result
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Execute_Query_to_get_Language_Name"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Set_Language_Name"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Terminate"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Send_message_to_Topic"));

                // Check message sent to Service Bus
                var serviceBusRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Send_message_to_Topic");
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.ServiceBus_Request_NoLanguageCode.json")),
                    ContentHelper.FormatJson(serviceBusRequest.Content));
            }
        }

        /// <summary>
        /// Tests that a new message is succesfully written to a Service bus topic when there is a Language Code in the received message that is valid.
        /// </summary>
        [TestMethod]
        public void BuiltInConnectorWorkflowTest_When_Valid_Language_Code()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Mock the SQL and Service Bus actions and customize responses
                // For both types of actions, the URI in the request matches the action name
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/Execute_Query_to_get_Language_Name")
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = ContentHelper.CreateJsonStringContent(GetSQLExecuteResponse());
                    }
                    else if (request.RequestUri.AbsolutePath == "/Send_message_to_Topic")
                    {
                        // No response content for Service Bus actions
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                    }
                    return mockedResponse;
                };

                // Run the workflow
                // The Service Bus trigger has been replaced with a HTTP trigger that uses a POST method
                var workflowResponse = testRunner.TriggerWorkflow(GetServiceBusMessageForTriggerWithValidLanguageCode(), HttpMethod.Post);

                // Check workflow run status
                Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);

                // Check workflow response
                // The workflow does not have a 'Response' action, so no content to validate
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Execute_Query_to_get_Language_Name"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Set_Language_Name"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Terminate"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Send_message_to_Topic"));

                // Check message sent to SQL database
                var sqlRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Execute_Query_to_get_Language_Name");
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.SQL_Request_en_GB.json")),
                    ContentHelper.FormatJson(sqlRequest.Content));

                // Check message sent to Service Bus
                var serviceBusRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Send_message_to_Topic");
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.ServiceBus_Request_LanguageCode.json")),
                    ContentHelper.FormatJson(serviceBusRequest.Content));
            }
        }

        /// <summary>
        /// Tests that a new message is succesfully written to a Service bus topic when there is a Language Code in the received message that is not valid.
        /// </summary>
        [TestMethod]
        public void BuiltInConnectorWorkflowTest_When_Invalid_Language_Code()
        {
            using (ITestRunner testRunner = CreateTestRunner())
            {
                // Mock the SQL and Service Bus actions and customize responses
                // For both types of actions, the URI in the request matches the action name
                testRunner.AddApiMocks = (request) =>
                {
                    HttpResponseMessage mockedResponse = new HttpResponseMessage();
                    if (request.RequestUri.AbsolutePath == "/Execute_Query_to_get_Language_Name")
                    {
                        mockedResponse.RequestMessage = request;
                        mockedResponse.StatusCode = HttpStatusCode.OK;
                        mockedResponse.Content = ContentHelper.CreateJsonStringContent(GetSQLExecuteResponseNoRecords());
                    }
                    return mockedResponse;
                };

                // Run the workflow
                // The Service Bus trigger has been replaced with a HTTP trigger that uses a POST method
                var workflowResponse = testRunner.TriggerWorkflow(GetServiceBusMessageForTriggerWithInvalidLanguageCode(), HttpMethod.Post);

                // Check workflow run status
                // This workflow has terminated with failure
                Assert.AreEqual(WorkflowRunStatus.Failed, testRunner.WorkflowRunStatus);

                // Check workflow response
                // The workflow does not have a 'Response' action, so no content to validate
                Assert.AreEqual(HttpStatusCode.OK, workflowResponse.StatusCode);

                // Check action result
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Execute_Query_to_get_Language_Name"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Set_Language_Name"));
                Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Terminate"));
                Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Send_message_to_Topic"));

                // Check message sent to SQL database
                var sqlRequest = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/Execute_Query_to_get_Language_Name");
                Assert.AreEqual(
                    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.SQL_Request_xx_GB.json")),
                    ContentHelper.FormatJson(sqlRequest.Content));
            }
        }

        private static HttpContent GetServiceBusMessageForTriggerNoLanguageCode()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                // The JSON must match the data structure used by the Service Bus trigger, this includes 'contentData' to represent the message content
                contentData = new
                {
                    id = 54624,
                    title = "Mr",
                    firstName = "Peter",
                    lastName = "Smith",
                    dateOfBirth = "1970-04-25",
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
                },
                contentType = "application/json",
                messageId = "ff421d65-5be6-4084-b748-af490100c9a5",
                label = "customer.54624",
                scheduledEnqueueTimeUtc = "1/1/0001 12:00:00 AM",
                sessionId = "54624",
                timeToLive = "06:00:00",
                deliveryCount = 1,
                enqueuedSequenceNumber = 6825,
                enqueuedTimeUtc = "2022-11-10T15:34:57.727Z",
                lockedUntilUtc = "9999-12-31T23:59:59.9999999Z",
                lockToken = "056bb9fa-9b8f-4d93-874b-7e78e71a588d",
                sequenceNumber = 980
            });
        }

        private static HttpContent GetServiceBusMessageForTriggerWithValidLanguageCode()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                // The JSON must match the data structure used by the Service Bus trigger, this includes 'contentData' to represent the message content
                contentData = new
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
                },
                contentType = "application/json",
                messageId = "ff421d65-5be6-4084-b748-af490100c9a5",
                label = "customer.54624",
                scheduledEnqueueTimeUtc = "1/1/0001 12:00:00 AM",
                sessionId = "54624",
                timeToLive = "06:00:00",
                deliveryCount = 1,
                enqueuedSequenceNumber = 6825,
                enqueuedTimeUtc = "2022-11-10T15:34:57.727Z",
                lockedUntilUtc = "9999-12-31T23:59:59.9999999Z",
                lockToken = "056bb9fa-9b8f-4d93-874b-7e78e71a588d",
                sequenceNumber = 980
            });
        }

        private static HttpContent GetServiceBusMessageForTriggerWithInvalidLanguageCode()
        {
            return ContentHelper.CreateJsonStringContent(new
            {
                // The JSON must match the data structure used by the Service Bus trigger, this includes 'contentData' to represent the message content
                contentData = new
                {
                    id = 54624,
                    title = "Mr",
                    firstName = "Peter",
                    lastName = "Smith",
                    dateOfBirth = "1970-04-25",
                    languageCode = "xx-GB",
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
                },
                contentType = "application/json",
                messageId = "ff421d65-5be6-4084-b748-af490100c9a5",
                label = "customer.54624",
                scheduledEnqueueTimeUtc = "1/1/0001 12:00:00 AM",
                sessionId = "54624",
                timeToLive = "06:00:00",
                deliveryCount = 1,
                enqueuedSequenceNumber = 6825,
                enqueuedTimeUtc = "2022-11-10T15:34:57.727Z",
                lockedUntilUtc = "9999-12-31T23:59:59.9999999Z",
                lockToken = "056bb9fa-9b8f-4d93-874b-7e78e71a588d",
                sequenceNumber = 980
            });
        }

        private static object GetSQLExecuteResponse()
        {
            return new object[]
            {
                new object[]
                {
                    new
                    {
                        LanguageCode = "en-GB",
                        LanguageName = "English (United Kingdom)"
                    }
                }
            };
        }

        private static object GetSQLExecuteResponseNoRecords()
        {
            return new object[]
            {
            };
        }
    }
}