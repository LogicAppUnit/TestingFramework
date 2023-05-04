namespace LogicAppUnit.Mocking
{
    /// <summary>
    /// Path match type.
    /// </summary>
    public enum PathMatchType
    {
        /// <summary>
        /// Value is an exact match for the path, e.g. '\api\v1\this-service\this-operation'.
        /// </summary>
        Exact,

        /// <summary>
        /// Value is contained within the path, e.g. 'v1\this-service'.
        /// </summary>
        Contains,

        /// <summary>
        /// Value matches the end of the path, e.g. 'this-operation'.
        /// </summary>
        EndsWith
    }
}
