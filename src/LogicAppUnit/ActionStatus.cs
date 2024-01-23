namespace LogicAppUnit
{
    /// <summary>
    /// Possible statuses for a workflow action.
    /// </summary>
    public enum ActionStatus
    {
        /// <summary>
        /// The action stopped or didn't finish due to external problems, for example, a system outage.
        /// </summary>
        Aborted,

        /// <summary>
        /// The action was running but received a cancel request.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The action failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The action is currently running.
        /// </summary>
        Running,

        /// <summary>
        /// The action was skipped because its runAfter conditions weren't met, for example, a preceding action failed.
        /// </summary>
        Skipped,

        /// <summary>
        /// The action succeeded.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The action stopped due to the timeout limit specified by that action's settings.
        /// </summary>
        TimedOut,

        /// <summary>
        /// The action is waiting for an inbound request from a caller.
        /// </summary>
        Waiting
    }
}