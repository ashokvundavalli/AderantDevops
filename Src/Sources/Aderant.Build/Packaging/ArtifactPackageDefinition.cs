using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.Packaging {
    internal class ArtifactPackageDefinition : IArtifact {
        private IReadOnlyCollection<PathSpec> pathSpecs;

        public ArtifactPackageDefinition(string id, IEnumerable<PathSpec> pathSpecs)
            : this(id, pathSpecs.ToList()) {
        }

        public ArtifactPackageDefinition(string id, IReadOnlyCollection<PathSpec> pathSpecs) {
            Id = id;
            this.pathSpecs = pathSpecs;
        }

        public bool IsAutomaticallyGenerated { get; set; }

        /// <summary>
        /// The test artifact package prefix
        /// </summary>
        public static string TestPackagePrefix { get; } = "Tests.";

        public bool IsInternalDevelopmentPackage { get; set; }
        public ArtifactType ArtifactType { get; set; }

        public string Id { get; }

        public IReadOnlyCollection<IDependable> GetDependencies() {
            return null;
        }

        public void AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
        }

        /// <summary>
        /// Gets the file paths contained within the artifact.
        /// </summary>
        public IReadOnlyCollection<PathSpec> GetFiles() {
            return pathSpecs;
        }

        public static PathSpec CreatePathSpecification(string[] trimPaths, string fullPath, string targetPath) {
            string outputRelativePath = null;

            if (trimPaths != null) {
                foreach (var path in trimPaths) {
                    if (fullPath.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
                        outputRelativePath = fullPath.Remove(0, path.Length);
                        break;
                    }
                }
            }

            return new PathSpec(fullPath, Path.Combine(targetPath ?? string.Empty, outputRelativePath ?? Path.GetFileName(fullPath)));
        }

        public static ArtifactPackageDefinition Create(string name, Action<ArtifactPackageDefinitionBuilder> builder) {
            return new ArtifactPackageDefinitionBuilder(name, builder).Build();
        }

        public string GetRootDirectory() {
            return GetFiles().FirstOrDefault().Location;
        }
    }

    internal class ArtifactPackageDefinitionBuilder {
        private ArtifactPackageDefinition artifact;
        private List<PathSpec> files;

        public ArtifactPackageDefinitionBuilder(string name, Action<ArtifactPackageDefinitionBuilder> builder) {
            files = new List<PathSpec>();
            artifact = new ArtifactPackageDefinition(name, files);

            builder(this);
        }

        public ArtifactPackageDefinitionBuilder AddFile(string location, string destination) {
            files.Add(new PathSpec(location, destination));
            return this;
        }

        public ArtifactPackageDefinition Build() {
            return artifact;
        }
    }
}
