# LogicAppUnit Testing Framework

<img align="right" src="https://raw.github.com/LogicAppUnit/TestingFramework/main/LogicAppUnit.png" width="130" />

LogicAppUnit is a testing framework that simplifies the creation of automated unit tests for Standard Logic Apps running in a *local development environment*, or in a *build server as part of a DevOps pipeline*. Standard Logic Apps do not include an out-of-the-box testing capability and this framework has been designed to fill this gap. The framework is based on the [Logic Apps Sample Test Framework](https://techcommunity.microsoft.com/t5/integrations-on-azure-blog/automated-testing-with-logic-apps-standard/ba-p/2960623) that was developed by Henry Liu, and includes additional functionality to make it easier to author and run tests and validate (assert) the results.

The framework does not support the testing of:

- Consumption Logic App workflows.
- Standard Logic App workflows that have been deployed to Azure.

The testing framework has been designed to make it easier to perform isolated unit testing of a workflow. The framework does this by modifying a copy of the workflow definition to remove the dependencies on external services and APIs, without affecting the functionality or behaviour of the workflow. This means that workflows can be easily tested in a developer's local environment, and by a DevOps pipeline running on a build server, where there is no access to Azure services or any other workflow dependencies.

## Key Features

- Replace non-HTTP triggers with HTTP triggers to enable automated testing of every workflow, irrespective of the trigger type.
- Remove external service dependencies for built-in service provider connectors by replacing these actions with HTTP actions and a mock HTTP server that is managed by the framework.
- Remove external service dependencies for managed API connectors by automatically re-configuring managed API connections to use a mock HTTP server that is managed by the framework.
- Remove dependencies on invoked workflows by replacing the Invoke Workflow actions with HTTP actions and a mock HTTP server that is managed by the framework.
- Remove all retry policies to ensure that tests exercising failure scenarios do not take a long time to execute.
- A fluent API to configure request matching and the creation of responses for the mock HTTP server.
- Detailed test execution logging to help with workflow test authoring and debugging.
- Programmatic access to the workflow run history to enable assertion of workflow run status, response status, action status, input and output messages and more. This includes support for action repetitions inside a loop.
- Programmatic access to the requests sent to the mock HTTP server to enable assertion of the data sent from the workflow to external services and APIs.
- Override specific local settings for a test case to enable more testing scenarios (e.g. feature flags).


## Projects

This code repository includes three projects:

| Name | Description |
|:-----|:------------|
| LogicAppUnit | The testing framework. |
| LogicAppUnit.Samples.LogicApps.Tests | Sample test project that demonstrates the features of the testing framework. 
| LogicAppUnit.Samples.LogicApps | Workflows that are tested by the sample test project. |


## Packages

Download the *LogicAppUnit* testing framework package from nuget: https://www.nuget.org/packages/LogicAppUnit/

[![NuGet Badge](https://buildstats.info/nuget/LogicAppUnit)](https://www.nuget.org/packages/LogicAppUnit)


## Compatibility

The framework has been tested with these environments:

- Windows
- Linux (Ubuntu)
- MacOS


## Main Contributors

- [Mark Abrams](https://github.com/mark-abrams)
- [Sanket Borhade](https://github.com/sanket-borhade)
- [Shadhaj Kumar](https://github.com/shadhajSH)


## Documentation

The best way to understand how the framework works and how to write tests using it is to read the [wiki](https://github.com/LogicAppUnit/TestingFramework/wiki) and look at the example tests in the *LogicAppUnit.Samples.LogicApps.Tests* project.


## Future Improvements and Changes

This is a list of possible future improvements and changes for the framework. Please create a [new issue](https://github.com/LogicAppUnit/TestingFramework/issues) if there are other features that you would like to see.

- Add more features to the fluent API for request matching and the creation of mock responses.
- Add a feature to the fluent API to allow a mock response to be matched using just the name of the workflow action that created the request. This would make it easier to match the request, compared to using multiple properties of the request such as the HTTP method, URI path and/or request content.
- Add a `Verifiable()` feature to the fluent API so that a test case can assert that a test execution did send a request to the mock HTTP server that was successfully matched. This would work in a simialar way to the `Verifiable()` feature in the [moq](https://github.com/devlooped/moq) unit testing framework.
- Support the `Call a local function` action when calling a .NET Framework function from a Logic App workflow.
- Auto-generate C# test cases based on a workflow's run history in the local development environment.