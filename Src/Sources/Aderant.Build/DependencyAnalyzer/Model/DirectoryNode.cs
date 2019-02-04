using System.Diagnostics;

namespace Aderant.Build.DependencyAnalyzer.Model {

    [DebuggerDisplay("DirectoryNode: {" + nameof(Id) + "}")]
    internal sealed class DirectoryNode : AbstractArtifact {

        public DirectoryNode(string id, string directory, bool isPostTargets) {
            IsPostTargets = isPostTargets;
            Directory = directory;
            Id = CreateName(id, isPostTargets);
        }

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
