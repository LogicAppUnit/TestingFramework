using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LogicAppUnit.Helper;
using LogicAppUnit.Hosting;
using LogicAppUnit.InternalHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace LogicAppUnit
{
    /// <summary>
    /// Base class for all test cases using the Logic App testing framework.
    /// </summary>
    public abstract class WorkflowTestBase
    {
        private readonly static HttpClient client;

        private TestConfiguration testConfig;
        private WorkflowTestInput[] workflowTestInput;
        private DirectoryInfo artifactDirectory;
        private string localSettings;
        private string parameters;
        private string connections;
        private string host;
        private string workflowName;
        private bool workflowIsInitialised = false;

        /// <summary>
        /// Static initializer for a new instance of the <see cref="WorkflowTestBase"/> class.
        /// </summary>
        static WorkflowTestBase()
        {
            if (client == null)
            {
                var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                client = httpClientFactory.CreateClient("funcAppClient");
            }
        }

        /// <summary>
        /// Releases resources held by the test framework.
        /// </summary>
        protected static void Close()
        {
            client?.Dispose();
        }

        /// <summary>
        /// Gets the URI for the mock test workflow host.
        /// </summary>
        /// <remarks>
        /// Use this property when you want to create a request message that contains a callback URL that needs to be pointed at the mock test host.
        /// </remarks>
        protected static string MockTestWorkflowHostUri
        {
            get
            {
                return TestEnvironment.FlowV2MockTestHostUri;
            }
        }

        /// <summary>
        /// Initializes all the workflow specific variables which will be used throughout the test executions.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="workflowName">The name of the workflow. This matches the name of the folder that contains the workflow definition file.</param>
        protected void Initialize(string logicAppBasePath, string workflowName)
        {
            Initialize(logicAppBasePath, workflowName, null);
        }

        /// <summary>
        /// Initializes all the workflow specific variables which will be used throughout the test executions, including the local settings file.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="workflowName">The name of the workflow. This matches the name of the folder that contains the workflow definition file.</param>
        /// <param name="localSettingsFilename">The name of the local settings file to be used, this can be used to override the default of <i>local.settings.json</i>.</param>
        protected void Initialize(string logicAppBasePath, string workflowName, string localSettingsFilename)
        {
            if (string.IsNullOrEmpty(logicAppBasePath))
                throw new ArgumentNullException(nameof(logicAppBasePath));
            if (string.IsNullOrEmpty(workflowName))
                throw new ArgumentNullException(nameof(workflowName));

            const string testConfigFilename = "testConfiguration.json";

            LoggingHelper.LogBanner("Initializing test");

            // Load the test configuration settings
            testConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(testConfigFilename, false, false)
                .Build()
                .Get<TestConfiguration>();
            if (testConfig == null)
                throw new TestException($"The test configuration could not be loaded. Ensure that the test project includes a '{testConfigFilename}' file, it is copied to the output directory and it is formatted correctly.");

            // Make sure Azurite is running
            // If Azurite is not running we want to fail the tests quickly, not wait for the tests to run and then fail
            if (!AzuriteHelper.IsRunning(testConfig.Azurite))
                throw new TestException($"Azurite is not running on ports {testConfig.Azurite.BlobServicePort} (Blob service), {testConfig.Azurite.QueueServicePort} (Queue service) and {testConfig.Azurite.TableServicePort} (Table service). Logic App workflows cannot run unless all three services are running in Azurite");

            // Name of the workflow
            this.workflowName = workflowName;

            // JSON definition of the workflow
            var workflowDefinition = ContentHelper.ReadFromPath(Path.Combine(logicAppBasePath, workflowName, Constants.WORKFLOW));

            // Paths to the Logic App files
            // The name of the local setting file can be set in the test configuration
            localSettings = ContentHelper.ReadFromPath(Path.Combine(logicAppBasePath, SetLocalSettingsFile(localSettingsFilename)));
            parameters = ContentHelper.ReadFromPath(Path.Combine(logicAppBasePath, Constants.PARAMETERS));
            connections = ContentHelper.ReadFromPath(Path.Combine(logicAppBasePath, Constants.CONNECTIONS));
            host = ContentHelper.ReadFromPath(Path.Combine(logicAppBasePath, Constants.HOST));

            // Set up the workflow
            workflowTestInput = new WorkflowTestInput[] { new WorkflowTestInput(workflowName, workflowDefinition) };
            WorkflowHelper.SetupWorkflowForExecution(ref workflowTestInput, testConfig.Workflow.BuiltInConnectorsToMock);

            // Set up the connections
            ConnectionHelper.ReplaceManagedApiConnectionUrlsToFallBackOnMockServer(ref connections);

            // Set up the local settings
            SettingsHelper.ReplaceExternalUrlsToFallBackOnMockServer(ref localSettings, testConfig.Workflow.ExternalApiUrlsToMock);

            // Set up the artifacts (schemas, maps)
            SetArtifactDirectory(logicAppBasePath);

            workflowIsInitialised = true;
        }

        /// <summary>
        /// Create a new instance of the test runner. This is used to run a test for a workflow.
        /// </summary>
        /// <returns>An instance of the rest runner.</returns>
        /// <remarks>
        /// Do not use an instance of a test runner to run multiple workflows.
        /// </remarks>
        protected TestRunner CreateTestRunner()
        {
            return CreateTestRunner(null);
        }

        /// <summary>
        /// Create a new instance of the test runner with overrides for specific local settings. This is used to run a test for a workflow.
        /// </summary>
        /// <param name="localSettingsOverrides">Dictionary containing the local settings to be overridden.</param>
        /// <returns>An instance of the rest runner.</returns>
        /// <remarks>
        /// Do not use an instance of a test runner to run multiple workflows.
        /// </remarks>
        protected TestRunner CreateTestRunner(Dictionary<string, string> localSettingsOverrides)
        {
            // Make sure that the workflow has been initialised
            if (!workflowIsInitialised)
                throw new TestException("Cannot create the test runner because the workflow has not been initialised using Initialize()");

            // Update the local settings if anything needs to  been overridden
            string localSettingsWithOverrides = localSettings;
            if (localSettings != null && localSettingsOverrides != null && localSettingsOverrides.Count > 0)
            {
                localSettingsWithOverrides = SettingsHelper.ReplaceSettingOverrides(localSettings, localSettingsOverrides);
            }

            return new TestRunner(testConfig.Logging, client, workflowName, workflowTestInput, localSettingsWithOverrides, parameters, connections, host, artifactDirectory);

        }

        /// <summary>
        /// Determine the local settings file to be used.
        /// </summary>
        /// <param name="localSettingsFileFromInitialize">The name of the local settings file that was passed into the Initialize method. This is optional.</param>
        /// <returns>The name of the local settings file to be used.</returns>
        private string SetLocalSettingsFile(string localSettingsFileFromInitialize)
        {
            string localSettingsFile;

            // Order of precedence:
            // 1 - File passed into the Initialize() method.
            // 2 - Filename configured in the 'testConfiguration.json' file.
            // 3 - The default 'local.settings.json'
            if (!string.IsNullOrEmpty(localSettingsFileFromInitialize))
                localSettingsFile = localSettingsFileFromInitialize;
            else if (!string.IsNullOrEmpty(testConfig.LocalSettingsFilename))
                localSettingsFile = testConfig.LocalSettingsFilename;
            else
                localSettingsFile = Constants.LOCAL_SETTINGS;

            Console.WriteLine($"Using local settings file: {localSettingsFile}");

            return localSettingsFile;
        }
        /// <summary>
        /// Check if the Logic App has any artifacts or not. If yes then the <i>artifactDirectory</i> variable is set with the path value,
        /// which can be used inside WorkflowTestHost as an input.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        private void SetArtifactDirectory(string logicAppBasePath)
        {
            if (string.IsNullOrEmpty(logicAppBasePath))
                throw new ArgumentNullException(nameof(logicAppBasePath));

            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(logicAppBasePath, Constants.ARTIFACTS_FOLDER));
            if (directoryInfo.Exists)
            {
                artifactDirectory = directoryInfo;
                Console.WriteLine($"Using artifacts directory: {Path.Combine(directoryInfo.Parent.Name, directoryInfo.Name)}");
            }
            else
            {
                artifactDirectory = null;
                Console.WriteLine("Artifacts directory does not exist.");
            }
        }
    }
}
