using System.Diagnostics;

namespace Aderant.Build.DependencyAnalyzer.Model {

    /// <summary>
    /// A directory within the build tree.
    /// The build tree will execute a set of pre/post targets per directory which is represented by this device.
    /// </summary>
    [DebuggerDisplay("DirectoryNode: {" + nameof(Id) + "}")]
    internal sealed class DirectoryNode : AbstractArtifact {

        public DirectoryNode(string id, string directory, bool isPostTargets) {
            IsPostTargets = isPostTargets;
            Directory = directory;
            this.DirectoryName = id;
            Id = CreateName(id, isPostTargets);
        }

        /// <summary>
        /// Gets the name of the directory.
        /// </summary>
        public string DirectoryName { get; }

        /// <summary>
        /// Gets the build instance unique id for this instance.
        /// </summary>
        public override string Id { get; }

        public bool IsPostTargets { get; }

        /// <summary>
        /// The directory this node points to.
        /// </summary>
        public string Directory { get; private set; }

        /// <summary>
        /// Sets a value indicating if this node was created during tree analysis.
        /// If false then the user explicitly added this directory as a target.
        /// </summary>
        public bool AddedByDependencyAnalysis { get; set; }

        /// <summary>
        /// Indicates if this node has any children that are being built.
        /// Used to determine if a graph fix up is required.
        /// </summary>
        public bool IsBuildingAnyProjects { get; set; }

        public bool? RetrievePrebuilts { get; set; }

        private static string CreateName(string name, bool isPostTargets) {
            if (isPostTargets) {
                name += ".Post";
            } else {
                name += ".Pre";
            }

            return name;
        }
    }
}