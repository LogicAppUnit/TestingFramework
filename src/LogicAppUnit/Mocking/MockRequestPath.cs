namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// A path for mock request matching.
    /// </summary>
    internal class MockRequestPath
    {
        /// <summary>
        /// Gets the path to be matched.
        /// </summary>
        public string Path { init; get; }

        /// <summary>
        /// Gets the type of matching to be used.
        /// </summary>
        public PathMatchType MatchType { init; get; }
    }
}
