namespace LogicAppUnit
{
    /// <summary>
    /// Commonly used hardcoded strings.
    /// Do not include any constants that are specific to any particular Logic App or workflow.
    /// </summary>
    /// <remarks>
    /// This class and its members are <i>internal</i> because they are only intended for use within the test framework, not for use by the test classes.
    /// </remarks>
    internal static class Constants
    {
        // Logic App files
        internal static readonly string WORKFLOW = "workflow.json";
        internal static readonly string LOCAL_SETTINGS = "local.settings.json";
        internal static readonly string PARAMETERS = "parameters.json";
        internal static readonly string CONNECTIONS = "connections.json";
        internal static readonly string HOST = "host.json";

        // Logic App folders
        internal static readonly string ARTIFACTS_FOLDER = "Artifacts";
    }
}
