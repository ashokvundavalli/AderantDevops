namespace Aderant.Build.TeamFoundation {
    /// <summary>
    /// Provides the type of a TF Build artifact.
    /// </summary>
    internal enum TfBuildArtifactType {
        /// <summary>
        /// The container type.
        /// </summary>
        Container,

        /// <summary>
        /// The file path type.
        /// </summary>
        FilePath,

        /// <summary>
        /// The version control path type.
        /// </summary>
        VersionControl,

        /// <summary>
        /// The git reference type.
        /// </summary>
        GitRef,

        /// <summary>
        /// The TFVC label type.
        /// </summary>
        TfvcLabel
    }
}