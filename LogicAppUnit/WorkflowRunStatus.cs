namespace LogicAppUnit
{
    /// <summary>
    /// Possible statuses for a workflow run.
    /// </summary>
    public enum WorkflowRunStatus
    {
        /// <summary>
        /// The workflow has not been triggered.
        /// </summary>
        NotTriggered,

        /// <summary>
        /// The workflow run stopped or didn't finish due to external problems, for example, a system outage.
        /// </summary>
        Aborted,

        /// <summary>
        /// The workflow run was triggered and started but received a cancel request.
        /// </summary>
        Cancelled,

        /// <summary>
        /// At least one action in the workflow run failed. No subsequent actions in the workflow were set up to handle the failure.
        /// </summary>
        Failed,

        /// <summary>
        /// The run was triggered and is in progress, or the run is throttled due to action limits or the current pricing plan.
        /// </summary>
        Running,

        /// <summary>
        /// The workflow run succeeded. If any action failed, a subsequent action in the workflow handled that failure.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The workflow run timed out because the current duration exceeded the workflow run duration limit.
        /// </summary>
        TimedOut,

        /// <summary>
        /// The workflow run hasn't started or is paused, for example, due to an earlier workflow instance that's still running.
        /// </summary>
        Waiting
    }
}
