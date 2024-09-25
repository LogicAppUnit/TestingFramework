using System;
using System.Globalization;

namespace LogicAppUnit.Hosting
{
    /// <summary>
    /// Defines the URLs for the workflow management API operations.
    /// </summary>
    internal class TestEnvironment
    {
        /// <summary>
        /// The Edge Preview API version (2019-10-01-edge-preview).
        /// </summary>
        public static readonly string EdgePreview20191001ApiVersion = "2019-10-01-edge-preview";

        /// <summary>
        /// The Preview API version (2020-05-01-preview).
        /// </summary>
        public static readonly string EdgePreview20200501ApiVersion = "2020-05-01-preview";

        /// <summary>
        /// The local machine name.
        /// </summary>
        public static readonly string MachineHostName = OperatingSystem.IsWindows() ? Environment.MachineName : "localhost";

        /// <summary>
        /// Workflow runtime webhook extension URI base path.
        /// </summary>
        public static readonly string WorkflowExtensionBasePath = "/runtime/webhooks/workflow";

        /// <summary>
        /// Workflow runtime webhook extension URI management base path.
        /// </summary>
        public static readonly string FlowExtensionManagementBasePath = $"{TestEnvironment.WorkflowExtensionBasePath}/api/management";

        /// <summary>
        /// Workflow runtime webhook extension URI workflow management base path.
        /// </summary>
        public static readonly string FlowExtensionWorkflowManagementBasePath = $"{TestEnvironment.FlowExtensionManagementBasePath}/workflows";

        /// <summary>
        /// The test host URI.
        /// </summary>
        public static readonly string FlowV2TestHostUri = new UriBuilder(Uri.UriSchemeHttp, TestEnvironment.MachineHostName, 7071).Uri.ToString().TrimEnd('/');

        /// <summary>
        /// The mock test host URI.
        /// </summary>
        public static readonly string FlowV2MockTestHostUri = new UriBuilder(Uri.UriSchemeHttp, TestEnvironment.MachineHostName, 7075).Uri.ToString().TrimEnd('/');

        /// <summary>
        /// Workflow runtime webhook extension URI workflow management base path.
        /// </summary>
        public static readonly string ManagementWorkflowBaseUrl = TestEnvironment.FlowV2TestHostUri + FlowExtensionWorkflowManagementBasePath;

        /// <summary>
        /// Gets the workflow trigger callback URI.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <param name="triggerName">The trigger name.</param>
        public static string GetTriggerCallbackRequestUri(string workflowName, string triggerName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/triggers/{2}/listCallbackUrl?api-version={3}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    workflowName,
                    triggerName,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the request URI for the 'List Workflow Runs' operation.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <param name="top">The maximum number of records to return.</param>
        public static string GetListWorkflowRunsRequestUri(string workflowName, int? top = null)
        {
            return top != null
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/{1}/runs?api-version={2}&$top={3}",
                        TestEnvironment.ManagementWorkflowBaseUrl,
                        workflowName,
                        TestEnvironment.EdgePreview20191001ApiVersion,
                        top.Value)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/{1}/runs?api-version={2}",
                        TestEnvironment.ManagementWorkflowBaseUrl,
                        workflowName,
                        TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the request URI for the 'Get Workflow Run' operation.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <param name="runId">The run id.</param>
        public static string GetGetWorkflowRunRequestUri(string workflowName, string runId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/runs/{2}?api-version={3}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    workflowName,
                    runId,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the request URI for the 'List Workflow Run Actions' operation.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <param name="runId">The run id.</param>
        public static string GetListWorkflowRunActionsRequestUri(string workflowName, string runId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/runs/{2}/actions?api-version={3}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    workflowName,
                    runId,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the request URI for the 'List Workflow Run Action Repetitions' operation.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <param name="runId">The run id.</param>
        /// <param name="actionName">The action name.</param>
        public static string GetListWorkflowRunActionRepetitionsRequestUri(string workflowName, string runId, string actionName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/runs/{2}/actions/{3}/repetitions?api-version={4}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    workflowName,
                    runId,
                    actionName,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }
    }
}
