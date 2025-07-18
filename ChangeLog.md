# 1.12.0 (18th July 2025)

LogicAppUnit Testing Framework:

- HTTP actions with the authentication type set to `ManagedServiceIdentity` are updated to use the `None` authentication type. [[Issue #49](https://github.com/LogicAppUnit/TestingFramework/issues/49)], [[Issue #50](https://github.com/LogicAppUnit/TestingFramework/issues/50)] and [[PR #51](https://github.com/LogicAppUnit/TestingFramework/pull/51), [@ronaldbosma ](https://github.com/ronaldbosma)]


LogicAppUnit.Samples.LogicApps.Tests:

- Added a `http-with-managed-identity-workflow` workflow and unit test to test a workflow that includes a HTTP action with the authentication type set to `ManagedServiceIdentity`.


# 1.11.0 (11th April 2025)

LogicAppUnit Testing Framework:

- The framework now supports parameterised connections that are created by the Standard Logic App extension. [[Issue #42](https://github.com/LogicAppUnit/TestingFramework/issues/42)]
- Bumped versions of NuGet packages to remove critical vulnerabilities in some of the transitive packages.
- Added configuration for NuGet Audit so that any future vulnerabilities are logged as build warnings and do not break the LogicAppUnit build. [[Issue #40](https://github.com/LogicAppUnit/TestingFramework/issues/40)]
- Updated method `ContentHelper.FormatJson()` to use `JToken.Parse()` instead of `JObject.Parse()`. [[Issue #45](https://github.com/LogicAppUnit/TestingFramework/issues/45)]
- Added new property `TestRunner.WorkflowTerminationCodeAsString` that returns the workflow termination code as a string value. The existing property `TestRunner.WorkflowTerminationCode` returns the code as an integer value, but the code is defined as a string data type in the workflow schema reference documentation. [[Issue #46](https://github.com/LogicAppUnit/TestingFramework/issues/46)]


# 1.10.0 (4th November 2024)

LogicAppUnit Testing Framework:

- The testing of workflows that call in-line C# is now supported. The .csx script files are copied to the test working directory and are not mocked. [[PR #35](https://github.com/LogicAppUnit/TestingFramework/pull/35), [@sschutten](https://github.com/sschutten)]
- Added a new configuration option `workflow.managedApisToMock` in the `testConfiguration.json` file to control which Managed API connectors are mocked. If this configuration is not set, all Managed API connectors are mocked - this ensures backwards compatabiity with previous versions. [[PR #38](https://github.com/LogicAppUnit/TestingFramework/pull/38), [@zzznz27](https://github.com/zzznz27)]

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `call-data-mapper-workflow` workflow and unit tests to test workflows that call the data mapper.
- Added a `inline-script-workflow` workflow and unit tests to test workflows that call in-line C# script (.csx) files.


# 1.9.0 (23rd January 2024)

LogicAppUnit Testing Framework:

- Improved the logging when using the Fluent API to match requests based on request headers or query parameters. When the actual and expected header or query parameter values do not match, both values are logged to make it easier to diagnose any test issues. Previous versions of the framework logged the match failure but did not log the actual and expected values.
- Added `IMockRequestMatcher.FromAction(string[] actionNames)` to allow a mock request matcher to match a request based on the name of the workflow action that created the request. This feature depends on the `x-ms-workflow-operation-name` header being present in the request. Refer to the [wiki](https://github.com/LogicAppUnit/TestingFramework/wiki) for details of when the Logic App runtime creates this header.
- Retry policies for actions using a managed API connection are removed and replaced with a `none` policy. This is the same pre-processing that is applied to HTTP actions. Previous versions of the framework did not remove the retry policies for actions using a managed API connection which meant that tests could take a long time to complete if they were testing failure scenarios.
- The framework checks the `connections.json` file and will fail a test if there are any managed API connections that are configured using the `ManagedServiceIdentity` authentication type. The Logic Apps runtime only supports the `Raw`
 and `ActiveDirectoryOAuth` authentication types when running in a local developer environment. [[Issue #30](https://github.com/LogicAppUnit/TestingFramework/issues/30)]
 - The `testConfiguration.json` file is now optional. If the file does not exist, or contains an empty JSON document (`{}`), the default values are used for all settings. Previous versions of the framework would fail a test if the configuration file did not exist. [[Issue #28](https://github.com/LogicAppUnit/TestingFramework/issues/28)]
 - `Call a local function` actions are now mocked using HTTP actions. This means that the dependencies between a workflow and a .NET Framework function can be broken to enable better unit testing of the workflow.
 - Added `IMockResponseBuilder.ThrowsException(Exception exceptionToThrow)` to simulate an exception being thrown by a local .NET Framework function.
 - Fixed a typo in the name of the `logging.writeFunctionRuntimeStartupLogs` configuration setting. Previously the setting was named `logging.writeFunctionRuntineStartupLogs` (note the incorrect spelling `Runtine`). [[PR #29](https://github.com/LogicAppUnit/TestingFramework/pull/29), [@jeanpaulsmit](https://github.com/jeanpaulsmit)] <br /> :warning: ***This is a breaking change. Any use of the `writeFunctionRuntineStartupLogs` setting in the `testConfiguration.json` file will need to be updated.***

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `call-local-function-workflow` workflow and unit tests to demonstrate the mocking of a local .NET Framework function.


# 1.8.0 (24th October 2023)

LogicAppUnit Testing Framework:

- Added new properties `TestRunner.WorkflowWasTerminated`, `TestRunner.WorkflowTerminationCode` and `TestRunner.WorkflowTerminationMessage` to allow the effects of a _Terminate_ action in a workflow to be tested.
- Mock responses can be configured using the fluent API in the test class initialization method, using the `WorkflowTestBase.AddMockResponse()` method. Mock responses configured using this method will be used by every test runner that is created in the same test class, and have a lower priority compared to the mock responses created using `ITestRunner.AddMockResponse()` and `ITestRunner.AddApiMocks`. This feature removes the need to repeatedly configure the same mocked responses in multiple tests in a test class.
- The HTTP status code for the default mock response can now be set in the `testConfiguration.json` file using the `runner.defaultHttpResponseStatusCode` option. Previously the status code was hard-coded to HTTP 200 (OK). The default value for this option is HTTP 200 (OK) to ensure backwards compatibility.
- Added a new feature to remove the chunking configuration for HTTP actions (`runtimeConfiguration.contentTransfer.transferMode`). This feature is enabled/disabled in the `testConfiguration.json` file using the `workflow.removeHttpChunkingConfiguration` option. The default value for this option is `true`. [[Issue #24](https://github.com/LogicAppUnit/TestingFramework/issues/24)]
- Added `IMockResponseBuilder.WithAccepted()` as a short-cut when creating a response with a HTTP 202 (Accepted) status code.


# 1.7.0 (27th July 2023)

LogicAppUnit Testing Framework:

- Mock responses can be configured using `ITestRunner.AddMockResponse()` and a fluent API, this includes the definition of the request matching conditions and the response. 
- Removed public methods `ContentHelper.SerializeObject()`, `ContentHelper.DeserializeObject()` and `ContentHelper.JClone()`, these were for internal use only and are now obsolete.
- Include the LogicAppUnit version at the end of the test log.
- The maximum execution time for a workflow can now be set in the `testConfiguration.json` file using the `runner.maxWorkflowExecutionDuration` option. Previously this duration was hard-coded to 5 minutes. The default value for this option is 300 seconds (5 minutes).
- Improved the logic that locates the Azure Functions runtime executable (`func.exe`) when running on a Windows platform. Previous versions of the framework used the PATH environment variable in the `Machine` scope which caused issues when tests were running in an Azure DevOps pipeline (because the `FuncToolsInstaller@0` task adds the path for `func.exe` to the PATH environment variable in the `Process` scope). Now the framework combines the PATH environment variables for the `Machine`, `Process` and `User` scopes to ensure that all possible paths are checked. [[PR #20](https://github.com/LogicAppUnit/TestingFramework/pull/20), [@AlexanderDobrescu](https://github.com/AlexanderDobrescu) and [PR #21](https://github.com/LogicAppUnit/TestingFramework/pull/21), [@danielduartemindera](https://github.com/danielduartemindera)]

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `fluent-workflow` workflow and unit tests to demonstrate the use of the fluent API.


# 1.6.0 (5th June 2023)

LogicAppUnit Testing Framework:

- Added support for Linux and MacOS platforms. [[PR #14](https://github.com/LogicAppUnit/TestingFramework/pull/14), [@easchi](https://github.com/eashi) and [PR #15](https://github.com/LogicAppUnit/TestingFramework/pull/15), [@atthevergeof](https://github.com/atthevergeof)]
- Added new overloads to `TestRunner.TriggerWorkflow()` to configure URL query parameters when triggering a workflow with a HTTP trigger. [[PR #15](https://github.com/LogicAppUnit/TestingFramework/pull/15), [@atthevergeof](https://github.com/atthevergeof)]
- Added support for workflows using a HTTP trigger with an asynchronous response. Previous versions of the framework assumed that all responses were synchronous. Now the framework handles a 202 (Accepted) response and uses the callback URL (in the `Location` header) to poll the workflow and get the response. [[PR #15](https://github.com/LogicAppUnit/TestingFramework/pull/15), [@atthevergeof](https://github.com/atthevergeof)]
- Added support for workflows using a HTTP trigger where the response action is not the last action in the workflow. Previous versions of the framework assumed that the workflow was complete once the response was received. Now the framework polls the workflow status to ensure that the workflow has completed. [[PR #15](https://github.com/LogicAppUnit/TestingFramework/pull/15), [@atthevergeof](https://github.com/atthevergeof)]
- Fixed a bug in `TestRunner.TriggerWorkflow()` where the return value was being incorrectly set to the (disposed) workflow run history API response. The response is now correctly set to the workflow trigger API response. This bug only occurred for workflows that have a non-HTTP trigger (which is then replaced by a HTTP trigger by the framework).
  <br />
  :warning: ***This is a breaking change. Previously the status code for the response would have been HTTP 200 (OK), now it will be HTTP 202 (Accepted).***

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `http-async-workflow` workflow and unit tests to demonstrate the use of the testing framework with HTTP triggers and asynchronous responses.


# 1.5.0 (14th April 2023)

LogicAppUnit Testing Framework:

- Invoked child workflows are now mocked using HTTP actions. This means that the dependencies between a parent workflow and child workflows can be broken to enable better unit testing of the parent workflow.
- `LogicAppUnit.TestRunner` does not assume that the name of the HTTP trigger is `manual`, it now retrieves the name from the workflow definition.
  - This change is needed because the new Standard Logic App designer allows a developer to edit the name of the HTTP trigger. In all previous versions of the designer the trigger name was set to `manual` and could not be changed.
- Non-HTTP triggers that are replaced with HTTP triggers now have the same name as the original trigger. Previously, the name of the HTTP trigger was set to `manual`.

LogicAppUnit.Samples.LogicApps.Tests:

- Added an `invoke-workflow` workflow and unit tests to demonstrate the use of the testing framework with child workflows that are invoked from a parent workflow.


# 1.4.0 (21st February 2023)

LogicAppUnit Testing Framework:

- Changed the logic that updates the `connectionRuntimeUrl` for Managed API connectors so that it works with URL values that include `@appsetting()` references. [[Issue #9](https://github.com/LogicAppUnit/TestingFramework/issues/9)]


# 1.3.0 (1st February 2023)

LogicAppUnit Testing Framework:

- Added methods to `LogicAppUnit.TestRunner` to allow tests to access the tracked properties that are created by an action. This includes action repetitions.
  - This is only available for stateful workflows because tracked properties are never recorded in the run history for stateless workflows.
- Updated `LogicAppUnit.Helper.ContentHelper.FormatJson(string)` so that any references to the local server name are replaced with `localhost`.

LogicAppUnit.Samples.LogicApps.Tests:

- Updated the `http-workflow` workflow and unit tests to include tracked properties.


# 1.2.0 (9th January 2023)

LogicAppUnit Testing Framework:

- Added methods to `LogicAppUnit.TestRunner` to allow tests to assert actions that run in an `Until` loop or a `ForEach` loop. These actions are known as action repetitions.
- Added methods to `LogicAppUnit.TestRunner` to allow tests to access the input and output messages for an action. This includes action repetitions.
- Added an interface `LogicAppUnit.ITestRunner` and updated `LogicAppUnit.TestRunner` to implement this interface. This interface has been added to allow for the implementation of other test runners in the future.
- Method `LogicAppUnit.WorkflowTestBase.CreateTestRunner()` returns an instance of `LogicAppUnit.ITestRunner` and not `LogicAppUnit.TestRunner`.

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `loop-workflow` workflow and unit tests to demonstrate the use of the testing framework with a workflow containing actions in an `Until` loop and a `ForEach` loop.


# 1.1.0 (16th December 2022)

LogicAppUnit Testing Framework:

- Changed the visibility of the `LogicAppUnit.Hosting` classes from `public` to `internal`. These classes are not for use by test authors.
- Added a new configuration option `azurite.enableAzuritePortCheck` to `testConfiguration.json` to enable or disable the Azurite port checks.
- Refactored the internal classes that update the workflow definition, local settings and connection files.
- The Test Runner (`LogicAppUnit.TestRunner`) now supports workflow HTTP triggers with relative paths.
- Improved handling of stateless workflows:
  - Added a new configuration option `workflow.autoConfigureWithStatelessRunHistory` to `testConfiguration.json` to control whether the testing framework automatically configures the workflow `OperationOptions` setting to `WithStatelessRunHistory`. If this option is not set for stateless workflows, the workflow run history is not stored. The default value for this configuration option is `true`.
  - If a stateless workflow is tested and the `OperationOptions` setting is not set to `WithStatelessRunHistory`, and `workflow.autoConfigureWithStatelessRunHistory` is set to `false`, the test fails with a `TestException` error.
- Added the `TestRunner.WorkflowClientTrackingId` property so that tests can assert a workflow's client tracking id.
- Improvements to `Readme.md`.

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `stateless-workflow` workflow and unit tests to demonstrate the use of the testing framework with a stateless workflow, a custom client tracking id and a relative path configured in the HTTP trigger.


# 1.0.0 (9th December 2022)

- Initial version.
