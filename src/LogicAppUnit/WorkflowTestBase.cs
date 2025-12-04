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
            Initialize(logicAppBasePath, workflowName, null, null);
        }

        /// <summary>
        /// Initializes all the workflow specific variables which will be used throughout the test executions, with optional custom configuration files.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="workflowName">The name of the workflow. This matches the name of the folder that contains the workflow definition file.</param>
        /// <param name="localSettingsFilename">Optional custom local settings filename (e.g., 'local.settings-custom.json'). If specified, searches test project first. If not specified, uses testConfiguration.json setting or default 'local.settings.json' from Logic App project.</param>
        /// <param name="testProjectPath">Path to the test project containing test-specific configuration files. If not specified, defaults to current directory.</param>
        /// <param name="parametersFilename">Optional custom parameters filename (e.g., 'parameters-custom.json'). If specified, searches test project first. If not specified, uses default 'parameters.json' from Logic App project.</param>
        /// <param name="connectionsFilename">Optional custom connections filename (e.g., 'connections-custom.json'). If specified, searches test project first. If not specified, uses default 'connections.json' from Logic App project.</param>
        protected void Initialize(string logicAppBasePath, string workflowName, string localSettingsFilename = null, string testProjectPath = null, string parametersFilename = null, string connectionsFilename = null)
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

            // Track if custom filenames were provided before applying defaults
            // Note: testConfiguration.json is NOT considered custom - it just specifies which file in Logic App project to use
            var hasCustomLocalSettings = !string.IsNullOrEmpty(localSettingsFilename);
            var hasCustomParametersFile = !string.IsNullOrEmpty(parametersFilename);
            var hasCustomConnectionsFile = !string.IsNullOrEmpty(connectionsFilename);

            // Default test project path to current directory if not specified
            testProjectPath = testProjectPath ?? Directory.GetCurrentDirectory();

            // Set default filenames if not specified
            parametersFilename = parametersFilename ?? Constants.PARAMETERS;
            connectionsFilename = connectionsFilename ?? Constants.CONNECTIONS;

            // Process the workflow definition, local settings, parameters and connection files
            ProcessWorkflowDefinitionFile(logicAppBasePath, workflowName);
            ProcessLocalSettingsFile(logicAppBasePath, localSettingsFilename, testProjectPath, hasCustomLocalSettings);
            ProcessParametersFile(logicAppBasePath, testProjectPath, parametersFilename, hasCustomParametersFile);
            ProcessConnectionsFile(logicAppBasePath, testProjectPath, connectionsFilename, hasCustomConnectionsFile);

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

        #region Helper methods

        /// <summary>
        /// Resolves the configuration file path by checking the test project path first (if searching), then falling back to the Logic App path.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="testProjectPath">Path to the test project containing test-specific configuration files.</param>
        /// <param name="fileName">The name of the configuration file to locate.</param>
        /// <param name="searchTestProject">If true, search test project first; otherwise only search Logic App project.</param>
        /// <returns>A tuple containing the full path to the configuration file (or null if not found) and a flag indicating if it's from the test project.</returns>
        private (string filePath, bool isFromTestProject) ResolveConfigurationFilePath(string logicAppBasePath, string testProjectPath, string fileName, bool searchTestProject)
        {
            // Check test project first (if searchTestProject is true)
            if (searchTestProject)
            {
                var testProjectFilePath = Path.Combine(testProjectPath, fileName);
                if (File.Exists(testProjectFilePath))
                {
                    Console.WriteLine($"Using configuration file from test project: {testProjectFilePath}");
                    return (testProjectFilePath, true);
                }
            }

            // Fall back to Logic App project
            var logicAppFilePath = Path.Combine(logicAppBasePath, fileName);
            if (File.Exists(logicAppFilePath))
            {
                Console.WriteLine($"Using configuration file from Logic App project: {logicAppFilePath}");
                return (logicAppFilePath, false);
            }

            return (null, false);
        }

        /// <summary>
        /// Generic helper to process configuration files with optional custom naming and test project support.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="testProjectPath">Path to the test project containing test-specific configuration files.</param>
        /// <param name="fileName">The name of the configuration file to locate.</param>
        /// <param name="hasCustomFile">True if a custom filename was explicitly provided by the user.</param>
        /// <param name="standardFileName">The standard filename to copy to if file is from test project (e.g., 'local.settings.json').</param>
        /// <param name="optional">True if the file is optional.</param>
        /// <returns>The final file path to use, or null if optional and not found.</returns>
        private string ProcessConfigurationFile(string logicAppBasePath, string testProjectPath, string fileName, bool hasCustomFile, string standardFileName, bool optional = false)
        {
            // Resolve the file location
            var (filePath, isFromTestProject) = ResolveConfigurationFilePath(logicAppBasePath, testProjectPath, fileName, hasCustomFile);

            if (filePath == null)
            {
                if (optional)
                    return null;
                throw new TestException($"The configuration file '{fileName}' does not exist in test project '{testProjectPath}' or Logic App project '{logicAppBasePath}'");
            }

            // If file is from test project and has custom name, copy to standard location for Logic App runtime
            if (isFromTestProject && hasCustomFile && fileName != standardFileName)
            {
                var targetPath = Path.Combine(logicAppBasePath, standardFileName);
                File.Copy(filePath, targetPath, overwrite: true);
                Console.WriteLine($"Copied {Path.GetFileName(standardFileName).Replace(".json", "")} file '{fileName}' from test project to '{standardFileName}' for Logic App runtime");
                return targetPath;
            }

            return filePath;
        }

        #endregion Helper methods

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
            _workflowDefinition.ReplaceManagedIdentityAuthenticationTypeWithNone();
        }

        /// <summary>
        /// Process a workflow local settings file before the test is run.
        /// The filename is determined by: Initialize() parameter → testConfiguration.json → default (local.settings.json).
        /// If a custom filename was specified via Initialize() parameter, the test project is searched first, then the Logic App project.
        /// If the file is found in the test project, it will be copied to the standard location (from testConfiguration.json or default) for the Logic App runtime.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="localSettingsFilename">The name of the local settings file passed to Initialize(), or null.</param>
        /// <param name="testProjectPath">Path to the test project containing test-specific configuration files.</param>
        /// <param name="hasCustomFile">True if a custom filename was explicitly provided via Initialize() parameter.</param>
        private void ProcessLocalSettingsFile(string logicAppBasePath, string localSettingsFilename, string testProjectPath, bool hasCustomFile)
        {
            // Determine the filename: Initialize param → testConfig → default
            var fileName = SetLocalSettingsFile(localSettingsFilename);

            // Determine the standard filename (from testConfig or default) for copying custom files to
            var standardFileName = SetLocalSettingsFile(null);

            // Process the file using generic helper
            var filePath = ProcessConfigurationFile(logicAppBasePath, testProjectPath, fileName, hasCustomFile, standardFileName);

            _localSettings = new LocalSettingsWrapper(ReadFromPath(filePath));
            _localSettings.ReplaceExternalUrlsWithMockServer(_testConfig.Workflow.ExternalApiUrlsToMock);
        }

        /// <summary>
        /// Process a workflow parameters file before the test is run.
        /// Files in the test project path take precedence over files in the Logic App project.
        /// If a custom-named file is provided from the test project, it will be copied to the standard parameters.json location.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="testProjectPath">Path to the test project containing test-specific configuration files.</param>
        /// <param name="parametersFilename">The name of the parameters file to be used.</param>
        /// <param name="hasCustomFile">True if a custom filename was explicitly provided by the user.</param>
        private void ProcessParametersFile(string logicAppBasePath, string testProjectPath, string parametersFilename, bool hasCustomFile)
        {
            // Process the file using generic helper
            var filePath = ProcessConfigurationFile(logicAppBasePath, testProjectPath, parametersFilename, hasCustomFile, Constants.PARAMETERS, optional: true);

            _parameters = new ParametersWrapper(ReadFromPath(filePath, optional: true));
        }

        /// <summary>
        /// Process a workflow connections file before the test is run.
        /// Files in the test project path take precedence over files in the Logic App project.
        /// If a custom-named file is provided from the test project, it will be copied to the standard connections.json location.
        /// </summary>
        /// <param name="logicAppBasePath">Path to the root folder containing the workflows.</param>
        /// <param name="testProjectPath">Path to the test project containing test-specific configuration files.</param>
        /// <param name="connectionsFilename">The name of the connections file to be used.</param>
        /// <param name="hasCustomFile">True if a custom filename was explicitly provided by the user.</param>
        private void ProcessConnectionsFile(string logicAppBasePath, string testProjectPath, string connectionsFilename, bool hasCustomFile)
        {
            const string invalidConnectionsMsg = "configured to use the 'ManagedServiceIdentity' authentication type. Only the 'Raw' and 'ActiveDirectoryOAuth' authentication types are allowed in a local developer environment";

            // Process the file using generic helper
            var filePath = ProcessConfigurationFile(logicAppBasePath, testProjectPath, connectionsFilename, hasCustomFile, Constants.CONNECTIONS, optional: true);

            _connections = new ConnectionsWrapper(ReadFromPath(filePath, optional: true), _localSettings, _parameters);

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
