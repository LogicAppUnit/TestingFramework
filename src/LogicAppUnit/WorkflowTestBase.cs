using LogicAppUnit.Hosting;
using LogicAppUnit.InternalHelper;
using LogicAppUnit.Mocking;
using LogicAppUnit.Wrapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private DirectoryInfo _customLibraryDirectory;
        private readonly List<MockResponse> _mockResponses;

        private WorkflowDefinitionWrapper _workflowDefinition;
        private LocalSettingsWrapper _localSettings;
        private ParametersWrapper _parameters;
        private ConnectionsWrapper _connections;
        private CsxWrapper[] _csxTestInputs;

        private string _host;
        private bool _workflowIsInitialised; // default = false

        #region Lifetime management

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
        /// Initializes a new instance of the <see cref="WorkflowTestBase"/> class.
        /// </summary>
        public WorkflowTestBase()
        {
            _mockResponses = new List<MockResponse>();
        }

        #endregion // Lifetime management

        /// <summary>
        /// Gets the URI for the mock test workflow host.
        /// </summary>
        /// <remarks>
        /// Use this property when you want to create a request message that contains a callback URL that needs to be pointed at the mock test host.
        /// </remarks>
        protected static string MockTestWorkflowHostUri
        {
            get => TestEnvironment.FlowV2MockTestHostUri;
        }

        #region Mock request handling

        /// <summary>
        /// Add a mocked response that is used across all test cases, consisting of a request matcher and a corresponding response builder.
        /// </summary>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        /// <returns>The mocked response.</returns>
        public IMockResponse AddMockResponse(IMockRequestMatcher mockRequestMatcher)
        {
            return AddMockResponse(null, mockRequestMatcher);
        }

        /// <summary>
        /// Add a named mocked response that is used across all test cases, consisting of a request matcher and a corresponding response builder.
        /// </summary>
        /// <param name="name">Name of the mock.</param>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        /// <returns>The mocked response.</returns>
        public IMockResponse AddMockResponse(string name, IMockRequestMatcher mockRequestMatcher)
        {
            if (!string.IsNullOrEmpty(name) && _mockResponses.Where(x => x.MockName == name).Any())
                throw new ArgumentException($"A mock response with the name '{name}' already exists.");

            var mockResponse = new MockResponse(name, mockRequestMatcher);
            _mockResponses.Add(mockResponse);
            return mockResponse;
        }

        #endregion // Mock request handling

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
                .AddJsonFile(testConfigFilename, true, false)
                .Build()
                .Get<TestConfiguration>();

            if (_testConfig == null)
            {
                _testConfig = new TestConfiguration();
                Console.WriteLine($"A test configuration file '{testConfigFilename}' could not be found, or does not contain any settings. Using default test configuration settings.");
            }

            // Make sure Azurite is running
            // If Azurite is not running we want to fail the tests quickly, not wait for the tests to run and then fail
            if (_testConfig.Azurite.EnableAzuritePortCheck && !AzuriteHelper.IsRunning(_testConfig.Azurite))
                throw new TestException($"Azurite is not running on ports {_testConfig.Azurite.BlobServicePort} (Blob service), {_testConfig.Azurite.QueueServicePort} (Queue service) and {_testConfig.Azurite.TableServicePort} (Table service). Logic App workflows cannot run unless all three services are running in Azurite");

            // Process the workflow definition, local settings, parameters and connection files
            ProcessWorkflowDefinitionFile(logicAppBasePath, workflowName);
            ProcessLocalSettingsFile(logicAppBasePath, localSettingsFilename);
            ProcessParametersFile(logicAppBasePath);
            ProcessConnectionsFile(logicAppBasePath);

            // Set up the artifacts (schemas, maps) and custom library folders
            _artifactDirectory = SetSourceDirectory(logicAppBasePath, Constants.ARTIFACTS_FOLDER, "artifacts");
            _customLibraryDirectory = SetSourceDirectory(logicAppBasePath, Constants.CUSTOM_LIB_FOLDER, "custom library");

            // Other files needed to test the workflow, but we don't need to read or modify these
            _host = ReadFromPath(Path.Combine(logicAppBasePath, Constants.HOST));

            // Find all of the csx files that are used by the Logic App
            // These files can be located anywhere in the folder structure
            _csxTestInputs = new DirectoryInfo(logicAppBasePath)
                .GetFiles("*.csx", SearchOption.AllDirectories)
                .Select(x => new CsxWrapper(File.ReadAllText(x.FullName), Path.GetRelativePath(logicAppBasePath, x.DirectoryName), x.Name))
                .ToArray();

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
        /// Releases resources held by the test framework.
        /// </summary>
        protected static void Close()
        {
            _client?.Dispose();
        }

        #region Create test runner

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
                _testConfig.Runner,
                _client,
                _host,
                _mockResponses,
                _workflowDefinition,
                _localSettings,
                _parameters,
                _connections,
                _csxTestInputs,
                _artifactDirectory,
                _customLibraryDirectory);
        }

        #endregion Create test runner

        #region Source file processing

        /// <summary>
        /// Process a workflow definition file before the test is run.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="workflowName">The name of the workflow. This matches the name of the folder that contains the workflow definition file.</param>
        private void ProcessWorkflowDefinitionFile(string logicAppBasePath, string workflowName)
        {
            _workflowDefinition = new WorkflowDefinitionWrapper(workflowName, ReadFromPath(Path.Combine(logicAppBasePath, workflowName, Constants.WORKFLOW)));
            Console.WriteLine($"Workflow '{_workflowDefinition.WorkflowName}' is {_workflowDefinition.WorkflowType}");

            _workflowDefinition.ReplaceTriggersWithHttp();

            if (_testConfig.Workflow.RemoveHttpRetryConfiguration)
                _workflowDefinition.ReplaceHttpRetryPoliciesWithNone();

            if (_testConfig.Workflow.RemoveHttpChunkingConfiguration)
                _workflowDefinition.RemoveHttpChunkingConfiguration();

            if (_testConfig.Workflow.RemoveManagedApiConnectionRetryConfiguration)
                _workflowDefinition.ReplaceManagedApiConnectionRetryPoliciesWithNone();

            _workflowDefinition.ReplaceInvokeWorkflowActionsWithHttp();
            _workflowDefinition.ReplaceCallLocalFunctionActionsWithHttp();
            _workflowDefinition.ReplaceBuiltInConnectorActionsWithHttp(_testConfig.Workflow.BuiltInConnectorsToMock);
        }

        /// <summary>
        /// Process a workflow local settings file before the test is run.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="localSettingsFilename">The name of the local settings file to be used, this can be used to override the default of <i>local.settings.json</i>.</param>
        private void ProcessLocalSettingsFile(string logicAppBasePath, string localSettingsFilename)
        {
            // The name of the local setting file can be set in the test configuration
            _localSettings = new LocalSettingsWrapper(ReadFromPath(Path.Combine(logicAppBasePath, SetLocalSettingsFile(localSettingsFilename))));

            _localSettings.ReplaceExternalUrlsWithMockServer(_testConfig.Workflow.ExternalApiUrlsToMock);
        }

        /// <summary>
        /// Process a workflow parameters file before the test is run.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        private void ProcessParametersFile(string logicAppBasePath)
        {
            _parameters = new ParametersWrapper(ReadFromPath(Path.Combine(logicAppBasePath, Constants.PARAMETERS), optional: true));
        }

        /// <summary>
        /// Process a workflow connections file before the test is run.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        private void ProcessConnectionsFile(string logicAppBasePath)
        {
            const string invalidConnectionsMsg = "configured to use the 'ManagedServiceIdentity' authentication type. Only the 'Raw' and 'ActiveDirectoryOAuth' authentication types are allowed in a local developer environment";

            _connections = new ConnectionsWrapper(ReadFromPath(Path.Combine(logicAppBasePath, Constants.CONNECTIONS), optional: true), _localSettings, _parameters);

            _connections.ReplaceManagedApiConnectionUrlsWithMockServer(_testConfig.Workflow.ManagedApisToMock);

            // The Functions runtime will not start if there are any Managed API connections using the 'ManagedServiceIdentity' authentication type
            // Check for this so that the test will fail early with a meaningful error message
            var invalidConnections = _connections.ListManagedApiConnectionsUsingManagedServiceIdentity();
            if (invalidConnections.Count() == 1)
                throw new TestException($"There is 1 managed API connection ({invalidConnections.First()}) that is {invalidConnectionsMsg}");
            else if (invalidConnections.Any())
                throw new TestException($"There are {invalidConnections.Count()} managed API connections ({string.Join(", ", invalidConnections)}) that are {invalidConnectionsMsg}");
        }

        #endregion // Source file processing

        #region Private methods

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
        /// Check if a source folder for the Logic App exists.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder for the Logic App.</param>
        /// <param name="sourcePath">Relative path to the source folder to be checked, from the root folder of the Logic App.</param>
        /// <param name="sourceName">The name of the source folder to be checked, used for logging only.</param>
        /// <returns>A <see cref="DirectoryInfo"/> if the source folder exists, or <c>null</c> if it does not exist.</returns>
        private static DirectoryInfo SetSourceDirectory(string logicAppBasePath, string sourcePath, string sourceName)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(logicAppBasePath, sourcePath));
            if (directoryInfo.Exists)
            {
                Console.WriteLine($"Using {sourceName} directory: {Path.Combine(directoryInfo.Parent.Name, directoryInfo.Name)}");
                return directoryInfo;
            }
            else
            {
                Console.WriteLine($"The {sourceName} directory does not exist: {Path.Combine(directoryInfo.Parent.Name, directoryInfo.Name)}");
                return null;
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
                    return null;
                else
                    throw new TestException($"File {fullPath} does not exist and it is a mandatory file for a Logic App");
            }

            return File.ReadAllText(fullPath);
        }

        #endregion // Private methods
    }
}
