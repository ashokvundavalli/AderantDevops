namespace Aderant.Build {

    /// <summary>
    /// Specifies the available modes for fetching dependencies
    /// </summary>
    internal enum DependencyFetchMode {
        /// <summary>
        /// The default behaviour implementation
        /// </summary>
        Default,

        /// <summary>
        /// Explicitly fetch all third party modules referenced by the branch if possible (implies Default).
        /// </summary>
        ThirdParty,
    }
}