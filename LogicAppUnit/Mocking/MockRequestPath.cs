namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// A header for mock request matching.
    /// </summary>
    internal class MockRequestPath
    {
        /// <summary>
        /// The path to be matched.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The type of matching to be used.
        /// </summary>
        public PathMatchType MatchType { get; set; }

    }
}
