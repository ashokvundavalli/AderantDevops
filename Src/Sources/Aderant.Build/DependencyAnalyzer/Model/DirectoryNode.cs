using System.Diagnostics;

namespace Aderant.Build.DependencyAnalyzer.Model {

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