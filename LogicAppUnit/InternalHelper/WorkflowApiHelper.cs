using LogicAppUnit.Hosting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace LogicAppUnit.InternalHelper
{
    /// <summary>
    /// Helper class to manage the REST API calls to the workflow management server, and cache the responses to improve efficiency.
    /// </summary>
    internal class WorkflowApiHelper
    {
        private const string StatelessWarningMessage = "If this is a stateless workflow, make sure that the 'Workflows.<workflow name>.OperationOptions' setting is set to 'WithStatelessRunHistory'.";

        private readonly HttpClient _client;
        private readonly string _workflowName;

        private JToken _workflowRunContent;
        private IEnumerable<JToken> _actionsContent;
        private readonly Dictionary<string, IEnumerable<JToken>> _actionRepetitionsContent;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowApiHelper"/> class.
        /// </summary>
        /// <param name="client">The HTTP client.</param>
        /// <param name="workflowName">The name of the workflow being tested.</param>
        public WorkflowApiHelper(HttpClient client, string workflowName)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrEmpty(workflowName))
                throw new ArgumentNullException(nameof(workflowName));

            _client = client;
            _workflowName = workflowName;

            _actionRepetitionsContent = new Dictionary<string, IEnumerable<JToken>>();
        }

        #region Response caching

        /// <summary>
        /// Gets the content for the workflow run.
        /// </summary>
        /// <returns>The content.</returns>
        public JToken WorkflowRunContent()
        {
            if (_workflowRunContent == null)
            {
                // The API responds with an array of workflow runs, we only want the first one (the most recent)
                _workflowRunContent = GetWorkflowRun(TestEnvironment.GetRunsRequestUriWithManagementHost(_workflowName)).First();
            }
            return _workflowRunContent;
        }

        /// <summary>
        /// Gets the content of all the actions in the workflow run.
        /// </summary>
        /// <param name="workflowRunId">The run id for the workflow.</param>
        /// <returns>The content for the actions.</returns>
        public IEnumerable<JToken> ActionsContent(string workflowRunId)
        {
            if (string.IsNullOrEmpty(workflowRunId))
                throw new ArgumentNullException(nameof(workflowRunId));

            if (_actionsContent == null)
            {
                _actionsContent = GetActionsInChunks(TestEnvironment.GetRunActionsRequestUri(_workflowName, workflowRunId));
            }
            return _actionsContent;
        }

        /// <summary>
        /// Gets the content for a repetition action in the workflow run.
        /// </summary>
        /// <param name="workflowRunId">The run id for the workflow.</param>
        /// <param name="actionName">The action name.</param>
        /// <returns>The content for the repetition action.</returns>
        public IEnumerable<JToken> ActionRepetitonsContent(string workflowRunId, string actionName)
        {
            if (string.IsNullOrEmpty(workflowRunId))
                throw new ArgumentNullException(nameof(workflowRunId));
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentNullException(nameof(actionName));

            if (!_actionRepetitionsContent.ContainsKey(actionName))
            {
                IEnumerable<JToken> response = GetActionRepetitions(actionName, TestEnvironment.GetRunActionRepetitionsRequestUri(_workflowName, workflowRunId, actionName));
                _actionRepetitionsContent.Add(actionName, response);
            }
            return _actionRepetitionsContent[actionName];
        }

        #endregion // Response caching

        #region REST API calls

        /// <summary>
        /// Gets the input message or output message for an action.
        /// </summary>
        /// <param name="url">URL to get the action message.</param>
        /// <param name="messageType">Either 'input' or 'output'.</param>
        /// <returns>The action message.</returns>
        public JToken GetActionMessage(string url, string messageType)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));
            if (string.IsNullOrEmpty(messageType))
                throw new ArgumentNullException(nameof(messageType));

            JToken actionMessageContent;
            using (var actionContentResponse = _client.GetAsync(url).Result)
            {
                actionContentResponse.EnsureSuccessStatusCode();
                actionMessageContent = actionContentResponse.Content.ReadAsAsync<JToken>().Result;
            }

            return actionMessageContent;
        }

        /// <summary>
        /// Get the workflow run response.
        /// </summary>
        /// <param name="url">URL to get the workflow run response.</param>
        /// <returns>The workflow run response.</returns>
        private IEnumerable<JToken> GetWorkflowRun(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            // Documentation: https://learn.microsoft.com/en-us/rest/api/logic/workflow-runs/list
            JToken responseContent;
            using (var workflowRunResponse = _client.GetAsync(url).Result)
            {
                workflowRunResponse.EnsureSuccessStatusCode();
                responseContent = workflowRunResponse.Content.ReadAsAsync<JToken>().Result;
            }

            if (!responseContent["value"].Any())
                throw new TestException($"There is no workflow run response. {StatelessWarningMessage}");

            return responseContent["value"];
        }

        /// <summary>
        /// Get the action responses for a workflow run.
        /// </summary>
        /// <param name="url">URL to get the action responses.</param>
        /// <returns>The action responses.</returns>
        private IEnumerable<JToken> GetActionsInChunks(string url)
        {
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            // Documentation: https://learn.microsoft.com/en-us/rest/api/logic/workflow-run-actions/list
            JToken actionsContent;
            using (var actionsResponse = _client.GetAsync(url).Result)
            {
                actionsResponse.EnsureSuccessStatusCode();
                actionsContent = actionsResponse.Content.ReadAsAsync<JToken>().Result;
            }

            // Use recursion to get all the results
            if ((actionsContent as JObject).ContainsKey("nextLink"))
            {
                var nextChunkOfContent = GetActionsInChunks(actionsContent["nextLink"].ToString());
                return actionsContent["value"].Union(nextChunkOfContent);
            }

            if (!actionsContent["value"].Any())
                throw new TestException($"There are no action responses for the workflow run. {StatelessWarningMessage}");

            return actionsContent["value"];
        }

        /// <summary>
        /// Get the action repetition responses for a repeated action in a workflow run.
        /// </summary>
        /// <param name="actionName">The name of the action.</param>
        /// <param name="url">URL to get the action repetition responses.</param>
        /// <returns>The action repetition responses.</returns>
        private IEnumerable<JToken> GetActionRepetitions(string actionName, string url)
        {
            if (string.IsNullOrEmpty(actionName))
                throw new ArgumentNullException(nameof(actionName));
            if (string.IsNullOrEmpty(url))
                throw new ArgumentNullException(nameof(url));

            // Documentation: https://learn.microsoft.com/en-us/rest/api/logic/workflow-run-action-repetitions
            JToken actionRepetitionsContent;
            using (var actionRepetitionsResponse = _client.GetAsync(url).Result)
            {
                actionRepetitionsResponse.EnsureSuccessStatusCode();
                actionRepetitionsContent = actionRepetitionsResponse.Content.ReadAsAsync<JToken>().Result;
            }

            if (!actionRepetitionsContent["value"].Any())
                throw new TestException($"There are no action repetition responses for action '{actionName}' in the workflow run. {StatelessWarningMessage}");

            return actionRepetitionsContent["value"];
        }

        #endregion // REST API calls
    }
}
