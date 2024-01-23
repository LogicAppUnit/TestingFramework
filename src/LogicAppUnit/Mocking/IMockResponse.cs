namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// A Mocked response consisting of a request matcher and a corresponding response builder.
    /// </summary>
    public interface IMockResponse
    {
        /// <summary>
        /// Configure the mocked response when the request is matched.
        /// </summary>
        /// <param name="mockResponseBuilder">The mocked response.</param>
        void RespondWith(IMockResponseBuilder mockResponseBuilder);

        /// <summary>
        /// Configure the default mocked response using a status code of 200 (OK), no response content and no additional response headers.
        /// </summary>
        void RespondWithDefault();
    }
}
