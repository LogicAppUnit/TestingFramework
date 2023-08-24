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
        public static readonly string MachineHostName = Environment.MachineName; //OperatingSystem.IsWindows() ? Environment.MachineName : "localhost";

        /// <summary>
        /// Flow runtime webhook extension URI base path.
        /// </summary>
        public static readonly string WorkflowExtensionBasePath = "/runtime/webhooks/workflow";

        /// <summary>
        /// Flow runtime webhook extension URI management base path.
        /// </summary>
        public static readonly string FlowExtensionManagementBasePath = $"{TestEnvironment.WorkflowExtensionBasePath}/api/management";

        /// <summary>
        /// Flow runtime webhook extension URI workflow management base path.
        /// </summary>
        public static readonly string FlowExtensionWorkflowManagementBasePath = $"{TestEnvironment.FlowExtensionManagementBasePath}/workflows";

        /// <summary>
        /// The test host URI.
        /// </summary>
        public static readonly string FlowV2TestHostUri = (new UriBuilder(Uri.UriSchemeHttp, "localhost", 7071).Uri.ToString()).TrimEnd('/');

        /// <summary>
        /// The mock test host URI.
        /// </summary>
        public static readonly string FlowV2MockTestHostUri = (new UriBuilder(Uri.UriSchemeHttp, TestEnvironment.MachineHostName, 7075).Uri.ToString()).TrimEnd('/');

        /// <summary>
        /// The test host URI.
        /// </summary>
        public static readonly string FlowV2TestManagementHostUri = (new UriBuilder(Uri.UriSchemeHttp, "localhost", 7071).Uri.ToString()).TrimEnd('/');

        /// <summary>
        /// Flow runtime webhook extension URI management base path.
        /// </summary>
        public static readonly string ManagementBaseUrl = TestEnvironment.FlowV2TestHostUri + FlowExtensionManagementBasePath;

        /// <summary>
        /// Flow runtime webhook extension URI workflow management base path.
        /// </summary>
        public static readonly string ManagementWorkflowBaseUrl = TestEnvironment.FlowV2TestHostUri + FlowExtensionWorkflowManagementBasePath;

        /// <summary>
        /// Flow runtime webhook extension URI workflow management base path with management host.
        /// </summary>
        public static readonly string ManagementWorkflowBaseUrlWithManagementHost = TestEnvironment.FlowV2TestManagementHostUri + FlowExtensionWorkflowManagementBasePath;

        /// <summary>
        /// Gets the workflow trigger callback URI.
        /// </summary>
        /// <param name="flowName">The flow name.</param>
        /// <param name="triggerName">The trigger name.</param>
        public static string GetTriggerCallbackRequestUri(string flowName, string triggerName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/triggers/{2}/listCallbackUrl?api-version={3}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    flowName,
                    triggerName,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the workflow runs request URI using the management host.
        /// </summary>
        /// <param name="flowName">The flow name.</param>
        /// <param name="top">The maximum number of records to return.</param>
        public static string GetRunsRequestUriWithManagementHost(string flowName, int? top = null)
        {
            return top != null
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/{1}/runs?api-version={2}&$top={3}",
                        TestEnvironment.ManagementWorkflowBaseUrlWithManagementHost,
                        flowName,
                        TestEnvironment.EdgePreview20191001ApiVersion,
                        top.Value)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}/{1}/runs?api-version={2}",
                        TestEnvironment.ManagementWorkflowBaseUrlWithManagementHost,
                        flowName,
                        TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the workflow run actions URI.
        /// </summary>
        /// <param name="flowName">The flow name.</param>
        /// <param name="runName">The run name.</param>
        public static string GetRunActionsRequestUri(string flowName, string runName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/runs/{2}/actions?api-version={3}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    flowName,
                    runName,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }

        /// <summary>
        /// Gets the workflow run action repetitions URI.
        /// </summary>
        /// <param name="flowName">The flow name.</param>
        /// <param name="runName">The run name.</param>
        /// <param name="actionName">The action name.</param>
        public static string GetRunActionRepetitionsRequestUri(string flowName, string runName, string actionName)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/runs/{2}/actions/{3}/repetitions?api-version={4}",
                    TestEnvironment.ManagementWorkflowBaseUrl,
                    flowName,
                    runName,
                    actionName,
                    TestEnvironment.EdgePreview20191001ApiVersion);
        }
    }
}
