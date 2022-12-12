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

        private string _runId;

        private readonly MockHttpHost _mockHttpHost;

        private readonly WorkflowTestHost _workflowTestHost;

        // Requests sent to the mock test server that are generated by the workflow during its execution.
        // Use a ConcurrentBag to store the requests during the test execution to ensure thread safety of this collection
        private ConcurrentBag<MockRequest> _mockRequests;
        private List<MockRequest> _mockRequestsAsList;

        private JToken _workflowRunResponseContent;
        /// <summary>
        /// Response content of logic app workflow run
        /// </summary>
        private JToken WorkflowRunResponseContent
        {
            get
            {
                if (_workflowRunResponseContent == null)
                {
                    var workflowRunResponse = _client.GetAsync(TestEnvironment.GetRunsRequestUriWithManagementHost(flowName: _workflowName)).Result;
                    _workflowRunResponseContent = workflowRunResponse.Content.ReadAsAsync<JToken>().Result;
                }
                return _workflowRunResponseContent;
            }
        }

        private IEnumerable<JToken> _actionRunResponseContent;
        /// <summary>
        /// Response content of all the actions in logic app workflow
        /// </summary>
        private IEnumerable<JToken> ActionRunResponseContent
        {
            get
            {
                if (_actionRunResponseContent == null)
                {
                    _runId = WorkflowRunResponseContent["value"].FirstOrDefault()["name"].ToString();
                    _actionRunResponseContent = GetActionRunResponseInChunks(TestEnvironment.GetRunActionsRequestUri(flowName: _workflowName, runName: _runId));
                }
                return _actionRunResponseContent;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestRunner"/> class.
        /// </summary>
        /// <param name="loggingConfig">The logging configuration for the test execution.</param>
        /// <param name="client">The HTTP client.</param>
        /// <param name="workflowName">The name of the workflow being tested.</param>
        /// <param name="inputs">The content of the workflow definition file.</param>
        /// <param name="localSettings">The contents of the local settings file.</param>
        /// <param name="parameters">The contents of the parameters file.</param>
        /// <param name="connections">The contents of the connections file.</param>
        /// <param name="host">The contents of the host file.</param>
        /// <param name="artifactsDirectory">The (optional) artifacts directory containing maps and schemas that are used by the workflow being tested.</param>
        public TestRunner(
            TestConfigurationLogging loggingConfig,
            HttpClient client,
            string workflowName,
            WorkflowTestInput[] inputs = null, string localSettings = null, string parameters = null, string connections = null, string host = null, DirectoryInfo artifactsDirectory = null)
        {
            if (loggingConfig == null)
                throw new ArgumentNullException(nameof(loggingConfig));
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(workflowName))
                throw new ArgumentNullException(nameof(workflowName));

            LoggingHelper.LogBanner("Starting test runner");

            if (!loggingConfig.WriteFunctionRuntineStartupLogs)
                Console.WriteLine("Logging of the Function runtime startup logs is disabled. This can be enabled using the 'logging.writeFunctionRuntineStartupLogs' option in 'testConfiguration.json'.");

            _workflowTestHost = new WorkflowTestHost(inputs, localSettings, parameters, connections, host, artifactsDirectory, loggingConfig.WriteFunctionRuntineStartupLogs);
            _client = client;
            _workflowName = workflowName;
            _mockHttpHost = new MockHttpHost();

            // Initialise the cached mocked requests
            _mockRequests = new ConcurrentBag<MockRequest>();

            // Configure the default API mock
            _mockHttpHost.RequestHandler = (request) => WrapApiMockDefinedInTestCase(request);
        }

        /// <summary>
        /// Some workflows might be big, hence its run response is received in chunks where nextLink has the remaining set of action responses.
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private IEnumerable<JToken> GetActionRunResponseInChunks(string url)
        {
            var actionRunResponse = _client.GetAsync(url).Result;
            var chunkOfContent = actionRunResponse.Content.ReadAsAsync<JToken>().Result;

            // Using recursion to get all the chunks of results.
            if ((chunkOfContent as JObject).ContainsKey("nextLink"))
            {
                var nextChunkOfContent = GetActionRunResponseInChunks(chunkOfContent["nextLink"].ToString());
                return chunkOfContent["value"].Union(nextChunkOfContent);
            }

            return chunkOfContent["value"];
        }

        /// <summary>
        /// Gets the list of mock requests that were created by the workflow during the test execution.
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

        /// <summary>
        /// Gets the workflow run id.
        /// </summary>
        /// <returns>The workflow run id.</returns>
        public string WorkflowRunId
        {
            get
            {
                return WorkflowRunResponseContent["value"][0]["name"].ToString();
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
                return (WorkflowRunStatus)Enum.Parse(typeof(WorkflowRunStatus), WorkflowRunResponseContent["value"][0]["properties"]["status"].ToString());
            }
        }

        /// <summary>
        /// Gets the workflow action status indicating whether the action completed successfully or not.
        /// </summary>
        /// <param name="actionName">The name of the action to be checked.</param>
        public ActionStatus GetWorkflowActionStatus(string actionName)
        {
            if (actionName == null)
                throw new ArgumentNullException(nameof(actionName));

            var getActionFromRunHistory = ActionRunResponseContent.Where(actionResult => actionResult["name"].ToString().Equals(actionName)).FirstOrDefault();

            if (getActionFromRunHistory == null)
                throw new TestException($"Action '{actionName}' was not found in the workflow run history");

            return (ActionStatus)Enum.Parse(typeof(ActionStatus), getActionFromRunHistory["properties"]["status"].ToString());
        }

        /// <summary>
        /// Mocks for all the outgoing http/service-bus/sql-connector calls.
        /// Make sure to properly add the matching URI fragment key with if-else block for creating your specific mock action response.
        /// </summary>
        public Func<HttpRequestMessage, HttpResponseMessage> AddApiMocks
        {
            set
            {
                _mockHttpHost.RequestHandler = (request) => WrapApiMockDefinedInTestCase(request, value);
            }
        }

        /// <summary>
        /// Disposes the resources.
        /// </summary>
        public void Dispose()
        {
            this._mockHttpHost.Dispose();
            this._workflowTestHost.Dispose();
        }

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
            // Get the callback information for the workflow, including the trigger URL
            CallbackUrlDefinition callbackDef = GetWorkflowCallbackDefinition();

            // Configure the HttpRequestMessage to trigger the workflow
            var httpRequestMessage = new HttpRequestMessage
            {
                Content = content,
                Method = method,
                RequestUri = callbackDef.Value
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
                List<JToken> failedActions = ActionRunResponseContent.Where(actionResult =>
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
                var getActionFromRunHistory = ActionRunResponseContent.Where(actionResult => actionResult["name"].ToString().Equals(workflowActionVerifier.ActionName)).FirstOrDefault();
                string url = GetActionOutputURI(getActionFromRunHistory, workflowActionVerifier);
                var actionRunResponse = _client.GetAsync(url).Result;
                workflowActionOutputMap.Add(workflowActionVerifier.ActionName, actionRunResponse);
            });

            return workflowActionOutputMap;
        }

        private string GetActionOutputURI(JToken getActionFromRunHistory, WorkflowActionVerifier workflowActionVerifier)
        {
            if (getActionFromRunHistory == null || !(getActionFromRunHistory as JObject).ContainsKey("properties"))
                throw new TestException($"Action named '{workflowActionVerifier.ActionName}' or its properties not found in the run history");

            if ((getActionFromRunHistory["properties"] as JObject).ContainsKey("outputsLink"))
                return getActionFromRunHistory["properties"]["outputsLink"]["uri"].Value<string>();

            if ((getActionFromRunHistory["properties"] as JObject).ContainsKey("repetitionCount"))
            {
                int repetitionCount = getActionFromRunHistory["properties"]["repetitionCount"].Value<int>();

                if (workflowActionVerifier.RepetitionNumber <= 0 || workflowActionVerifier.RepetitionNumber > repetitionCount)
                    throw new TestException($"The acion '{workflowActionVerifier.ActionName}' has ran inside a loop, so in order to access the run history of particular repetition, you must pass valid repetition number");

                var actionRepetitionRunURI = TestEnvironment.GetRunActionsRepetationRequestUri(_workflowName, _runId, workflowActionVerifier.ActionName);
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

        /// <summary>
        /// Get the callback definition for the workflow to run. 
        /// </summary>
        /// <returns>The callback definition for the workflow.</returns>
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
            // Hit API of HTTP based workflow
            var initialWorkflowHttpResponse = _client.SendAsync(httpRequestMessage).Result;

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

        /// <summary>
        /// Copy the request headers into a Dictionary.
        /// </summary>
        /// <param name="headerCollection">The collection of request headers.</param>
        /// <returns>A dictionary containing the request headers.</returns>
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
                Content = ContentHelper.CreatePlainStringContent("archive/datatype/entityId/hello.json")
            };
            return mockedResponse;
        }
    }
}
