namespace LogicAppUnit
{
    /// <summary>
    /// Defines a C# script that is to be tested.
    /// </summary>
    public class CsxTestInput
    {
        /// <summary>
        /// Gets the C# script content
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
        /// Initializes a new instance of the <see cref="CsxTestInput"/> class.
        /// </summary>
        /// <param name="script">The script content.</param>
        /// <param name="relativePath">The script relative path.</param>
        /// <param name="filename">The script filename</param>
        public CsxTestInput(string script, string relativePath, string filename)
        {
            this.Script = script;
            this.RelativePath = relativePath;
            this.Filename = filename;
        }
    }
}
