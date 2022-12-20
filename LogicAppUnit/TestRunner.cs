﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using LogicAppUnit.Helper;
using LogicAppUnit.Hosting;
using LogicAppUnit.InternalHelper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;

namespace LogicAppUnit
{
    /// <summary>
    /// Runs a workflow in an isolated test environment and makes available the run execution history.
    /// </summary>
    public class TestRunner : IDisposable
    {
        private readonly HttpClient _client;

        private readonly string _workflowName;
        private readonly MockHttpHost _mockHttpHost;
        private readonly WorkflowTestHost _workflowTestHost;
        private readonly WorkflowApiHelper _apiHelper;

        private string _runId;
        private string _clientTrackingId;

        // Requests sent to the mock test server that are generated by the workflow during its execution.
        // Use a ConcurrentBag to store the requests during the test execution to ensure thread safety of this collection
        private ConcurrentBag<MockRequest> _mockRequests;
        private List<MockRequest> _mockRequestsAsList;

        #region Properties

        /// <summary>
        /// Gets the workflow run id.
        /// </summary>
        /// <returns>The workflow run id.</returns>
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

        /// <summary>
        /// Gets the workflow client tracking id.
        /// </summary>
        /// <returns>The workflow client tracking id.</returns>
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

        /// <summary>
        /// Gets the workflow run status indicating whether the workflow ran successfully or not.
        /// </summary>
        /// <returns>The workflow run status.</returns>
        public WorkflowRunStatus WorkflowRunStatus
        {
            get
            {
                return (WorkflowRunStatus)Enum.Parse(typeof(WorkflowRunStatus), _apiHelper.WorkflowRunContent()["properties"]["status"].ToString());
            }
        }

        /// <summary>
        /// Sets the mocked responses for the outgoing HTTP calls from the workflow to the mock HTTP server.
        /// </summary>
        public Func<HttpRequestMessage, HttpResponseMessage> AddApiMocks
        {
            set
            {
                _mockHttpHost.RequestHandler = (request) => WrapApiMockDefinedInTestCase(request, value);
            }
        }

        /// <summary>
        /// Gets the mock requests that were created by the workflow during the test execution.
        /// </summary>
        /// <remarks>
        /// The requests are ordered in chronological order, with the most recent request at the start of the list.
        /// </remarks>
        public List<MockRequest> MockRequests
        {
            get
            {
                return _mockRequestsAsList;
            }
        }

        #endregion // Properties

        #region Lifetime management

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunner"/> class.
        /// </summary>
        /// <param name="loggingConfig">The logging configuration for the test execution.</param>
        /// <param name="client">The HTTP client.</param>
        /// <param name="workflowName">The name of the workflow being tested.</param>
        /// <param name="workflowDefinition">The content of the workflow definition file.</param>
        /// <param name="localSettings">The contents of the local settings file.</param>
        /// <param name="host">The contents of the host file.</param>
        /// <param name="parameters">The contents of the parameters file, or <c>null</c> if the file does not exist.</param>
        /// <param name="connections">The contents of the connections file, or <c>null</c> if the file does not exist.</param>
        /// <param name="artifactsDirectory">The (optional) artifacts directory containing maps and schemas that are used by the workflow being tested.</param>
        public TestRunner(
            TestConfigurationLogging loggingConfig,
            HttpClient client,
            string workflowName, string workflowDefinition,
            string localSettings, string host, string parameters = null, string connections = null, DirectoryInfo artifactsDirectory = null)
        {
            if (loggingConfig == null)
                throw new ArgumentNullException(nameof(loggingConfig));
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(workflowName))
                throw new ArgumentNullException(nameof(workflowName));
            if (string.IsNullOrEmpty(workflowDefinition))
                throw new ArgumentNullException(nameof(workflowDefinition));

            LoggingHelper.LogBanner("Starting test runner");

            if (!loggingConfig.WriteFunctionRuntineStartupLogs)
                Console.WriteLine("Logging of the Function runtime startup logs is disabled. This can be enabled using the 'logging.writeFunctionRuntineStartupLogs' option in 'testConfiguration.json'.");

            _client = client;
            _workflowName = workflowName;

            var workflowTestInput = new WorkflowTestInput[] { new WorkflowTestInput(workflowName, workflowDefinition) };
            _workflowTestHost = new WorkflowTestHost(workflowTestInput, localSettings, parameters, connections, host, artifactsDirectory, loggingConfig.WriteFunctionRuntineStartupLogs);
            _mockHttpHost = new MockHttpHost();
            _apiHelper = new WorkflowApiHelper(client, workflowName);

            // Initialise the cached mocked requests
            _mockRequests = new ConcurrentBag<MockRequest>();

            // Configure the default API mock
            _mockHttpHost.RequestHandler = (request) => WrapApiMockDefinedInTestCase(request);
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

        /// <summary>
        /// Gets the workflow action so that it can be asserted in a test.
        /// </summary>
        /// <param name="actionName">The action name.</param>
        /// <returns>The action as a JSON object.</returns>
        public JToken GetWorkflowAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentNullException(nameof(actionName));

            var getActionFromRunHistory = _apiHelper.ActionsContent(WorkflowRunId).Where(actionResult => actionResult["name"].ToString().Equals(actionName)).FirstOrDefault();

            if (getActionFromRunHistory == null)
                throw new TestException($"Action '{actionName}' was not found in the workflow run history");

            return getActionFromRunHistory["properties"];
        }

        /// <summary>
        /// Gets the workflow action status indicating whether the action completed successfully or not.
        /// </summary>
        /// <param name="actionName">The name of the action to be checked.</param>
        public ActionStatus GetWorkflowActionStatus(string actionName)
        {
            JToken actionRunProperties = GetWorkflowAction(actionName);
            return (ActionStatus)Enum.Parse(typeof(ActionStatus), actionRunProperties["status"].ToString());
        }

        /// <summary>
        /// Gets the number of repetitions for a workflow action. An action in an Until or a ForEach loop can be run multiple times.
        /// </summary>
        /// <param name="actionName">The number of repetitions.</param>
        public int GetWorkflowActionRepetitionCount(string actionName)
        {
            JToken actionRunProperties = GetWorkflowAction(actionName);

            // Actions inside a loop will have a 'repetitionCount' property
            if (actionRunProperties["repetitionCount"] != null)
                return actionRunProperties["repetitionCount"].Value<int>();

            // A looping action (for example Until or ForEach) will have an 'iterationCount' property
            if (actionRunProperties["iterationCount"] != null)
                return actionRunProperties["iterationCount"].Value<int>();

            // Else the action has not been repeated
            return 1;
        }

        #endregion // Public Action methods

        #region Public Action Repetition methods

        /// <summary>
        /// Gets the workflow action for a specific repetition so that it can be asserted in a test.
        /// </summary>
        /// <param name="actionName">The action name.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        /// <returns>The action repetition as a JSON object.</returns>
        public JToken GetWorkflowActionRepetition(string actionName, int repetitionNumber)
        {
            if (repetitionNumber <= 0)
                throw new ArgumentException("The repetition number must be greater than 1");

            JToken actionRunProperties = GetWorkflowAction(actionName);

            if (!(actionRunProperties as JObject).ContainsKey("repetitionCount"))
                throw new TestException($"Action '{actionName}' was not part of a repetition running inside a loop");

            int repetitionCount = actionRunProperties["repetitionCount"].Value<int>();

            if (repetitionNumber > repetitionCount)
                throw new TestException($"The action '{actionName}' has run inside a loop and the number of repetitions is {repetitionCount}. Therefore testing a repetition number of {repetitionNumber} is not valid.");

            IEnumerable<JToken> value = _apiHelper.ActionRepetitonsContent(WorkflowRunId, actionName);

            if (value.Count() != repetitionCount)
                throw new TestException($"Repetitions for action '{actionName}' did not run properly, could not find {repetitionCount} repetitions.");

            JToken repetition = value.Where(rep => rep["properties"]["repetitionIndexes"][0]["itemIndex"].Value<int>() == repetitionNumber - 1).First();

            return repetition["properties"];
        }

        /// <summary>
        /// Gets the workflow action status for a repetition, indicating whether the action completed successfully or not.
        /// </summary>
        /// <param name="actionName">The name of the action to be checked.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        public ActionStatus GetWorkflowActionStatus(string actionName, int repetitionNumber)
        {
            JToken actionRunRepetitionProperties = GetWorkflowActionRepetition(actionName, repetitionNumber);
            return (ActionStatus)Enum.Parse(typeof(ActionStatus), actionRunRepetitionProperties["status"].ToString());
        }

        #endregion // Public Action Repetition methods

        #region TriggerWorkflow

        /// <summary>
        /// Trigger a workflow using an empty request content and optional request headers.
        /// </summary>
        /// <param name="method">The HTTP method, this needs to match the method defined in the HTTP trigger in the workflow.</param>
        /// <param name="requestHeaders">The request headers.</param>
        /// <returns>The response from the workflow.</returns>
        /// <remarks>
        /// An empty request body may be used for workflows that contain triggers that do not use a request body, for example a Recurrence trigger.
        /// </remarks>
        public HttpResponseMessage TriggerWorkflow(HttpMethod method, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(null, method, requestHeaders);
        }

        /// <summary>
        /// Trigger a workflow using a request body and optional request headers.
        /// </summary>
        /// <param name="content">The content (including any content headers) for running the workflow, or <c>null</c> if there is no content.</param>
        /// <param name="method">The HTTP method, this needs to match the method defined in the HTTP trigger in the workflow.</param>
        /// <param name="requestHeaders">The request headers.</param>
        /// <returns>The response from the workflow.</returns>
        public HttpResponseMessage TriggerWorkflow(HttpContent content, HttpMethod method, Dictionary<string, string> requestHeaders = null)
        {
            return TriggerWorkflow(content, method, string.Empty, requestHeaders);
        }

        /// <summary>
        /// Trigger a workflow using a request body, a relative path and optional request headers.
        /// </summary>
        /// <param name="content">The content (including any content headers) for running the workflow, or <c>null</c> if there is no content.</param>
        /// <param name="method">The HTTP method, this needs to match the method defined in the HTTP trigger in the workflow.</param>
        /// <param name="relativePath">The relative path to be used in the trigger. The path must already be URL-encoded.</param>
        /// <param name="requestHeaders">The request headers.</param>
        /// <returns>The response from the workflow.</returns>
        public HttpResponseMessage TriggerWorkflow(HttpContent content, HttpMethod method, string relativePath, Dictionary<string, string> requestHeaders = null)
        {
            // Get the callback information for the workflow, including the trigger URL
            CallbackUrlDefinition callbackDef = GetWorkflowCallbackDefinition();

            // Configure the HttpRequestMessage to trigger the workflow
            var httpRequestMessage = new HttpRequestMessage
            {
                Content = content,
                Method = method,
                RequestUri = callbackDef.ValueWithRelativePath(relativePath)
            };

            if (requestHeaders != null)
            {
                foreach (var item in requestHeaders)
                {
                    httpRequestMessage.Headers.Add(item.Key, item.Value);
                }
            }

            LoggingHelper.LogBanner("Starting workflow execution");

            // Run the workflow and wait for completion
            Console.WriteLine($"Workflow trigger: {httpRequestMessage.Method} {httpRequestMessage.RequestUri}");
            HttpResponseMessage response = PollAndReturnFinalWorkflowResponse(httpRequestMessage);

            // Copy the collection of mock requests from the thread-safe collection into a List that is accessible to the test case
            // The requests in the list are ordered in chronological order
            // The list is not thread-safe but this does not matter because the test case is not multi-threaded
            _mockRequestsAsList = _mockRequests.OrderBy(x => x.Timestamp).ToList();

            LoggingHelper.LogBanner("Completed workflow execution");

            // Write out a summary of the mock requests to help with test case creation
            if (_mockRequestsAsList.Count > 0)
            {
                Console.WriteLine("Mocked requests:");
                foreach (MockRequest req in _mockRequestsAsList)
                {
                    Console.WriteLine($"    {req.Timestamp.ToString("HH:mm:ss.fff")}");
                    Console.WriteLine($"      {req.Method} {req.RequestUri.AbsoluteUri}");
                }
            }
            else
            {
                Console.WriteLine("No mocked requests were logged");
            }

            LoggingHelper.LogBanner("Test assertions");

            return response;
        }

        #endregion // TriggerWorkflow

        /// <summary>
        /// Wraps a test assertion in a <c>catch</c> block which logs additional workflow execution information when the assertion fails.
        /// </summary>
        /// <param name="assertion">The test assertion to be run.</param>
        /// <exception cref="AssertFailedException">Thrown when the test assertion fails.</exception>
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

        /// <summary>
        /// Gets the output responses of provided actions to the end user, so that they can verify the expected results.
        /// </summary>
        /// <param name="workflowActionVerifiers"></param>
        /// <returns></returns>
        public Dictionary<string, HttpResponseMessage> GetWorkflowActionOutputResponse(List<WorkflowActionVerifier> workflowActionVerifiers)
        {
            var workflowActionOutputMap = new Dictionary<string, HttpResponseMessage>();

            workflowActionVerifiers.ForEach((workflowActionVerifier) =>
            {
                var getActionFromRunHistory = _apiHelper.ActionsContent(WorkflowRunId).Where(actionResult => actionResult["name"].ToString().Equals(workflowActionVerifier.ActionName)).FirstOrDefault();
                string url = GetActionOutputUri(getActionFromRunHistory, workflowActionVerifier);
                var actionRunResponse = _client.GetAsync(url).Result;
                workflowActionOutputMap.Add(workflowActionVerifier.ActionName, actionRunResponse);
            });

            return workflowActionOutputMap;
        }

        private string GetActionOutputUri(JToken getActionFromRunHistory, WorkflowActionVerifier workflowActionVerifier)
        {
            if (getActionFromRunHistory == null || !(getActionFromRunHistory as JObject).ContainsKey("properties"))
                throw new TestException($"Action named '{workflowActionVerifier.ActionName}' or its properties not found in the run history");

            if ((getActionFromRunHistory["properties"] as JObject).ContainsKey("outputsLink"))
                return getActionFromRunHistory["properties"]["outputsLink"]["uri"].Value<string>();

            if ((getActionFromRunHistory["properties"] as JObject).ContainsKey("repetitionCount"))
            {
                int repetitionCount = getActionFromRunHistory["properties"]["repetitionCount"].Value<int>();

                if (workflowActionVerifier.RepetitionNumber <= 0 || workflowActionVerifier.RepetitionNumber > repetitionCount)
                    throw new TestException($"The action '{workflowActionVerifier.ActionName}' has run inside a loop, so in order to access the run history of particular repetition, you must pass valid repetition number");

                var actionRepetitionRunURI = TestEnvironment.GetRunActionRepetitionsRequestUri(_workflowName, WorkflowRunId, workflowActionVerifier.ActionName);
                var actionRepetitionRunResult = _client.GetAsync(actionRepetitionRunURI).Result.Content.ReadAsAsync<JToken>().Result;
                var value = actionRepetitionRunResult["value"].Value<IEnumerable<JToken>>();

                if (value.Count() != repetitionCount)
                    throw new TestException($"Repetition for action '{workflowActionVerifier.ActionName}' did not run properly. We didn't find all the repetition runs.");

                var specificRepetition = value.ElementAt(workflowActionVerifier.RepetitionNumber - 1);

                if ((specificRepetition as JObject).ContainsKey("properties") && (specificRepetition["properties"] as JObject).ContainsKey("outputsLink"))
                    return specificRepetition["properties"]["outputsLink"]["uri"].Value<string>();
            }

            throw new TestException($"Output run history for action '{workflowActionVerifier.ActionName}' not found. This might mean the action has failed creating output.");
        }

        #region Private methods

        /// <summary>
        /// Get the callback definition for the workflow trigger. 
        /// </summary>
        /// <returns>The callback definition for the workflow trigger.</returns>
        private CallbackUrlDefinition GetWorkflowCallbackDefinition()
        {
            HttpResponseMessage workflowTriggerCallbackResponse;
            try
            {
                workflowTriggerCallbackResponse = _client.PostAsync(TestEnvironment.GetTriggerCallbackRequestUri(flowName: _workflowName, triggerName: "manual"), ContentHelper.CreatePlainStringContent("")).Result;
                workflowTriggerCallbackResponse.EnsureSuccessStatusCode();

                return workflowTriggerCallbackResponse.Content.ReadAsAsync<CallbackUrlDefinition>().Result;
            }
            catch (HttpRequestException hrex) when (hrex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new TestException($"The callback endpoint for workflow '{_workflowName}' was not found. This indicates that the Function runtime could not start the workflow. Enable the Function runtime start-up logging using the 'logging.writeFunctionRuntineStartupLogs' option in 'testConfiguration.json'. Then check the logs for any errors.", hrex);
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                {
                    if (e is SocketException)
                    {
                        Console.WriteLine($"Socket Exception: Error Code = {(e as SocketException).ErrorCode}, Message = {e.Message}");
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Poll the workflow run result every 1 second for MAX_TIME_MINUTES_WHILE_POLLING_WORKFLOW_RESULT time, and try to get the final workflow status other than running.
        /// </summary>
        /// <param name="httpRequestMessage">The request message to trigger the workflow.</param>
        /// <returns>The response from the workflow.</returns>
        private HttpResponseMessage PollAndReturnFinalWorkflowResponse(HttpRequestMessage httpRequestMessage)
        {
            // Call the endpoint for the HTTP trigger
            var initialWorkflowHttpResponse = _client.SendAsync(httpRequestMessage).Result;

            // Store some of the run metadata for test assertions, this may not exist for stateless workflows
            _runId = GetHeader(initialWorkflowHttpResponse.Headers, "x-ms-workflow-run-id");
            _clientTrackingId = GetHeader(initialWorkflowHttpResponse.Headers, "x-ms-client-tracking-id");

            if (initialWorkflowHttpResponse.StatusCode != HttpStatusCode.Accepted)
            {
                return initialWorkflowHttpResponse;
            }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (stopwatch.Elapsed < TimeSpan.FromMinutes(Constants.MAX_TIME_MINUTES_WHILE_POLLING_WORKFLOW_RESULT))
            {
                using (var latestWorkflowHttpResponse = _client.GetAsync(TestEnvironment.GetRunsRequestUriWithManagementHost(flowName: _workflowName)).Result)
                {
                    var latestWorkflowHttpResponseContent = latestWorkflowHttpResponse.Content.ReadAsAsync<JToken>().Result;
                    var runStatusOfWorkflow = latestWorkflowHttpResponseContent["value"][0]["properties"]["status"].ToString();
                    // If we got status code other than Accepted then return the response
                    if (latestWorkflowHttpResponse.StatusCode != HttpStatusCode.Accepted && runStatusOfWorkflow != ActionStatus.Running.ToString())
                    {
                        return latestWorkflowHttpResponse;
                    }
                    Thread.Sleep(1000);
                }
            }

            throw new TestException($"Workflow is taking more than {Constants.MAX_TIME_MINUTES_WHILE_POLLING_WORKFLOW_RESULT} minutes for its execution");
        }

        /// <summary>
        /// Wrap the mock delegate defined in the test case with additional functionality.
        /// </summary>
        /// <param name="httpRequestMessage">Request message for the mocked API call.</param>
        /// <param name="mockDefinedInTestCase">Delegate function that sets the response message for the mocked API call.</param>
        /// <returns>The response message.</returns>
        /// <remarks>
        /// We will always have Archive actions in our workflow. In order to avoid repetition of mocking code for these Archive API calls,
        /// we add the mocked response by default with every test case execution.</remarks>
        private HttpResponseMessage WrapApiMockDefinedInTestCase(HttpRequestMessage httpRequestMessage, Func<HttpRequestMessage, HttpResponseMessage> mockDefinedInTestCase = null)
        {
            if (httpRequestMessage == null)
                throw new ArgumentNullException(nameof(httpRequestMessage));

            // Cache the mock request to enable test assertions
            // Include anything that might be useful to the test author to validate the workflow
            _mockRequests.Add(new MockRequest
            {
                RequestUri = httpRequestMessage.RequestUri,
                Method = httpRequestMessage.Method,
                Headers = CopyHeaders(httpRequestMessage.Headers),
                Content = httpRequestMessage.Content.ReadAsStringAsync().Result,
                ContentHeaders = CopyHeaders(httpRequestMessage.Content.Headers)
            });

            // Wire up the archive mock
            if (httpRequestMessage.RequestUri.AbsolutePath.Contains("Archive"))
                return GetMockArchiveResponse(httpRequestMessage);

            // And then wire up the mock responses defined in the test case
            // If there is no mock defined by the test case, return an empty response
            if (mockDefinedInTestCase == null)
                return new HttpResponseMessage();
            else
                return mockDefinedInTestCase(httpRequestMessage);
        }

        #endregion // Private methods

        #region Private static methods

        /// <summary>
        /// Copy a HTTP headers collection into a dictionary.
        /// </summary>
        /// <param name="headerCollection">The collection of headers.</param>
        /// <returns>A dictionary containing the headers.</returns>
        private static Dictionary<string, IEnumerable<string>> CopyHeaders(HttpHeaders headerCollection)
        {
            if (headerCollection == null)
                return null;

            Dictionary<string, IEnumerable<string>> col = new Dictionary<string, IEnumerable<string>>();
            foreach (var header in headerCollection)
            {
                col.Add(header.Key, header.Value);
            }
            return col;
        }

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

        /// <summary>
        /// Create a response message for the mocked archive API request.
        /// </summary>
        /// <param name="httpRequestMessage">Request message for the mocked API call.</param>
        /// <returns>The response message.</returns>
        private static HttpResponseMessage GetMockArchiveResponse(HttpRequestMessage httpRequestMessage)
        {
            if (httpRequestMessage == null)
                throw new ArgumentNullException(nameof(httpRequestMessage));

            HttpResponseMessage mockedResponse = new HttpResponseMessage
            {
                RequestMessage = httpRequestMessage,
                StatusCode = HttpStatusCode.OK,
                Content = ContentHelper.CreatePlainStringContent("archived")
            };

            return mockedResponse;
        }

        #endregion // Private static methods
    }
}
