using LogicAppUnit.Mocking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace LogicAppUnit
{
    /// <summary>
    /// Runs a workflow and makes available the run execution history.
    /// </summary>
    public interface ITestRunner : IDisposable
    {
        #region Mock request handling

        /// <summary>
        /// Configures a delegate function that creates a mocked response based on a request.
        /// </summary>
        Func<HttpRequestMessage, HttpResponseMessage> AddApiMocks { set; }

        /// <summary>
        /// Configures a mocked response, consisting of a request matcher and a corresponding response builder.
        /// </summary>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        /// <returns>The mocked response.</returns>
        IMockResponse AddMockResponse(IMockRequestMatcher mockRequestMatcher);

        /// <summary>
        /// Configures a named mocked response, consisting of a request matcher and a corresponding response builder.
        /// </summary>
        /// <param name="name">Name of the mock.</param>
        /// <param name="mockRequestMatcher">The request matcher.</param>
        /// <returns>The mocked response.</returns>
        IMockResponse AddMockResponse(string name, IMockRequestMatcher mockRequestMatcher);

        /// <summary>
        /// Gets the mock requests that were created by the workflow during the test execution.
        /// </summary>
        /// <remarks>
        /// The requests are ordered in chronological order, with the most recent request at the start of the list.
        /// </remarks>
        List<MockRequest> MockRequests { get; }

        #endregion // Mock request handling

        #region Workflow properties

        /// <summary>
        /// Gets the workflow run id.
        /// </summary>
        /// <returns>The workflow run id.</returns>
        string WorkflowRunId { get; }

        /// <summary>
        /// Gets the workflow client tracking id.
        /// </summary>
        /// <returns>The workflow client tracking id.</returns>
        string WorkflowClientTrackingId { get; }

        /// <summary>
        /// Gets the workflow run status indicating whether the workflow ran successfully or not.
        /// </summary>
        /// <returns>The workflow run status.</returns>
        WorkflowRunStatus WorkflowRunStatus { get; }

        #endregion // Workflow properties

        #region Action methods

        /// <summary>
        /// Gets the workflow action so that it can be asserted in a test.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <returns>The action as a JSON object.</returns>
        JToken GetWorkflowAction(string actionName);

        /// <summary>
        /// Gets the workflow action status indicating whether the action completed successfully or not.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        ActionStatus GetWorkflowActionStatus(string actionName);

        /// <summary>
        /// Gets the input for a workflow action.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <returns>The input.</returns>
        JToken GetWorkflowActionInput(string actionName);

        /// <summary>
        /// Gets the output for a workflow action.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <returns>The output.</returns>
        JToken GetWorkflowActionOutput(string actionName);

        /// <summary>
        /// Gets the number of repetitions for a workflow action. An action in an Until or a ForEach loop can be run multiple times.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <returns>The number of repetitions.</returns>
        int GetWorkflowActionRepetitionCount(string actionName);

        /// <summary>
        /// Gets the properties that have been tracked by a workflow action.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <returns>The tracked properties, including their names and values.</returns>
        Dictionary<string, string> GetWorkflowActionTrackedProperties(string actionName);

        #endregion // Action methods

        #region Action Repetition methods

        /// <summary>
        /// Gets the workflow action for a specific repetition so that it can be asserted in a test.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        /// <returns>The action repetition as a JSON object.</returns>
        JToken GetWorkflowActionRepetition(string actionName, int repetitionNumber);

        /// <summary>
        /// Gets the workflow action status for a repetition, indicating whether the action completed successfully or not.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        ActionStatus GetWorkflowActionStatus(string actionName, int repetitionNumber);

        /// <summary>
        /// Gets the input for a workflow action for a repetition.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        /// <returns>The input.</returns>
        JToken GetWorkflowActionInput(string actionName, int repetitionNumber);

        /// <summary>
        /// Gets the output for a workflow action for a repetition.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        /// <returns>The output.</returns>
        JToken GetWorkflowActionOutput(string actionName, int repetitionNumber);

        /// <summary>
        /// Gets the properties that have been tracked by a workflow action for a repetition.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="repetitionNumber">The repetition number.</param>
        /// <returns>The tracked properties, including their names and values.</returns>
        Dictionary<string, string> GetWorkflowActionTrackedProperties(string actionName, int repetitionNumber);

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
        HttpResponseMessage TriggerWorkflow(HttpMethod method, Dictionary<string, string> requestHeaders = null);

        /// <summary>
        /// Trigger a workflow using a request body and optional request headers.
        /// </summary>
        /// <param name="content">The content (including any content headers) for running the workflow, or <c>null</c> if there is no content.</param>
        /// <param name="method">The HTTP method, this needs to match the method defined in the HTTP trigger in the workflow.</param>
        /// <param name="requestHeaders">The request headers.</param>
        /// <returns>The response from the workflow.</returns>
        HttpResponseMessage TriggerWorkflow(HttpContent content, HttpMethod method, Dictionary<string, string> requestHeaders = null);

        /// <summary>
        /// Trigger a workflow using a request body, a relative path and optional request headers.
        /// </summary>
        /// <param name="content">The content (including any content headers) for running the workflow, or <c>null</c> if there is no content.</param>
        /// <param name="method">The HTTP method, this needs to match the method defined in the HTTP trigger in the workflow.</param>
        /// <param name="relativePath">The relative path to be used in the trigger. The path must already be URL-encoded.</param>
        /// <param name="requestHeaders">The request headers.</param>
        /// <returns>The response from the workflow.</returns>
        HttpResponseMessage TriggerWorkflow(HttpContent content, HttpMethod method, string relativePath, Dictionary<string, string> requestHeaders = null);

        #endregion // TriggerWorkflow

        /// <summary>
        /// Wraps a test assertion in a <c>catch</c> block which logs additional workflow execution information when the assertion fails.
        /// </summary>
        /// <param name="assertion">The test assertion to be run.</param>
        /// <exception cref="AssertFailedException">Thrown when the test assertion fails.</exception>
        void ExceptionWrapper(Action assertion);
    }
}
