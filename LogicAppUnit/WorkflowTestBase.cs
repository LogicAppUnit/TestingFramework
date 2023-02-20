using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly static HttpClient _client;

        private TestConfiguration _testConfig;
        private DirectoryInfo _artifactDirectory;

        private WorkflowHelper _workflowDefinition;
        private SettingsHelper _localSettings;
        private ConnectionHelper _connections;

        private string _parameters;
        private string _host;

        private bool _workflowIsInitialised = false;

        /// <summary>
        /// Static initializer for a new instance of the <see cref="WorkflowTestBase"/> class.
        /// </summary>
        static WorkflowTestBase()
        {
            if (_client == null)
            {
                var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                _client = httpClientFactory.CreateClient("funcAppClient");
            }
        }

        /// <summary>
        /// Releases resources held by the test framework.
        /// </summary>
        protected static void Close()
        {
            _client?.Dispose();
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
            const string workflowOperationOptionsRunHistory = "WithStatelessRunHistory";

            LoggingHelper.LogBanner("Initializing test");

            // Load the test configuration settings
            _testConfig = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(testConfigFilename, false, false)
                .Build()
                .Get<TestConfiguration>();
            if (_testConfig == null)
                throw new TestException($"The test configuration could not be loaded. Ensure that the test project includes a '{testConfigFilename}' file, it is copied to the output directory and it is formatted correctly.");

            // Make sure Azurite is running
            // If Azurite is not running we want to fail the tests quickly, not wait for the tests to run and then fail
            if (_testConfig.Azurite.EnableAzuritePortCheck && !AzuriteHelper.IsRunning(_testConfig.Azurite))
                throw new TestException($"Azurite is not running on ports {_testConfig.Azurite.BlobServicePort} (Blob service), {_testConfig.Azurite.QueueServicePort} (Queue service) and {_testConfig.Azurite.TableServicePort} (Table service). Logic App workflows cannot run unless all three services are running in Azurite");

            // Set up the workflow
            _workflowDefinition = new WorkflowHelper(workflowName, ReadFromPath(Path.Combine(logicAppBasePath, workflowName, Constants.WORKFLOW)));
            Console.WriteLine($"Workflow '{_workflowDefinition.WorkflowName}' is {_workflowDefinition.WorkflowType}");
            _workflowDefinition.ReplaceRetryPoliciesWithNone();
            _workflowDefinition.ReplaceTriggersWithHttp();
            _workflowDefinition.ReplaceBuiltInConnectorActionsWithHttp(_testConfig.Workflow.BuiltInConnectorsToMock);

            // Set up the local settings
            // The name of the local setting file can be set in the test configuration
            _localSettings = new SettingsHelper(ReadFromPath(Path.Combine(logicAppBasePath, SetLocalSettingsFile(localSettingsFilename))));
            _localSettings.ReplaceExternalUrlsWithMockServer(_testConfig.Workflow.ExternalApiUrlsToMock);

            // Set up the connections
            _connections = new ConnectionHelper(ReadFromPath(Path.Combine(logicAppBasePath, Constants.CONNECTIONS), optional: true), _localSettings);
            _connections.ReplaceManagedApiConnectionUrlsWithMockServer();

            // Set up the artifacts (schemas, maps)
            SetArtifactDirectory(logicAppBasePath);

            // Other files needed to test the workflow, but we don't need to update these
            _parameters = ReadFromPath(Path.Combine(logicAppBasePath, Constants.PARAMETERS), optional: true);
            _host = ReadFromPath(Path.Combine(logicAppBasePath, Constants.HOST));

            // If this is a stateless workflow and the 'OperationOptions' is not 'WithStatelessRunHistory'...
            if (_workflowDefinition.WorkflowType == WorkflowType.Stateless && _localSettings.GetWorkflowOperationOptionsValue(_workflowDefinition.WorkflowName) != workflowOperationOptionsRunHistory)
            {
                if (_testConfig.Workflow.AutoConfigureWithStatelessRunHistory)
                {
                    string newSetting = _localSettings.SetWorkflowOperationOptionsValue(_workflowDefinition.WorkflowName, workflowOperationOptionsRunHistory);
                    Console.WriteLine($"Workflow is stateless, creating new setting: {newSetting}");
                }
                else
                {
                    throw new TestException($"The workflow is stateless and the 'Workflows.{_workflowDefinition.WorkflowName}.OperationOptions' setting is not configured for 'WithStatelessRunHistory'. This means that the workflow execution history will not be created and therefore the workflow cannot be tested. Set the 'workflow.autoConfigureWithStatelessRunHistory` option to 'true' in 'testConfiguration.json' so that the testing framework creates this setting automatically when running the test");
                }
            }

            _workflowIsInitialised = true;
        }

        /// <summary>
        /// Create a new instance of the test runner. This is used to run a test for a workflow.
        /// </summary>
        /// <returns>An instance of the test runner.</returns>
        /// <remarks>
        /// Do not use an instance of a test runner to run multiple workflows.
        /// </remarks>
        protected ITestRunner CreateTestRunner()
        {
            return CreateTestRunner(null);
        }

        /// <summary>
        /// Create a new instance of the test runner with overrides for specific local settings. This is used to run a test for a workflow.
        /// </summary>
        /// <param name="localSettingsOverrides">Dictionary containing the local settings to be overridden.</param>
        /// <returns>An instance of the test runner.</returns>
        /// <remarks>
        /// Do not use an instance of a test runner to run multiple workflows.
        /// </remarks>
        protected ITestRunner CreateTestRunner(Dictionary<string, string> localSettingsOverrides)
        {
            // Make sure that the workflow has been initialised
            if (!_workflowIsInitialised)
                throw new TestException("Cannot create the test runner because the workflow has not been initialised using Initialize()");

            // Update the local settings if anything needs to be overridden
            if (localSettingsOverrides != null && localSettingsOverrides.Count > 0)
            {
                _localSettings.ReplaceSettingOverrides(localSettingsOverrides);
            }

            return new TestRunner(
                _testConfig.Logging,
                _client,
                _workflowDefinition.WorkflowName, _workflowDefinition.ToString(),
                _localSettings.ToString(), _host, _parameters, _connections.ToString(), _artifactDirectory);
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
            else if (!string.IsNullOrEmpty(_testConfig.LocalSettingsFilename))
                localSettingsFile = _testConfig.LocalSettingsFilename;
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
                _artifactDirectory = directoryInfo;
                Console.WriteLine($"Using artifacts directory: {Path.Combine(directoryInfo.Parent.Name, directoryInfo.Name)}");
            }
            else
            {
                _artifactDirectory = null;
                Console.WriteLine("Artifacts directory does not exist.");
            }
        }

        /// <summary>
        /// Read the contents of a file using the given path.
        /// </summary>
        /// <param name="path">Path of the file to be read.</param>
        /// <param name="optional"><c>true</c> if the file is option, otherwise <c>false</c>.</param>
        /// <returns>The file content, as a <see cref="string"/>, or <c>null</c> if the file is optional and does not exist.</returns>
        private static string ReadFromPath(string path, bool optional = false)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var fullPath = Path.GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                if (optional)
                {
                    return null;
                }
                else
                {
                    throw new TestException($"File {fullPath} does not exist and it is a mandatory file for a Logic App");
                }
            }

            return File.ReadAllText(fullPath);
        }
    }
}
