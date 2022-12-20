namespace LogicAppUnit
{
    /// <summary>
    /// An action and a status to be verified.
    /// </summary>
    public class WorkflowActionVerifier
    {
        /// <summary>
        /// Name of the action to be verified.
        /// </summary>
        public string ActionName { get; set; }

        /// <summary>
        /// Expected action status.
        /// </summary>
        public ActionStatus ActionStatusExpected { get; set; }

        /// <summary>
        /// Repetition number for the action to be verified.
        /// </summary>
        public int RepetitionNumber { get; set; }
    }
}
