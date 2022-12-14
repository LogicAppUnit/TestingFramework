## 1.1.0

*19th December 2022*

LogicAppUnit Testing Framework:

- Changed the visability of the `LogicAppUnit.Hosting` classes from `public` to `internal`. These classes are not for use by test authors.
- Added a new configuration option `azurite.enableAzuritePortCheck` to `testConfiguration.json` to enable or disable the Azurite port checks.
- Refactored the internal classes that update the workflow definition, local settings and connection files.
- The Test Runner (`LogicAppUnit.TestRunner`) now supports workflow HTTP triggers with relative paths.
- Improved handling of stateless workflows:
  - Added a new configuration option `workflow.autoConfigureWithStatelessRunHistory` to `testConfiguration.json` to control whether the testing framework automatically configures the workflow `OperationOptions` setting to `WithStatelessRunHistory`. If this option is not set for stateless workflows, the workflow run history is not stored. The default value for this configuration option is `true`.
  - If a stateless workflow is tested and the `OperationOptions` setting is not set to `WithStatelessRunHistory`, and `workflow.autoConfigureWithStatelessRunHistory` is set to `false`, the test fails with a `TestException` error.

LogicAppUnit.Samples.LogicApps.Tests:

- Added a `StatelessWorkflowTest` workflow and unit tests to demonstrate the use of the testing framework with a stateless workflow and a relative path configured in the HTTP trigger.


## 1.0.0

*9th December 2022*

- Initial version.
