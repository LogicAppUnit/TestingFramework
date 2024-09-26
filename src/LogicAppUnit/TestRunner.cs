using LogicAppUnit.Hosting;
using LogicAppUnit.InternalHelper;
using LogicAppUnit.Mocking;
using LogicAppUnit.Wrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace LogicAppUnit
{
    /// <summary>
    /// Runs a workflow in an isolated test environment and makes available the run execution history.
    /// </summary>
    public class TestRunner : ITestRunner, IDisposable
    {
        private readonly HttpClient _client;

        private readonly WorkflowDefinitionWrapper _workflowDefinition;
        private readonly WorkflowTestHost _workflowTestHost;
        private readonly WorkflowApiHelper _apiHelper;

        private readonly MockDefinition _mockDefinition;
        private readonly MockHttpHost _mockHttpHost;

        private readonly TestConfigurationRunner _runnerConfig;

        private string _runId;
        private string _clientTrackingId;

        // Whether to wait for the asynchronous response by polling the redirected URI
        private bool _waitForAsyncResponse;

        // Maximum time to wait for the asynchronous response before timing out 
        private TimeSpan _asyncResponseTimeout;

        #region Mock request handling

        /// <inheritdoc cref="ITestRunner.AddApiMocks" />
        public Func<HttpRequestMessage, HttpResponseMessage> AddApiMocks
        {
            set => _mockDefinition.MockResponseDelegate = value;
        }

        /// <inheritdoc cref="ITestRunner.MockRequests" />
        public List<MockRequest> MockRequests
        {
            get => _mockDefinition.MockRequests;
        }

        /// <inheritdoc cref="ITestRunner.AddMockResponse(IMockRequestMatcher)" />
        public IMockResponse AddMockResponse(IMockRequestMatcher mockRequestMatcher)
        {
            return _mockDefinition.AddMockResponse(mockRequestMatcher);
        }

        /// <inheritdoc cref="ITestRunner.AddMockResponse(string, IMockRequestMatcher)" />
        public IMockResponse AddMockResponse(string name, IMockRequestMatcher mockRequestMatcher)
        {
            return _mockDefinition.AddMockResponse(name, mockRequestMatcher);
        }

        #endregion // Mock request handling

        #region Workflow properties

        /// <inheritdoc cref="ITestRunner.WorkflowRunId" />
        public string WorkflowRunId
        {
            get
            {
                if (string.IsNullOrEmpty(_runId))
                {
                    _runId = _apiHelper.WorkflowRunContent()["name"].ToString();
                }
                return _runId;
            }
        }

        /// <inheritdoc cref="ITestRunner.WorkflowClientTrackingId" />
        public string WorkflowClientTrackingId
        {
            get
            {
                if (string.IsNullOrEmpty(_clientTrackingId))
                {
                    _clientTrackingId = _apiHelper.WorkflowRunContent()["properties"]["correlation"]["clientTrackingId"].ToString();
                }
                return _clientTrackingId;
            }
        }

        /// <inheritdoc cref="ITestRunner.WorkflowRunStatus" />
        public WorkflowRunStatus WorkflowRunStatus
        {
            get
            {
                return (WorkflowRunStatus)Enum.Parse(typeof(WorkflowRunStatus), _apiHelper.WorkflowRunContent()["properties"]["status"].ToString());
            }
        }

        /// <inheritdoc cref="ITestRunner.WorkflowWasTerminated" />
        public bool WorkflowWasTerminated
        {
            get
            {
                return "Terminated" == _apiHelper.WorkflowRunContent()["properties"]["code"]?.ToString();
            }
        }

        /// <inheritdoc cref="ITestRunner.WorkflowTerminationCode" />
        public int? WorkflowTerminationCode
        {
            get
            {
                return _apiHelper.WorkflowRunContent()["properties"]["error"]?["code"]?.ToObject<int>();
            }
        }

        /// <inheritdoc cref="ITestRunner.WorkflowTerminationMessage" />
        public string WorkflowTerminationMessage
        {
            get
            {
                return _apiHelper.WorkflowRunContent()["properties"]["error"]?["message"]?.ToString();
            }
        }

        #endregion // Workflow properties

        #region Lifetime management

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunner"/> class.
        /// </summary>
        /// <param name="loggingConfig">The logging configuration for the test execution.</param>
        /// <param name="runnerConfig">The test runner configuration for the test execution.</param>
        /// <param name="client">The HTTP client.</param>
        /// <param name="mockResponsesFromBase">Mock responses that have been configured in the test base class.</param>
        /// <param name="workflowDefinition">The workflow definition file.</param>
        /// <param name="localSettings">The local settings file.</param>
        /// <param name="host">The contents of the host file.</param>
        /// <param name="parameters">The contents of the parameters file, or <c>null</c> if the file does not exist.</param>
        /// <param name="connections">The connections file, or <c>null</c> if the file does not exist.</param>
        /// <param name="csxTestInputs">A collection of C# script files or <c>null</c> if there are none.</param>
        /// <param name="artifactsDirectory">The (optional) artifacts directory containing maps and schemas that are used by the workflow being tested.</param>
        /// <param name="customLibraryDirectory">The (optional) custom library (lib/custom) directory containing custom components that are used by the workflow being tested.</param>
        internal TestRunner(
            TestConfigurationLogging loggingConfig,
            TestConfigurationRunner runnerConfig,
            HttpClient client,
            List<MockResponse> mockResponsesFromBase,
            WorkflowDefinitionWrapper workflowDefinition,
            LocalSettingsWrapper localSettings,
            string host,
            string parameters = null,
            ConnectionsWrapper connections = null,
            CsxTestInput[] csxTestInputs = null,
            DirectoryInfo artifactsDirectory = null,
            DirectoryInfo customLibraryDirectory = null)
        {
            if (loggingConfig == null)
                throw new ArgumentNullException(nameof(loggingConfig));
            if (runnerConfig == null)
                throw new ArgumentNullException(nameof(runnerConfig));
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (mockResponsesFromBase == null)
                throw new ArgumentNullException(nameof(mockResponsesFromBase));
            if (workflowDefinition == null)
                throw new ArgumentNullException(nameof(workflowDefinition));
            if (localSettings == null)
                throw new ArgumentNullException(nameof(localSettings));

            LoggingHelper.LogBanner("Starting test runner");
            //Console.WriteLine($"Max workflow duration: {runnerConfig.MaxWorkflowExecutionDuration} seconds");

            if (!loggingConfig.WriteFunctionRuntimeStartupLogs)
                Console.WriteLine("Logging of the Function runtime startup logs is disabled. This can be enabled using the 'logging.writeFunctionRuntimeStartupLogs' option in 'testConfiguration.json'.");

            _client = client;
            _workflowDefinition = workflowDefinition;
            _runnerConfig = runnerConfig;

            var workflowTestInput = new WorkflowTestInput[] { new WorkflowTestInput(workflowDefinition.WorkflowName, workflowDefinition.ToString()) };
            _workflowTestHost = new WorkflowTestHost(workflowTestInput, localSettings.ToString(), parameters, connections.ToString(), host,
                                                        csxTestInputs, artifactsDirectory, customLibraryDirectory, loggingConfig.WriteFunctionRuntimeStartupLogs);
            _apiHelper = new WorkflowApiHelper(client, workflowDefinition.WorkflowName);

            // Create the mock definition and mock HTTP host
            _mockDefinition = new MockDefinition(loggingConfig.WriteMockRequestMatchingLogs, runnerConfig.DefaultHttpResponseStatusCode, mockResponsesFromBase);
            _mockHttpHost = new MockHttpHost(_mockDefinition);
        }

        /// <summary>
        /// Disposes the resources.
        /// </summary>
        public void Dispose()
        {
            this._mockHttpHost.Dispose();
            this._workflowTestHost.Dispose();
        }

        #endregion // Lifetime management

        #region Public Action methods

        /// <inheritdoc cref="ITestRunner.GetWorkflowAction(string)" />
        public JToken GetWorkflowAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentNullException(nameof(actionName));

            JToken getActionFromRunHistory = _apiHelper.ActionsContent(WorkflowRunId).Where(actionResult => actionResult["name"].ToString().Equals(actionName)).FirstOrDefault();

            if (getActionFromRunHistory == null)
                throw new TestException($"Action '{actionName}' was not found in the workflow run history.");

            return getActionFromRunHistory["properties"];
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionStatus(string)" />
        public ActionStatus GetWorkflowActionStatus(string actionName)
        {
            JToken actionRunProperties = GetWorkflowAction(actionName);
            return (ActionStatus)Enum.Parse(typeof(ActionStatus), actionRunProperties["status"].ToString());
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionInput(string)" />
        public JToken GetWorkflowActionInput(string actionName)
        {
            return GetWorkflowActionMessage(actionName, "input");
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionOutput(string)" />
        public JToken GetWorkflowActionOutput(string actionName)
        {
            return GetWorkflowActionMessage(actionName, "output");
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionRepetitionCount(string)" />
        public int GetWorkflowActionRepetitionCount(string actionName)
        {
            JToken actionRunProperties = GetWorkflowAction(actionName);

            // Actions inside a loop will have a 'repetitionCount' property
            if (actionRunProperties["repetitionCount"] != null)
                return actionRunProperties["repetitionCount"].Value<int>();

            // An Until loop will have an 'iterationCount' property
            if (actionRunProperties["iterationCount"] != null)
                return actionRunProperties["iterationCount"].Value<int>();

            // A ForEach loop will have an 'inputsLink.metadata.foreachItemsCount' property
            if (actionRunProperties["inputsLink"]?["metadata"]?["foreachItemsCount"] != null)
                return actionRunProperties["inputsLink"]["metadata"]["foreachItemsCount"].Value<int>();

            // Else the action has not been repeated
            return 1;
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionTrackedProperties(string)" />
        public Dictionary<string, string> GetWorkflowActionTrackedProperties(string actionName)
        {
            JToken actionRunProperties = GetWorkflowAction(actionName);
            return actionRunProperties["trackedProperties"]?.ToDictionary(a => ((JProperty)a).Name, b => ((JProperty)b).Value.Value<string>());
        }

        #endregion // Public Action methods

        #region Public Action Repetition methods

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionRepetition(string, int)" />
        public JToken GetWorkflowActionRepetition(string actionName, int repetitionNumber)
        {
            if (repetitionNumber <= 0)
                throw new ArgumentException("The repetition number must be greater than 1");

            JToken actionRunProperties = GetWorkflowAction(actionName);

            if (!(actionRunProperties as JObject).ContainsKey("repetitionCount"))
                throw new TestException($"Action '{actionName}' was not part of a repetition running inside a loop.");

            int repetitionCount = actionRunProperties["repetitionCount"].Value<int>();

            if (repetitionNumber > repetitionCount)
                throw new TestException($"The action '{actionName}' has run inside a loop and the number of repetitions is {repetitionCount}. Therefore testing a repetition number of {repetitionNumber} is not valid.");

            IEnumerable<JToken> value = _apiHelper.ActionRepetitonsContent(WorkflowRunId, actionName);

            if (value.Count() != repetitionCount)
                throw new TestException($"Repetitions for action '{actionName}' did not run properly, could not find {repetitionCount} repetitions in the workflow run history.");

            JToken repetition = value.Where(rep => rep["properties"]["repetitionIndexes"][0]["itemIndex"].Value<int>() == repetitionNumber - 1).First();

            return repetition["properties"];
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionStatus(string, int)" />
        public ActionStatus GetWorkflowActionStatus(string actionName, int repetitionNumber)
        {
            JToken actionRunRepetitionProperties = GetWorkflowActionRepetition(actionName, repetitionNumber);
            return (ActionStatus)Enum.Parse(typeof(ActionStatus), actionRunRepetitionProperties["status"].ToString());
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionInput(string, int)" />
        public JToken GetWorkflowActionInput(string actionName, int repetitionNumber)
        {
            return GetWorkflowActionRepetitionMessage(actionName, repetitionNumber, "input");
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionOutput(string, int)" />
        public JToken GetWorkflowActionOutput(string actionName, int repetitionNumber)
        {
            return GetWorkflowActionRepetitionMessage(actionName, repetitionNumber, "output");
        }

        /// <inheritdoc cref="ITestRunner.GetWorkflowActionTrackedProperties(string, int)" />
        public Dictionary<string, string> GetWorkflowActionTrackedProperties(string actionName, int repetitionNumber)
        {
            JToken actionRunRepetitionProperties = GetWorkflowActionRepetition(actionName, repetitionNumber);
            return actionRunRepetitionProperties["trackedProperties"]?.ToDictionary(a => ((JProperty)a).Name, b => ((JProperty)b).Value.Value<string>());
        }

        #endregion // Public Action Repetition methods

        #region TriggerWorkflow

        /// <inheritdoc cref="ITestRunner.TriggerWorkflow(HttpMethod, Dictionary{string, string})" />
        public HttpResponseMessage TriggerWorkflow(HttpMethod method, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(null, null, method, string.Empty, requestHeaders);
        }

        /// <inheritdoc cref="ITestRunner.TriggerWorkflow(Dictionary{string, string}, HttpMethod, Dictionary{string, string})" />
        public HttpResponseMessage TriggerWorkflow(Dictionary<string, string> queryParams, HttpMethod method, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(queryParams, null, method, string.Empty, requestHeaders);
        }

        /// <inheritdoc cref="ITestRunner.TriggerWorkflow(Dictionary{string, string}, HttpMethod, string, Dictionary{string, string})" />
        public HttpResponseMessage TriggerWorkflow(Dictionary<string, string> queryParams, HttpMethod method, string relativePath, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(queryParams, null, method, relativePath, requestHeaders);
        }

        /// <inheritdoc cref="ITestRunner.TriggerWorkflow(HttpContent, HttpMethod, Dictionary{string, string})" />
        public HttpResponseMessage TriggerWorkflow(HttpContent content, HttpMethod method, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(null, content, method, string.Empty, requestHeaders);
        }

        /// <inheritdoc cref="ITestRunner.TriggerWorkflow(HttpContent, HttpMethod, string, Dictionary{string, string})" />
        public HttpResponseMessage TriggerWorkflow(HttpContent content, HttpMethod method, string relativePath, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(null, content, method, relativePath, requestHeaders);
        }

        /// <inheritdoc cref="ITestRunner.TriggerWorkflow(Dictionary{string, string}, HttpContent, HttpMethod, string, Dictionary{string, string})" />
        public HttpResponseMessage TriggerWorkflow(Dictionary<string, string> queryParams, HttpContent content, HttpMethod method, string relativePath, Dictionary<string, string> requestHeaders = null)
        {
            string triggerName = _workflowDefinition.HttpTriggerName;
            if (string.IsNullOrEmpty(triggerName))
                throw new TestException($"Workflow does not have a HTTP Request trigger, so the workflow cannot be started.");

            // On Mac and Linux platforms, without a short delay the call to the Functions Management host to get the callback URL will fail with
            // a "System.Net.Http.HttpRequestException: Connection refused" exception
            if (!OperatingSystem.IsWindows())
                System.Threading.Thread.Sleep(500);

            // Get the callback information for the workflow, including the trigger URL
            CallbackUrlDefinition callbackDef = _apiHelper.GetWorkflowCallbackDefinition(triggerName);

            // Configure the HttpRequestMessage to trigger the workflow
            var httpRequestMessage = new HttpRequestMessage
            {
                Content = content,
                Method = method,
                RequestUri = callbackDef.ValueWithRelativePathAndQueryParams(relativePath, queryParams)
            };

            if (requestHeaders != null)
            {
                foreach (var item in requestHeaders)
                {
                    httpRequestMessage.Headers.Add(item.Key, item.Value);
                }
            }

            _mockDefinition.TestRunStarting();
            LoggingHelper.LogBanner("Starting workflow execution");
            Console.WriteLine("Workflow trigger:");
            Console.WriteLine($"    Name: {triggerName}");
            Console.WriteLine($"    URL: {httpRequestMessage.Method} {httpRequestMessage.RequestUri}");
            Console.WriteLine();

            // Run the workflow and wait for completion
            HttpResponseMessage response = PollAndReturnFinalWorkflowResponse(httpRequestMessage);

            LoggingHelper.LogBanner("Completed workflow execution");
            _mockDefinition.TestRunComplete();

            LoggingHelper.LogBanner("Test assertions");

            return response;
        }

        #endregion // TriggerWorkflow

        #region WaitForAsynchronousResponse

        /// <inheritdoc cref="ITestRunner.WaitForAsynchronousResponse(int)" />
        public void WaitForAsynchronousResponse(int maxTimeoutSeconds)
        {
            WaitForAsynchronousResponse(TimeSpan.FromSeconds(maxTimeoutSeconds));
        }

        /// <inheritdoc cref="ITestRunner.WaitForAsynchronousResponse(TimeSpan)" />
        public void WaitForAsynchronousResponse(TimeSpan maxTimeout)
        {
            _waitForAsyncResponse = true;
            _asyncResponseTimeout = maxTimeout;
        }

        #endregion // WaitForAsynchronousResponse

        /// <inheritdoc cref="ITestRunner.ExceptionWrapper(Action)" />
        public void ExceptionWrapper(Action assertion)
        {
            try
            {
                assertion();
            }
            catch (AssertFailedException afex)
            {
                // Include a list of the failed workflow actions to help with the investigations
                List<JToken> failedActions = _apiHelper.ActionsContent(WorkflowRunId).Where(actionResult =>
                    actionResult["properties"]["status"].ToString().Equals("Failed") || actionResult["properties"]["status"].ToString().Equals("Running")).ToList();

                if (failedActions.Count > 0)
                {
                    throw new AssertFailedException($"{afex.Message}\r\n\r\nFailed actions:\r\n{string.Join(",\r\n", failedActions.Select(x => x.ToString()))}");
                }
                else throw;
            }
        }

        #region Private methods

        /// <summary>
        /// Gets the input or output for an action.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="messageType">Either 'input' or 'output'.</param>
        /// <returns>The input or output.</returns>
        private JToken GetWorkflowActionMessage(string actionName, string messageType)
        {
            JToken actionRunProperties = GetWorkflowAction(actionName);
            string uri = actionRunProperties[$"{messageType}sLink"]?["uri"]?.Value<string>();

            if (string.IsNullOrEmpty(uri))
            {
                // Give a better error message to the test author if this action was skipped
                if (actionRunProperties["status"].ToString() == ActionStatus.Skipped.ToString())
                    throw new TestException($"Action '{actionName}' does not have any {messageType} because the action was skipped.");
                else
                    throw new TestException($"Action '{actionName}' does not have any {messageType}.");
            }

            return _apiHelper.GetActionMessage(uri);
        }

        /// <summary>
        /// Gets the input or output for an action for a repetition.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        /// <param name="messageType">Either 'input' or 'output'.</param>
        /// <returns>The input or output.</returns>
        private JToken GetWorkflowActionRepetitionMessage(string actionName, int repetitionNumber, string messageType)
        {
            JToken actionRunRepetitionProperties = GetWorkflowActionRepetition(actionName, repetitionNumber);
            string uri = actionRunRepetitionProperties[$"{messageType}sLink"]?["uri"]?.Value<string>();

            if (string.IsNullOrEmpty(uri))
            {
                // Give a better error message to the test author if this action was skipped
                if (actionRunRepetitionProperties["status"].ToString() == ActionStatus.Skipped.ToString())
                    throw new TestException($"Action '{actionName}' and repetiton {repetitionNumber} does not have any {messageType} because the action was skipped.");
                else
                    throw new TestException($"Action '{actionName}' and repetiton {repetitionNumber} does not have any {messageType}.");
            }

            return _apiHelper.GetActionMessage(uri);
        }

        /// <summary>
        /// Poll the workflow run result.
        /// </summary>
        /// <param name="httpRequestMessage">The request message to trigger the workflow.</param>
        /// <returns>The response from the workflow.</returns>
        private HttpResponseMessage PollAndReturnFinalWorkflowResponse(HttpRequestMessage httpRequestMessage)
        {
            HttpResponseMessage initialWorkflowResponse;
            HttpResponseMessage asyncWorkflowResponse = null;

            // Call the endpoint for the HTTP trigger
            // If a workflow doesn't include a Response action, the endpoint responds immediately with a HTTP 202 (Accepted) status
            // If a workflow includes an asynchronous Response action, the endpoint responds immediately with a HTTP 202 (Accepted) status and a callback URL to get the asynchronous response
            try
            {
                initialWorkflowResponse = _client.SendAsync(httpRequestMessage).Result;
            }
            catch (AggregateException aex) when (aex.InnerException is System.Threading.Tasks.TaskCanceledException)
            {
                // This occurs when the trigger API does not respond
                throw new TestException($"The API call to the workflow trigger did not respond within the required time. URL: {httpRequestMessage.RequestUri.AbsoluteUri}.");
            }

            // Store some of the run metadata for test assertions, this may not exist for stateless workflows or for workflows with asynchronous responses 
            _runId = GetHeader(initialWorkflowResponse.Headers, "x-ms-workflow-run-id");
            _clientTrackingId = GetHeader(initialWorkflowResponse.Headers, "x-ms-client-tracking-id");

            //if (string.IsNullOrEmpty(_runId))
            //    throw new TestException("The Run Id was not provided in the response when triggering the workflow.");

            // Check for and handle asynchronous response
            var callbackLocation = initialWorkflowResponse.Headers?.Location;
            if (initialWorkflowResponse.StatusCode == HttpStatusCode.Accepted && callbackLocation != null && _waitForAsyncResponse)
            {
                Console.WriteLine();
                Console.WriteLine("Workflow async callback:");
                Console.WriteLine($"    URL: GET {callbackLocation}");
                Console.WriteLine($"    Timeout for receipt of async response: {_asyncResponseTimeout.TotalSeconds} seconds");
                Console.WriteLine();

                var retryAfterSeconds = initialWorkflowResponse.Headers?.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);

                var stopwatchAsyncResponse = new Stopwatch();
                stopwatchAsyncResponse.Start();

                while (stopwatchAsyncResponse.Elapsed < _asyncResponseTimeout)
                {
                    HttpResponseMessage latestAsyncResponse = _client.GetAsync(callbackLocation).Result;

                    // If the async response has not been sent, we'll continue to get a HTTP 202 (Accepted) status code
                    if (latestAsyncResponse.StatusCode != HttpStatusCode.Accepted)
                    {
                        stopwatchAsyncResponse.Stop();
                        asyncWorkflowResponse = latestAsyncResponse;
                        break;
                    }
                    Thread.Sleep(retryAfterSeconds);
                }

                if (asyncWorkflowResponse == null)
                {
                    throw new TestException($"Workflow is taking more than {_asyncResponseTimeout.TotalSeconds} seconds to return the final async response.");
                }
            }

            // Wait till the workflow ends, i.e. workflow status is not "Running".
            // This should be checked in case of HTTP trigger workflows to make sure that any actions after the Response action have completed before testing their outputs.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(_runnerConfig.MaxWorkflowExecutionDuration))
            {
                using (var latestWorkflowHttpResponse = _client.GetAsync(TestEnvironment.GetListWorkflowRunsRequestUri(_workflowDefinition.WorkflowName)).Result)
                //using (var latestWorkflowHttpResponse = _client.GetAsync(TestEnvironment.GetGetWorkflowRunRequestUri(_workflowDefinition.WorkflowName, _runId)).Result)
                {
                    var latestWorkflowHttpResponseContent = latestWorkflowHttpResponse.Content.ReadAsAsync<JToken>().Result;
                    var runStatusOfWorkflow = latestWorkflowHttpResponseContent["value"]?[0]?["properties"]?["status"]?.ToString();
                    //var runStatusOfWorkflow = latestWorkflowHttpResponseContent["properties"]?["status"]?.ToString();

                    if (string.IsNullOrEmpty(runStatusOfWorkflow))
                    {
                        // There should always be a status in the response
                        Console.WriteLine("WARNING: The workflow status (properties.status) was not included in the response from the Logic App Management API");
                        Console.WriteLine($"    Response: {latestWorkflowHttpResponseContent.ToString()}");
                    }

                    // If we got status code other than Accepted then return the appropriate response
                    if (latestWorkflowHttpResponse.StatusCode != HttpStatusCode.Accepted && runStatusOfWorkflow != ActionStatus.Running.ToString())
                    {
                        // If there is an asynchronous response, return it
                        if (asyncWorkflowResponse != null)
                            return asyncWorkflowResponse;

                        // Otherwise return the initial response from the HTTP trigger
                        // For a workflow with a HTTP trigger that replaces a non-HTTP trigger, the response will have a HTTP 202 (Accepted) status
                        return initialWorkflowResponse;
                    }

                    Thread.Sleep(1000);
                }
            }

            throw new TestException($"Workflow is taking more than {_runnerConfig.MaxWorkflowExecutionDuration} seconds to complete its execution.");
        }

        #endregion // Private methods

        #region Private static methods

        /// <summary>
        /// Get a header value from a HTTP headers collection.
        /// </summary>
        /// <param name="headerCollection">The collection of headers.</param>
        /// <param name="headerName">The name of the header to be retrieved.</param>
        /// <returns>The header value.</returns>
        private static string GetHeader(HttpHeaders headerCollection, string headerName)
        {
            if (headerCollection == null)
                return null;

            var header = headerCollection.FirstOrDefault(h => h.Key == headerName);

            if (string.IsNullOrEmpty(header.Key))
                return null;

            return header.Value.FirstOrDefault();
        }

        #endregion // Private static methods
    }
}
