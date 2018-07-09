using System;
using System.Diagnostics;

namespace Aderant.Build.DependencyAnalyzer.Model {

    [DebuggerDisplay("DirectoryNode: {" + nameof(Id) + "}")]
    internal sealed class DirectoryNode : AbstractArtifact {

        [Obsolete]
        public DirectoryNode(string name, bool isCompletion) {

        }

        public DirectoryNode(string id, string directory, bool isAfterTargets) {
            ModuleName = id;
            IsAfterTargets = isAfterTargets;
            Directory = directory;
            Id = CreateName(id, isAfterTargets);
        }

        public override string Id { get; }

        public bool IsAfterTargets { get; }

        public string ModuleName { get; }
        public string Directory { get; set; }

        public static string CreateName(string name, bool isCompletion) {
            if (isCompletion) {
                name += ".AfterBuild";
            } else {
                name += ".BeforeBuild";
            }

            return name;
        }
    }
}
