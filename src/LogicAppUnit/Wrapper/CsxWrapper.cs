namespace LogicAppUnit.Wrapper
{
    /// <summary>
    /// Wrapper class to manage the C# scripts that are used by a workflow.
    /// </summary>
    public class CsxWrapper
    {
        /// <summary>
        /// Gets the C# script content.
        /// </summary>
        public string Script { init; get; }

        /// <summary>
        /// Gets the C# script relative path.
        /// </summary>
        public string RelativePath { init; get; }

        /// <summary>
        /// Gets the C# script filename.
        /// </summary>
        public string Filename { init; get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CsxWrapper"/> class.
        /// </summary>
        /// <param name="script">The script content.</param>
        /// <param name="relativePath">The script relative path.</param>
        /// <param name="filename">The script filename</param>
        public CsxWrapper(string script, string relativePath, string filename)
        {
            this.Script = script;
            this.RelativePath = relativePath;
            this.Filename = filename;
        }
    }
}
