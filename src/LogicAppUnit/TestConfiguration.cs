﻿using System.Collections.Generic;

namespace LogicAppUnit
{
    /// <summary>
    /// Configuration that can be set by a test project to configure how tests are set up and executed.
    /// </summary>
    public class TestConfiguration
    {
        /// <summary>
        /// Name of the local settings JSON file. This is optional and is only used when a non-standard filename is used.
        /// </summary>
        public string LocalSettingsFilename { get; set; }

        /// <summary>
        /// Azurite configuration. Azurite is a dependency for running Logic App workflows.
        /// </summary>
        public TestConfigurationAzurite Azurite { get; set; }

        /// <summary>
        /// Logging configuration for test execution.
        /// </summary>
        public TestConfigurationLogging Logging { get; set; }

        /// <summary>
        /// Test runner configuration for test execution.
        /// </summary>
        public TestConfigurationRunner Runner { get; set; }

        /// <summary>
        /// Workflow configuration, controls how the workflow definition is modified to enable mocking.
        /// </summary>
        public TestConfigurationWorkflow Workflow { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestConfiguration"/> class.
        /// </summary>
        public TestConfiguration()
        {
            Azurite = new TestConfigurationAzurite();
            Logging = new TestConfigurationLogging();
            Runner = new TestConfigurationRunner();
            Workflow = new TestConfigurationWorkflow();
        }
    }

    /// <summary>
    /// Configuration for test execution logging.
    /// </summary>
    public class TestConfigurationLogging
    {
        /// <summary>
        /// <c>true</c> if the Functions runtime start-up logs are to be written to the test execution logs, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>false</c>.
        /// </remarks>
        public bool WriteFunctionRuntimeStartupLogs { get; set; }   // default is false

        /// <summary>
        /// <c>true</c> if the mock request matching logs are to be written to the test execution logs, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>false</c>.
        /// </remarks>
        public bool WriteMockRequestMatchingLogs { get; set; }      // default is false
    }

    /// <summary>
    /// Configuration for the Test Runner.
    /// </summary>
    public class TestConfigurationRunner
    {
        /// <summary>
        /// Maximum time (in seconds) to poll for the workflow result. The Test Runner will fail any test where the workflow execution is longer than this value.
        /// </summary>
        /// <remarks>
        /// Default value is 300 seconds (5 minutes).
        /// </remarks>
        public int MaxWorkflowExecutionDuration { get; set; } = 300;

        /// <summary>
        /// The HTTP status code for the default mock response, used when no mock Request Matchers are matched and when the mock response delegate function is not set, or returns <c>null</c>.
        /// </summary>
        /// <remarks>
        /// Default value is HTTP 200 (OK).
        /// </remarks>
        public int DefaultHttpResponseStatusCode { get; set; } = 200;
    }

    /// <summary>
    /// Configuration for Azurite. Azurite is a dependency for running Logic App workflows.
    /// </summary>
    public class TestConfigurationAzurite
    {
        /// <summary>
        /// <c>true</c> if the test framework checks that Azurite is running and listening on the required ports, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>true</c>.
        /// </remarks>
        public bool EnableAzuritePortCheck { get; set; } = true;

        /// <summary>
        /// Port number used by the Blob service.
        /// </summary>
        public int BlobServicePort { get; set; } = 10000;

        /// <summary>
        /// Port number used by the Queue service.
        /// </summary>
        public int QueueServicePort { get; set; } = 10001;

        /// <summary>
        /// Port number used by the Table service.
        /// </summary>
        public int TableServicePort { get; set; } = 10002;
    }

    /// <summary>
    /// Configuration for the workflow, controls how the workflow definition is modified to enable mocking.
    /// </summary>
    public class TestConfigurationWorkflow
    {
        /// <summary>
        /// List of external API URLs that are to be replaced with the test mock server.
        /// </summary>
        public List<string> ExternalApiUrlsToMock { get; set; } = new List<string>();

        /// <summary>
        /// List of built-in connectors where the actions are to be replaced with HTTP actions referencing the test mock server.
        /// </summary>
        public List<string> BuiltInConnectorsToMock { get; set; } = new List<string>();


        /// <summary>
        /// List of managed api connectors where the actions are to be replaced with HTTP actions referencing the test mock server.
        /// </summary>
        public List<string> ManagedApisToMock { get; set; } = new List<string>();


        /// <summary>
        /// <c>true</c> if the test framework automatically configures the <i>OperationOptions</i> setting to <i>WithStatelessRunHistory</i> for a stateless workflow, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>true</c>.
        /// </remarks>
        public bool AutoConfigureWithStatelessRunHistory { get; set; } = true;

        /// <summary>
        /// <c>true</c> if the retry configuration in HTTP actions is to be removed, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>true</c>.
        /// </remarks>
        public bool RemoveHttpRetryConfiguration { get; set; } = true;

        /// <summary>
        /// <c>true</c> if the chunking configuration in HTTP actions is to be removed, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>true</c>.
        /// </remarks>
        public bool RemoveHttpChunkingConfiguration { get; set; } = true;

        /// <summary>
        /// <c>true</c> if the retry configuration in actions using managed API connections is to be removed, otherwise <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Default value is <c>true</c>.
        /// </remarks>
        public bool RemoveManagedApiConnectionRetryConfiguration { get; set; } = true;
    }
}
