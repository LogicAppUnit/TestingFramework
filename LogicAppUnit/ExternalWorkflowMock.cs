using System.Net;

namespace LogicAppUnit
{
    /// <summary>
    /// Represents a dependency on an external workflow that is to be mocked.
    /// </summary>
    public class ExternalWorkflowMock
    {
        /// <summary>
        /// Name of the workflow to be mocked.
        /// </summary>
        public string WorkflowNameToMock { get; set; }

        /// <summary>
        /// Status code for the mocked workflow response.
        /// </summary>
        public HttpStatusCode StatusCodeOfMockResponse { get; set; }

        /// <summary>
        /// Content for the mocked workflow response.
        /// </summary>
        public object BodyOfMockResponse { get; set; }
    }
}
