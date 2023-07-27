namespace LogicAppUnit
{
    /// <summary>
    /// Defines a workflow that is to be tested.
    /// </summary>
    public class WorkflowTestInput
    {
        /// <summary>
        /// Gets the workflow name.
        /// </summary>
        public string WorkflowName { init; get; }

        /// <summary>
        /// Gets the workflow definition.
        /// </summary>
        public string WorkflowDefinition { init; get; }

        /// <summary>
        /// Gets the workflow filename.
        /// </summary>
        public string WorkflowFilename { init; get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTestInput"/> class.
        /// </summary>
        /// <param name="workflowName">The workflow name.</param>
        /// <param name="workflowDefinition">The workflow definition.</param>
        /// <param name="workflowFilename">The workflow filename.</param>
        public WorkflowTestInput(string workflowName, string workflowDefinition, string workflowFilename = null)
        {
            this.WorkflowName = workflowName;
            this.WorkflowDefinition = workflowDefinition;
            this.WorkflowFilename = workflowFilename ?? Constants.WORKFLOW;
        }
    }
}
