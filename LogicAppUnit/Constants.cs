namespace LogicAppUnit
{
    /// <summary>
    /// Commonly used hardcoded strings.
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
        internal static readonly string LIB_FOLDER = "lib";
        internal static readonly string CUSTOM_FOLDER = "custom";
        internal static readonly string CUSTOM_LIB_FOLDER = System.IO.Path.Combine(LIB_FOLDER, CUSTOM_FOLDER);
    }
}
