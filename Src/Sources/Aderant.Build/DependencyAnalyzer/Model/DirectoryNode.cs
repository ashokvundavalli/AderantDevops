using System.Diagnostics;

namespace Aderant.Build.DependencyAnalyzer.Model {

    [DebuggerDisplay("DirectoryNode: {" + nameof(Id) + "}")]
    internal sealed class DirectoryNode : AbstractArtifact {

        public DirectoryNode(string id, string directory, bool isPostTargets) {
            ModuleName = id;
            IsPostTargets = isPostTargets;
            Directory = directory;
            Id = CreateName(id, isPostTargets);
        }

        public override string Id { get; }

        public bool IsPostTargets { get; }

        public string ModuleName { get; }
        public string Directory { get; set; }

        public static string CreateName(string name, bool isPostTargets) {
            if (isPostTargets) {
                name += ".AfterBuild";
            } else {
                name += ".BeforeBuild";
            }

            return name;
        }
    }
}
