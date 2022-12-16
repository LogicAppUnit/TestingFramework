namespace LogicAppUnit
{
    /// <summary>
    /// Defines a workflow that is to be tested.
    /// </summary>
    public class WorkflowTestInput
    {
        /// <summary>
        /// Gets or sets the workflow name.
        /// </summary>
        public string WorkflowName { get; set; }

        /// <summary>
        /// Gets or sets the workflow definition.
        /// </summary>
        public string WorkflowDefinition { get; set; }

        /// <summary>
        /// Gets or sets the workflow filename.
        /// </summary>
        public string WorkflowFilename { get; set; }

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
