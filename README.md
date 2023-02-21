# LogicAppUnit Testing Framework

<img align="right" src="https://raw.github.com/LogicAppUnit/TestingFramework/main/LogicAppUnit.png" width="120" />

LogicAppUnit is a testing framework that simplifies the creation of automated unit tests for Standard Logic Apps running in a *local development environment*, or on a *build server as part of a DevOps pipeline*. Standard Logic Apps do not include an out-of-the-box testing capability and this framework has been designed to fill this gap. The framework is based on the [Logic Apps Sample Test Framework](https://techcommunity.microsoft.com/t5/integrations-on-azure-blog/automated-testing-with-logic-apps-standard/ba-p/2960623) that was developed by Henry Liu, and includes additional functionality to make it easier to author and run tests and validate (assert) the results.

The framework does not support the testing of:

- Consumption Logic App workflows.
- Standard Logic App workflows that have been deployed to Azure.

The testing framework has been designed to make it easier to perform isolated unit testing of a workflow. The framework does this by modifying a copy of the workflow definition to remove the dependencies on external services and APIs, without affecting the functionality or behaviour of the workflow. This means that workflows can be easily tested in a developer's local environment, and by a DevOps pipeline running on a build server, where there is no access to Azure services or any other workflow dependencies.

The framework includes these high-level capabilities:

- Replace non-HTTP triggers with HTTP triggers to enable automated testing of every workflow, irrespective of the trigger type.
- Remove external service dependencies for built-in service provider connectors by replacing these actions with HTTP actions and a mock HTTP server that is managed by the framework.
- Remove external service dependencies for managed API connectors by automatically re-configuring managed API connections to use a mock HTTP server that is managed by the framework.
- Remove all retry policies to ensure that tests exercising failure scenarios do not take a long time to execute.
- Detailed logging to help with workflow test authoring and debugging.
- Programmatic access to the workflow run history to enable assertion of workflow run status, response status, action status, input and output messages and more. This includes support for action repetitions inside a loop.
- Programmatic access to the requests sent to the mock HTTP server to enable assertion of the data sent from the workflow to external services and APIs.
- Override specific local settings for a test case to enable more testing scenarios (e.g. feature flags).
- Automatically enable run history for stateless workflows by creating the `Workflows.<workflow name>.OperationOptions` setting.

This code repository includes three projects:

| Name | Description |
|:-----|:------------|
| LogicAppUnit | The testing framework. |
| LogicAppUnit.Samples.LogicApps.Tests | Example test project that demonstrates the features of the testing framework. 
| LogicAppUnit.Samples.LogicApps | Workflows that are tested by the example test project. |

You can download the *LogicAppUnit* testing framework package from nuget: https://www.nuget.org/packages/LogicAppUnit/

The best way to understand how the framework works and how to write tests using it is to read this information and look at the example tests in the *LogicAppUnit.Samples.LogicApps.Tests* project.


# Contents

<!--TOC-->
- [Using the Testing Framework](#using-the-testing-framework)
  - [Setting up a Test](#setting-up-a-test)
  - [Running a Test](#running-a-test)
  - [Checking (Asserting) the Workflow Run](#checking-asserting-the-workflow-run)
    - [Repeating Actions](#repeating-actions)
    - [Checking Action Input and Output Messages](#checking-action-input-and-output-messages)
    - [Checking HTTP requests in the Mock Server](#checking-http-requests-in-the-mock-server)
    - [Checking Tracked Properties](#checking-tracked-properties)
- [Test Configuration](#test-configuration)
- [Azurite](#azurite)
- [Local Settings file](#local-settings-file)
  - [Overriding Settings Values in a Test](#overriding-settings-values-in-a-test)
- [Test Execution Logs](#test-execution-logs)
  - [Disable Functions runtime start-up logging](#disable-functions-runtime-start-up-logging)
- [Stateless Workflows](#stateless-workflows)
- [Handling Workflow Dependencies](#handling-workflow-dependencies)
  - [Workflow Triggers](#workflow-triggers)
  - [Workflow Actions and Built-In Connectors](#workflow-actions-and-built-in-connectors)
  - [Workflow Actions and Managed API Connectors](#workflow-actions-and-managed-api-connectors)
  - [External URLs configured in the `local.settings.json` file](#external-urls-configured-in-the-local.settings.json-file)
  - [Retry Policies](#retry-policies)
- [Summary of Test Configuration Options](#summary-of-test-configuration-options)
- [Future Improvements and Changes](#future-improvements-and-changes)
<!--/TOC-->


# Using the Testing Framework

The `WorkflowTestBase` class is an abstract base class that contains functionality to set up and configure the testing framework. All test classes should inherit from this base class. This example uses test attributes for MSTest to define a test class and the test initialization and clean-up methods:

```c#
[TestClass]
public class MyWorkflowTest : WorkflowTestBase
{
    [TestInitialize]
    public void TestInitialize()
    {
        Initialize("../../../../LogicAppUnit.Samples.LogicApps", "my-test-workflow");
    }

    [ClassCleanup]
    public static void CleanResources()
    {
        Close();
    }
}
```

The `Initialize()` method is used to configure a workflow for testing. The path to the Logic App's root folder and the name of the workflow are passed as parameters. The actions performed by this method to prepare the workflow for testing are described in later sections.

The `Close()` method is used to free up the resources used by the testing framework, once all tests in the test class have completed.


## Setting up a Test

A workflow test is executed using an implementation of `ITestRunner`. This is created using the `CreateTestRunner()` method from the base class:

```c#
[TestMethod]
public void WorkflowTest()
{
    using (ITestRunner testRunner = CreateTestRunner())
    {
```

An instance of `ITestRunner` should only be used for a single test.

The next step is to configure the responses for the requests that are sent to the mock HTTP server, using the `TestRunner.AddApiMocks()` property. This example mocks the responses for workflow actions that connect to SQL Server and Service Bus:

```c#
// Mock the SQL and Service Bus actions and customize responses
// For both types of actions, the URI in the request matches the action name
testRunner.AddApiMocks = (request) =>
{
    HttpResponseMessage mockedResponse = new HttpResponseMessage();
    if (request.RequestUri.AbsolutePath == "/Execute_Query_to_get_Language_Name")
    {
        mockedResponse.RequestMessage = request;
        mockedResponse.StatusCode = HttpStatusCode.OK;
        mockedResponse.Content = ContentHelper.CreateJsonStringContent(GetSqlExecuteResponseContent());
    }
    else if (request.RequestUri.AbsolutePath == "/Send_message_to_Topic")
    {
        // No response content for Service Bus actions
        mockedResponse.RequestMessage = request;
        mockedResponse.StatusCode = HttpStatusCode.OK;
    }
    return mockedResponse;
};
```

The `ContentHelper` class is part of the testing framework and contains methods that are useful when creating HTTP content (JSON, XML and plain text) for the mocked responses.


## Running a Test

The next step is to run the workflow. The `TestRunner.TriggerWorkflow()` method creates a HTTP request for the workflow trigger and sends it to the workflow. This example uses HTTP POST but other HTTP methods can be used:

```c#
HttpResponseMessage workflowResponse = testRunner.TriggerWorkflow(FunctionToGetTriggerContent(), HttpMethod.Post);
```

The `TriggerWorkflow()` method will complete when the workflow execution has completed.

There are a few overloads of the `TriggerWorkflow()` method that allow you to set the following:
- HTTP request headers. 
- The relative path, if the HTTP trigger is configured to use a relative path. The relative path must be URL-encoded by the test case, this is not done by the test runner.

The trigger URL for the workflow is logged to the test execution log. This example is for a workflow that uses a relative path of `/thisIsMyContainer/thisIsMyBlob`:

```txt
Workflow trigger: POST http://localhost:7071/api/stateless-test-workflow/triggers/manual/invoke/thisIsMyContainer/thisIsMyBlob?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=123ao4DL03l1c1C-KRqfsl9hr0G_ipOjg_h77STbAWQ
```


## Checking (Asserting) the Workflow Run

You can check (assert) the workflow run status using the `TestRunner.WorkflowRunStatus` property:

```c#
// Check workflow run status
Assert.AreEqual(WorkflowRunStatus.Succeeded, testRunner.WorkflowRunStatus);
```

The `TestRunner.WorkflowRunId` property will give you the workflow's Run Id:

```c#
// Get the Run Id
string runId = testRunner.WorkflowRunId;
```

You can check the response from the workflow:

```c#
// Check workflow response
Assert.AreEqual(HttpStatusCode.Unauthorized, workflowResponse.StatusCode);
Assert.AreEqual("Invalid authorization header passed", workflowResponse.Content.ReadAsStringAsync().Result);
Assert.AreEqual("text/plain; charset=utf-8", workflowResponse.Content.Headers.ContentType.ToString());
```

You can check the status of individual actions in the workflow run history using the `TestRunner.GetWorkflowActionStatus(string actionName)` method, passing the action name as the parameter:

```c#
// Check action result
Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Unauthorized_Response"));
Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Get_Customer_Details_from_Service_One"));
Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Failed_Get_Response"));
Assert.AreEqual(ActionStatus.Skipped, testRunner.GetWorkflowActionStatus("Update_Customer_Details_in_Service_Two"));
```

Make sure the action name matches the action name in the `workflow.json` file, i.e. spaces replaced with underscores.

You can also check the workflow's Client Tracking Id using the `TestRunner.WorkflowClientTrackingId` property:

```c#
// Check the Client Tracking Id
Assert.AreEqual("expected-tracking-id", testRunner.WorkflowClientTrackingId);
```


### Repeating Actions

The testing framework supports the checking of actions that run inside a loop, for example an `Until` loop or a `ForEach` loop. 

You can check the status of individual action repetitions in the workflow run history using the `TestRunner.GetWorkflowActionStatus(string actionName, int repetitionNumber)` method, passing the action name and repetition number as the parameters:

```c#
// Check action result
Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service", 3));
Assert.AreEqual(ActionStatus.Failed, testRunner.GetWorkflowActionStatus("Call_Service", 4));
Assert.AreEqual(ActionStatus.Succeeded, testRunner.GetWorkflowActionStatus("Call_Service", 5));
```

In this example we expect the `Call_Service` action in the third and fifth repetitions to be successful and the fourth repetition to fail.

Make sure the action name matches the action name in the `workflow.json` file, i.e. spaces replaced with underscores.

You can also check the total number of repetitions for an action in a loop, and check the number of iterations for the loop itself:

```c#
// The 'Call_Service' action is inside of the 'Loop_for_each_iteration' loop
Assert.AreEqual(5, testRunner.GetWorkflowActionRepetitionCount("Loop_for_each_iteration"));
Assert.AreEqual(5, testRunner.GetWorkflowActionRepetitionCount("Call_Service"));
```


### Checking Action Input and Output Messages

You can check the input and output messages for an action using the `TestRunner.GetWorkflowActionInput(string actionName)` and `TestRunner.GetWorkflowActionOutput(string actionName)` methods, passing the action name as the parameter:

```c#
// Check input and output messages for Parse action
JToken parseCustomerInput = testRunner.GetWorkflowActionInput("Parse_Customer");
JToken parseCustomerOutput = testRunner.GetWorkflowActionOutput("Parse_Customer");
```

Use the method overloads for action repetitions that run in a loop:

```c#
// Get input and output messages for the second repetition
JToken serviceOneInput2 = testRunner.GetWorkflowActionInput("Call_Service_One", 2);
JToken serviceOneOutput2 = testRunner.GetWorkflowActionOutput("Call_Service_One", 2);
```

The response is a `JToken` that includes the details of the input and output for the action, including the input and output message bodies. The structure of the JSON and the attributes in the `JToken` depends on the type of action. This example validates fields in the JSON output of a Compose action:

```c#
// Validate Customer request
JToken composeCustomerRequestOutput = testRunner.GetWorkflowActionOutput("Compose_Customer_Request");
Assert.IsNotNull(composeCustomerRequestOutput);
Assert.AreEqual(composeCustomerRequestOutput["data"]["name"].Value<string>(), "Peter Smith");
Assert.AreEqual(composeCustomerRequestOutput["data"]["address"]["line1"].Value<string>(), "23 High Street");
```


### Checking HTTP requests in the Mock Server

You can also check the requests sent to the mock HTTP server, using the `TestRunner.MockRequests` property which returns a `List<MockRequest>`:

```c#
// Check request to Membership API
var request = testRunner.MockRequests.First(r => r.RequestUri.AbsolutePath == "/api/v1.1/membership/customers/1234");
Assert.AreEqual(HttpMethod.Put, request.Method);
Assert.AreEqual("application/json", request.ContentHeaders["Content-Type"].First());
Assert.AreEqual("expected-api-key", request.Headers["x-api-key"].First());
Assert.AreEqual(
    ContentHelper.FormatJson(ResourceHelper.GetAssemblyResourceAsString($"{GetType().Namespace}.MockData.ExpectedRequest.json")),
    ContentHelper.FormatJson(request.Content));
```

The instances of `MockRequest` in the list are sorted in chronological ascending order.

The `ContentHelper.FormatJson()` method is part of the testing framework and formats JSON into a consistent format to enable reliable string comparison between the actual request and the expected request. `ContentHelper.FormatXml()` can be used for the comparison of XML.

The input message for a HTTP action should match the request message that is recorded by the mock HTTP server and the same applies to the action's output message and the response that is created by the mock HTTP server. The testing framework lets you use either or both of these approaches for your test cases.


### Checking Tracked Properties

You can check the tracked properties for an action using the `TestRunner.GetWorkflowActionTrackedProperties(string actionName)` method, passing the action name as the parameter:

```c#
// Check tracked properties
Dictionary<string, string> trackedProps = testRunner.GetWorkflowActionTrackedProperties("Get_Customer");
Assert.AreEqual("customer", trackedProps["recordType"]);
Assert.AreEqual("123456", trackedProps["recordId"]);
Assert.AreEqual("c2ddb2f2-7bff-4cce-b724-ac2400b12760", trackedProps["correlationId"]);
```

Use the method overload for action repetitions that run in a loop:

```c#
// Check tracked properties for the fourth repetition
Dictionary<string, string> trackedProps = testRunner.GetWorkflowActionTrackedProperties("Get_Customer", 4);
```

> Stateless workflows do not record the tracked properties in the workflow run history, even when the run history is enabled using the `OperationOptions` setting. Therefore the `GetWorkflowActionTrackedProperties()` method will always return `null` for actions in a stateless workflow.


# Test Configuration

Each test project that uses the testing framework must include a `testConfiguration.json` file which is used to configure the framework. If this file does not exist, the tests will not run. 

The various test configuration options are described in the following sections. If no configuration options are required, the file should contain an empty JSON document:

```json
{}
```


# Azurite

Standard Logic Apps have a dependency on Azure Storage services such as Blob, File Share, Queue and Table for metadata, run-time state and run history. When Standard Logic apps are run in a local development environment, Azurite is used to emulate the Azure Storage services. If Azurite is not installed or is not running in the local environment, workflows cannot be run.

The testing framework assumes that the environment in which the tests are running has Azurite installed.

The framework will automatically check that Azurite is listening to the required ports before running any tests. This feature ensures that tests fail quickly when Azurite is not running, as opposed to taking longer to run and then failing later in the Functions runtime. If Azurite is not listening on the required ports, an exception will be thrown by the testing  framework:

```txt
LogicAppUnit.TestException: Azurite is not running on ports XXX (Blob service), YYY (Queue service) and ZZZ (Table service).
Logic App workflows cannot run unless all three services are running in Azurite.
```

If Azurite is listening, you will see this message recorded in the test execution log:

```txt
Azurite is listening on ports XXX (Blob service), YYY (Queue service) and ZZZ (Table service).
```

By default, the testing framework assumes that Azurite is using these ports:

- 10000 (Blob service)
- 10001 (Queue service)
- 10002 (Table service)

If you have configured Azurite to use different ports, the automatic check can be configured using the `azurite` section of the `testConfiguration.json` file:

```json
"azurite": {
    "blobServicePort": 10000,
    "queueServicePort": 10001,
    "tableServicePort": 10002
}
```

This Azurite port check can be disabled by setting the `azurite.enableAzuritePortCheck` option in the `testConfiguration.json` file to `false`. The default is `true`.



# Local Settings file

Each Logic App includes a mandatory settings file. The default name for this file is `local.settings.json`. The testing framework can be configured to use a different settings file if required, using the `localSettingsFilename` option in the `testConfiguration.json` file:

```json
"localSettingsFilename": "local.settings.unitTests.json"
```


## Overriding Settings Values in a Test

The testing framework will use the Logic App's settings file for each workflow that is tested. There may be a scenario where a test case wants to exercise a scenario which requires different values for one or more settings. One example would be a feature flag that needs to be enabled to allow testing of the feature.

A test author can implement this by using the `WorkflowTestBase.CreateTestRunner(Dictionary<string, string>)` overload when creating the `TestRunner` and passing a dictionary containing the settings to be overridden and their values.

Here is an example:

```c#
var settingsToOverride = new Dictionary<string, string>()
{
  { "DefaultAddressType", "physical" },
  { "EnableV2Functionaity", "true" }
};

using (TestRunner testRunner = CreateTestRunner(settingsToOverride))
```

The test execution log will include logging to show when local settings have been overridden:

```txt
Updating local settings file with test overrides:
    DefaultAddressType
      Updated value to: physical
    EnableV2Functionaity
      Updated value to: true
```


# Test Execution Logs

The testing framework writes information to the test log so that the test author can understand what the test framework is doing and why a test is failing. The majority of the logs are the Standard Output from the Functions runtime that is executing the workflow being tested, but the framework also writes other information that is useful.

The test execution log includes:

- Description of how the framework is updating the workflow, settings and connection files to enable isolated testing (see sections below for more details)
- Functions runtime start-up logs
- Functions runtime workflow execution logs
- Summary of the requests that were received by the mock HTTP server that is managed by the testing framework


## Disable Functions runtime start-up logging

The Functions runtime start-up logs can be verbose and are not usually needed when authoring or running tests, unless the test is failing because the Functions runtime is not starting up correctly.

To reduce the volume of logging, the inclusion of the Functions runtime start-up logs is disabled by default. It can be enabled by adding the `logging.writeFunctionRuntineStartupLogs` option to the `testConfiguration.json` file with a value of `true`:

```json
"logging": {
  "writeFunctionRuntineStartupLogs": true
}
```

The setting is optional. If it is not included, the default value is `false`.


# Stateless Workflows

The testing framework must have access to the workflow run history to be able to check (assert) the workflow execution. With stateless Logic Apps, the run history is not stored unless the  `Workflows.<workflow name>.OperationOptions` setting is set to `WithStatelessRunHistory` in the settings file.

The testing framework will automatically check if this setting exists for a stateless workflow. If the setting does not exist, and the `workflow.autoConfigureWithStatelessRunHistory` configuration option is set to `true` in `testConfiguration.json`, the framework will automatically create the setting so that the run history is stored. You will then see this in the test execution log:

```txt
 Workflow is stateless, creating new setting: Workflows.<workflow name>.OperationOptions = WithStatelessRunHistory
```

If the `workflow.autoConfigureWithStatelessRunHistory` configuration option is set to `false`, the test will fail:

```txt
LogicAppUnit.TestException: The workflow is stateless and the 'Workflows.<workflow name>.OperationOptions' setting is not configured for 'WithStatelessRunHistory'.
This means that the workflow execution history will not be created and therefore the workflow cannot be tested.
Set the 'workflow.autoConfigureWithStatelessRunHistory` option to 'true' in 'testConfiguration.json' so that the testing framework creates this setting automatically when running the test.
```

The default value for the `workflow.autoConfigureWithStatelessRunHistory` configuration option is `true`.


# Handling Workflow Dependencies

A workflow usually has dependencies on one or more external services. For example, a workflow may read a message from a Service Bus queue using a trigger, or read data from a SQL Server database using a SQL Server action, or send an email using a SMTP action. To test a workflow in an isolated environment, these dependencies need to be removed and replaced with something else that enables an action's inputs and outputs to be validated by the test case, without affecting the functionality or behaviour of the workflow.

The testing framework will automatically remove the following dependencies when a workflow is tested:

- Triggers that use a non-HTTP connector
- Actions that use a non-HTTP built-in service provider connector
- Actions that use a Managed API connector
- External URLs configured in the `local.settings.json` file

The testing framework will also remove any retry policies for actions to improve the run-time execution of tests.

These features are described below.


## Workflow Triggers

Every workflow has a trigger that is used to start the execution of the workflow. There are many different types of trigger, for example the HTTP trigger which is used to start a workflow using a HTTP request, and a Service Bus trigger which is used to start a workflow when a message is available in a queue or topic.

A HTTP trigger does not have any dependencies on an external system, but other trigger types do. This includes Service Bus triggers, Event Hub triggers and Event Grid triggers. When unit testing a workflow with one of these trigger types, the dependency needs to be removed. The testing framework does this by replacing a non-HTTP trigger with a HTTP trigger. This allows each workflow to be started by the testing framework using a HTTP request.

Replacing the trigger like this does not affect the functionality of the workflow or change the behaviour. Every trigger creates a JSON message which is then processed by the actions in the workflow. The structure of the JSON message differs for each type of trigger, but as long as the same message structure is used in the request for the HTTP trigger, the rest of the workflow will execute in exactly the same way.

As an example, this is a Service Bus trigger that receives a message from a topic using a peek-lock:

```json
"triggers": {
    "When_messages_are_available_in_a_topic_subscription_(peek-lock)": {
        "type": "ServiceProvider",
        "inputs": {
            "parameters": {
                "topicName": "topic-name",
                "subscriptionName": "subscription-name",
                "isSessionsEnabled": true
            },
            "serviceProviderConfiguration": {
                "connectionName": "service-bus-connection",
                "operationId": "peekLockTopicMessages",
                "serviceProviderId": "/serviceProviders/serviceBus"
            }
        }
    }
}
```

The testing framework will replace the Service bus trigger with a HTTP trigger that expects a JSON request using HTTP POST:

```json
"triggers": {
    "manual": {
        "type": "Request",
        "kind": "Http",
        "inputs": {
          "schema": {}
        }
    }
}
```

The structure of the HTTP trigger request used by a test case must match the JSON that would have been created by the original Service Bus trigger. This is an example of a request that matches the Service Bus trigger shown above:

```json
{
    "contentData": {
        "id": 1234,
        "title": "Mr",
        "firstName": "Peter",
        "lastName": "Smith",
        "dateOfBirth": "1970-04-25"
    },
    "contentType": "application/json",
    "userProperties": {
        "operation": "update",
        "source": "hr",
        "type": "employee"
    },
    "messageId": "cdc0df50-4c73-4360-882a-af6400bf3aac",
    "label": "employee-1234",
    "scheduledEnqueueTimeUtc": "1/1/0001 12:00:00 AM",
    "sessionId": "1234",
    "timeToLive": "01:00:00",
    "deliveryCount": 1,
    "enqueuedSequenceNumber": 13961,
    "enqueuedTimeUtc": "2022-12-07T11:36:24.115Z",
    "lockedUntilUtc": "9999-12-31T23:59:59.9999999Z",
    "lockToken": "24da398b-3405-4acf-bc7c-d57ca2552d76",
    "sequenceNumber": 5850
}
```

The Service Bus message metadata is just attributes in the JSON message.

The test execution log will include logging to show when a non-HTTP trigger has been replaced with a HTTP trigger:

```txt
Replacing workflow trigger 'When_messages_are_available_in_a_topic_subscription_(peek-lock)' with a HTTP Request trigger.
```

## Workflow Actions and Built-In Connectors 

A workflow action can communicate with an external service using a [built-in service provider connector](https://learn.microsoft.com/en-us/azure/connectors/built-in) which runs in-process with the workflow. The configuration of the built-in connector is part of the workflow definition. There are many different types of built-in service provider connector, for example Service Bus, Event Grid, SQL Server, SMTP and Salesforce.

When unit testing a workflow with a built-in connector, any dependency on an external service needs to be removed. The testing framework does this by replacing a non-HTTP connector with a HTTP connector that is configured to call a mock HTTP server that is managed by the testing framework. This allows the action to run independently of any external dependency.

Replacing the connector like this does not affect the functionality of the workflow action or change the behaviour. Every action generates an *input* JSON message which is then sent to the external service via the connector. The action then generates an *output* JSON message which is then processed by the rest of the workflow. The structure of the *input* and *output* JSON messages differs for each type of action and connector, but as long as the same message structures are used in the request and responses for the HTTP connector and mock HTTP server, the rest of the workflow will execute in exactly the same way.

The testing framework will only replace actions using built-in connectors where the action's `operationId` is listed in the `workflow.builtInConnectorsToMock` section in the `testConfiguration.json` file. The example below will enable the update of all actions that use the `executeQuery` (SQL Server) and `sendMessage` (Service Bus) operations:

```json
"workflow": {
    "builtInConnectorsToMock": [
      "executeQuery",
      "sendMessage"
    ]
}
```

As an example, this is an `Execute Query` action that runs a SQL query against a database and receives a response:

```json
"Execute_Query": {
    "type": "ServiceProvider",
    "inputs": {
        "parameters": {
            "query": "SELECT LanguageName, LanguageCode FROM config.Languages WHERE LanguageCode = @LanguageCode",
            "queryParameters": {
                "LanguageCode": "@body('Parse_Customer')?['languageCode']"
            }
        },
        "serviceProviderConfiguration": {
            "connectionName": "sql",
            "operationId": "executeQuery",
            "serviceProviderId": "/serviceProviders/sql"
        }
    }
}
```

The testing framework will replace the action with a HTTP action that calls the mock HTTP server using a POST operation:

```json
"Execute_Query": {
    "type": "Http",
    "inputs": {
        "method": "POST",
        "uri": "http://local-server-name:7075/Execute_Query",
        "body": {
            "query": "SELECT LanguageName, LanguageCode FROM config.Languages WHERE LanguageCode = @LanguageCode",
            "queryParameters": {
                "LanguageCode": "@body('Parse_Customer')?['languageCode']"
            }
        },
        "retryPolicy": {
            "type": "none"
        }
    },
    "operationOptions": "DisableAsyncPattern, SuppressWorkflowHeaders"
}
```

The contents of the `parameters` attribute in the original action configuration is included in the JSON request body that is sent to the mock HTTP server. This includes the SQL query and any parameters and their values. The request is sent to the mock HTTP server using a URL that includes the action name. The test case can assert the contents of the request to ensure that the SQL query is correct and any parameter values match expectations. Other built-in service provider connector types work in exactly the same way - the contents of the `parameters` attribute is always included in the request body for the mock HTTP server.

The test execution log will include logging to show when a non-HTTP action has been replaced with a HTTP action:

```txt
Replacing workflow actions using a built-in connector with a HTTP action for the mock test server:
    Execute_Query:
      Connector Type: /serviceProviders/sql/executeQuery
      Mocked URL: http://local-server-name:7075/Execute_Query
    Send_message_to_Topic:
      Connector Type: /serviceProviders/serviceBus/sendMessage
      Mocked URL: http://local-server-name:7075/Send_message_to_Topic
```

## Workflow Actions and Managed API Connectors 

A workflow action can also communicate with an external service using a [managed API connector](https://learn.microsoft.com/en-us/azure/connectors/managed#standard-connectors). These connectors run outside of the Logic App in a Microsoft-hosted Azure environment. A `connections.json` file contains  configuration to map a named connection in the workflow definition to an instance of a Microsoft-hosted API connector. The managed API connector is invoked by the workflow using a HTTP call and the URL for the API connector is stored in the `connectionRuntimeUrl` attribute in the `connections.json` file.

When unit testing a workflow that uses a managed API connector, the dependency on the Microsoft-hosted API connector needs to be removed. The testing framework does this by updating the `connections.json` file and replacing the host name in each `connectionRuntimeUrl` attribute with the host name for a mock HTTP server that is managed by the testing framework. This allows workflow actions that use the connection to run independently of the Microsoft-hosted API connector.

If the value of `connectionRuntimeUrl` attribute includes `@appsetting()` references, these references are replaced with the values defined in the `local.settings.json` file, before the host name is replaced.

Updating the connection URL like this does not affect the functionality of the workflow action or change the behaviour. Every action generates an *input* JSON message which is then sent to the external service via the connector. The action then generates an *output* JSON message which is then processed by the rest of the workflow. The structure of the *input* and *output* JSON messages differs for each type of action and API connector, but as long as the same message structures are used in the request and responses for the mock HTTP server, the rest of the workflow will execute in exactly the same way.

As an example, this is a managed API connection in the `connections.json` file for Salesforce:

```json
"salesforce": {
    "api": {
        "id": "/subscriptions/c1661296-a732-44b9-8458-d1a0dd19815e/providers/Microsoft.Web/locations/uksouth/managedApis/salesforce"
    },
    "connection": {
        "id": "/subscriptions/c1661296-a732-44b9-8458-d1a0dd19815e/resourceGroups/rg-uks-01/providers/Microsoft.Web/connections/salesforce01"
    },
    "connectionRuntimeUrl": "https://7606763fdc09952f.10.common.logic-uksouth.azure-apihub.net/apim/salesforce/fba515601ef14f9193eee596a9dcfd1c/",
    "authentication": {
        "type": "Raw",
        "scheme": "Key",
        "parameter": "salesforce-connection-key"
    }
}
```

The testing framework will replace the host name in the `connectionRuntimeUrl` attribute (`https://7606763fdc09952f.10.common.logic-uksouth.azure-apihub.net`) with the host name of the mock HTTP server:

```json
"connectionRuntimeUrl": "http://local-server-name:7075/apim/salesforce/fba515601ef14f9193eee596a9dcfd1c/",
```

When the workflow is run, any request generated by actions using the connection will be sent to the mock HTTP server instead of the Microsoft-hosted API connector. 

The test execution log will include logging to show when a managed API connection in the `connections.json` file has been updated to use the mock HTTP server:

```txt
Updating connections file for managed API connectors:
    salesforce:
      https://7606763fdc09952f.10.common.logic-uksouth.azure-apihub.net/apim/salesforce/fba515601ef14f9193eee596a9dcfd1c/ ->
        http://local-server-name:7075/apim/salesforce/fba515601ef14f9193eee596a9dcfd1c/
    outlook:
      https://7606763fdc09952f.10.common.logic-uksouth.azure-apihub.net/apim/outlook/79a0bc680716416e90e17323b581695d/ ->
        http://local-server-name:7075/apim/outlook/79a0bc680716416e90e17323b581695d/
```

## External URLs configured in the `local.settings.json` file

A workflow may include actions that call APIs and services using the HTTP connector. It is recommended that the URL for the API or service is stored as a setting in the `local.settings.json` file and not hard-coded in the workflow definition - this allows the workflow to be promoted through environments more easily.

The testing framework can be configured to replace the host name in these URLs with a URL for a mock HTTP server that is managed by the testing framework. The host names to be replaced are configured using the `workflow.externalApiUrlsToMock` section in the `testConfiguration.json` file:

```json
"workflow": {
  "externalApiUrlsToMock": [
    "https://external-service-one.testing.net",
    "https://external-service-two.testing.net"
  ]
}
```

So this setting:

```json
"ServiceOne-Url": "https://external-service-one.testing.net/api/v1/employee",
```

Will be updated to:

```json
"ServiceOne-Url": "https://local-server-name:7075/api/v1/employee",
```

NOTE: If the API or service URL is hard-coded in a workflow definition, it will not be updated by the testing framework.


## Retry Policies

Workflow actions that communicate with external dependencies can be configured for [automatic retry in the case of failure](https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-exception-handling#retry-policies). Although automatic reties are desirable in a production workflow, they are less useful in a unit test which is focussing on testing the workflow functionality and needs to complete relatively quickly. For example, a workflow with a HTTP action configured to retry 4 times with an exponential back-off is going to take a while to run when testing a scenario where the called service returns a HTTP 500 response.

The testing framework will automatically modify a workflow to remove any retry policies and replace with a retry policy of type `none`. This ensures that failure scenarios can be tested without automatic retries and long test durations.

As an example, this HTTP action includes an exponential retry policy that retries 4 times:

```json
"Get_Record": {
    "type": "Http",
    "inputs": {
        "method": "GET",
        "uri": "http://something.com/api/v1",
        "retryPolicy": {
            "type": "exponential",
            "count": 4,
            "interval": "PT7S",
            "minimumInterval": "PT5S",
            "maximumInterval": "PT30S"
        }
    }
}
```

The testing framework will replace the existing retry policy with a new policy of type `none`:

```json
"Get_Record": {
    "type": "Http",
    "inputs": {
        "method": "GET",
        "uri": "http://something.com/api/v1",
        "retryPolicy": {
            "type": "none"
        }
    }
}
```

The test execution log will include logging to show what actions have been updated:

```
Updating workflow HTTP actions to remove any existing Retry policies and replace with a 'none' policy:
    Get_Record
```

# Summary of Test Configuration Options

The previous sections describe how the `testConfiguration.json` file can be used to configure the test setup and execution. This is a summary of all of the available configuration attributes:

| Setting name | Optional? | Permitted values | Description |
|:-------------|:----------|:-----------------|:------------|
| localSettingsFilename | Yes | Valid filename | Name of the setting file to be used, if different from the default of `local.settings.json`. |
| azurite.enableAzuritePortCheck | Yes | `true` <br /> `false` | `true` if the framework is to check that Azurite is running and listening on the required ports, otherwise `false`. Default is `true`. |
| azurite.blobServicePort | Yes | 0 -> 65535 | Port number of the Azurite Blob service, if different from the default port 10000. |
| azurite.queueServicePort | Yes | 0 -> 65535 | Port number of the Azurite Queue service, if different from the default port 10001. |
| azurite.tableServicePort | Yes | 0 -> 65535 | Port number of the Azurite Table service, if different from the default port 10002. |
| logging.writeFunctionRuntineStartupLogs | Yes | `true` <br /> `false` | `true` if the start-up logs are to be included in the test execution logs, otherwise `false`. Default is `false`. |
| workflow.externalApiUrlsToMock | Yes | List of host names | List of host names that are to be replaced in the settings file with the URL of the mock HTTP server. |
| workflow.builtInConnectorsToMock | Yes | List of connector names | List of built-in connector names where actions using these connectors are to be replaced with HTTP actions pointing at the mock HTTP server. |
| workflow.autoConfigureWithStatelessRunHistory | Yes | `true` <br /> `false` | `true` if the testing framework automatically sets the `Workflows.<workflow name>.OperationOptions` setting to `WithStatelessRunHistory` for stateless workflows, otherwise `false`. Default is `true`. |


# Future Improvements and Changes

This is a list of possible future improvements and changes for the framework. Please create a new issue in GitHub if there are other features that you would like to see.

- Ability to mock an `Invoke workflow` action to remove dependencies on a called workflow.
- Improve the creation of the mocked responses using the mock HTTP server, perhaps using Fluent notation to create the responses.
- Reduce the number of dependent packages.