using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.Packaging {
    internal class ArtifactPackage : IArtifact {
        private List<PathSpec> pathSpecs;

        public ArtifactPackage(string id, IEnumerable<PathSpec> pathSpecs) {
            Id = id;
            this.pathSpecs = pathSpecs.ToList();
        }

        public string Id { get; }

        public IReadOnlyCollection<IDependable> GetDependencies() {
            return null;
        }

        public void AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
        }

        public IReadOnlyCollection<PathSpec> GetFiles() {
            return pathSpecs;
        }

        public static PathSpec CreatePathSpecification(string solutionRoot, string[] trimPaths, string fullPath, string targetPath) {
            string outputRelativePath = null;

            foreach (var path in trimPaths) {
                if (fullPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
                    outputRelativePath = fullPath.Remove(0, path.Length);
                    break;
                }
            }

            if (outputRelativePath == null) {
                throw new InvalidOperationException(string.Format("Unable to construct an output relative path from {0} to {1}", solutionRoot, fullPath));
            }

            return new PathSpec(fullPath, Path.Combine(targetPath ?? string.Empty, outputRelativePath));
        }
    }
}
