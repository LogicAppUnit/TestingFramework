# 1.2.0 (9th January 2023)

LogicAppUnit Testing Framework:

- Added methods to `LogicAppUnit.TestRunner` to allow tests to assert actions that run in an `Until` loop or a `ForEach` loop. These actions are known as action repetitions.
- Added methods to `LogicAppUnit.TestRunner` to allow tests to access the input and output messages for an action. This includes action repetitions.
- Added an interface `LogicAppUnit.ITestRunner` and updated `LogicAppUnit.TestRunner` to implement this interface. This interface has been added to allow for the implementation of other test runners in the future.
- Method `LogicAppUnit.WorkflowTestBase.CreateTestRunner()` returns an instance of `LogicAppUnit.ITestRunner` and not `LogicAppUnit.TestRunner`.

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `LoopWorkflowTest` workflow and unit tests to demonstrate the use of the testing framework with a workflow containing actions in an `Until` loop and a `ForEach` loop.


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

- Added a `StatelessWorkflowTest` workflow and unit tests to demonstrate the use of the testing framework with a stateless workflow, a custom client tracking id and a relative path configured in the HTTP trigger.


# 1.0.0 (9th December 2022)

- Initial version.
