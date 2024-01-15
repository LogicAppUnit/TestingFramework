namespace LogicAppUnit.Mocking
{
    internal class MockRequestMatchResult
    {
        /// <summary>
        /// Gets the match result, <c>true</c> if the request was matched, otherwise <c>false</c>.
        /// </summary>
        public bool IsMatch { init; get; }

        /// <summary>
        /// Gets the match log, indicating why a request was not matched.
        /// </summary>
        public string MatchLog { init; get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockRequestMatcher"/> class.
        /// </summary>
        /// <param name="isMatch"><c>true</c> if the request was matched, otherwise <c>false</c>.</param>
        public MockRequestMatchResult(bool isMatch) : this(isMatch, string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MockRequestMatcher"/> class.
        /// </summary>
        /// <param name="isMatch"><c>true</c> if the request was matched, otherwise <c>false</c>.</param>
        /// <param name="matchLog">Match log, indicating why a request was not matched.</param>
        public MockRequestMatchResult(bool isMatch, string matchLog)
        {
            IsMatch = isMatch;
            MatchLog = matchLog;
        }
    }
}
