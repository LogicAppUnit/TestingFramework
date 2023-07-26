# 1.7.0 (27th July 2023)

LogicAppUnit Testing Framework:

- Mock responses can be configured using a fluent API, this includes the definition of the request matching conditions and the response. 
- Removed public methods `ContentHelper.SerializeObject()`, `ContentHelper.DeserializeObject()` and `ContentHelper.JClone()`, these were for internal use only and are now obsolete.
- Include the LogicAppUnit version at the end of the test log.
- The maximum execution time for a workflow can now be set in the `testConfiguration.json` file using the `runner.maxWorkflowExecutionDuration` option. Previously this duration was hard-coded to 5 minutes. The default value for this option is 300 seconds (5 minutes).
- Improved the logic that locates the Azure Functions runtime executable (`func.exe`) when running on a Windows platform. Previous versions of the framework used the PATH environment variable in the `Machine` scope which caused issues when tests were running in an Azure DevOps pipeline (because the `FuncToolsInstaller@0` task adds the path for `func.exe` to the PATH environment variable in the `Process` scope). Now the framework combines the PATH environment variables for the `Machine`, `Process` and `User` scopes to ensure that all possible paths are checked. [[PR #20](https://github.com/LogicAppUnit/TestingFramework/pull/22), [@AlexanderDobrescu](https://github.com/AlexanderDobrescu) and [PR #21](https://github.com/LogicAppUnit/TestingFramework/pull/21), [@danielduartemindera](https://github.com/danielduartemindera)]

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

Thanks to [@easchi](https://github.com/eashi) and [@atthevergeof](https://github.com/atthevergeof) for their contributions.


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
