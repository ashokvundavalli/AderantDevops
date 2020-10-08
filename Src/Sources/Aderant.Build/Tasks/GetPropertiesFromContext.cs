using System;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Extracts key properties from the context and returns them to MSBuild
    /// </summary>
    public sealed class GetPropertiesFromContext : BuildOperationContextTask {

        [Output]
        public string ArtifactStagingDirectory { get; set; }

        [Output]
        public string SharedDependencyDirectory { get; set; }

        /// <Remarks>
        /// Unfortunately we support 'DependenciesDirectory' being specified in this file
        /// as it crosses the boundary between compile and dependency resolution.
        /// Ideally the build would tell the resolver what to do via parameters.props
        /// but that ship has sailed and instead the branch config file can override the dependency path
        /// </Remarks>
        public string BranchConfigFile { get; set; }

        [Output]
        public bool IsDesktopBuild {
            get { return Context.IsDesktopBuild; }
        }

        [Output]
        public string BuildSystemDirectory {
            get { return Context.BuildSystemDirectory; }
        }

        [Output]
        public string[] IncludePaths {
            get {
                return Context.Include;
            }
        }

        [Output]
        public ITaskItem[] ChangedFiles {
            get {
                if (Context.SourceTreeMetadata.Changes != null) {
                    return Context.SourceTreeMetadata.Changes.Select(x => (ITaskItem)new TaskItem(x.FullPath))
                        .ToArray();
                }

                return Array.Empty<ITaskItem>();
            }
        }

        [Output]
        public string BuildFlavor { get; set; }

        public override bool ExecuteTask() {
            base.Execute();

            var context = Context;

            SetFlavor(context);

            if (!string.IsNullOrWhiteSpace(BranchConfigFile)) {
                ReadBranchConfigFile(BranchConfigFile, new PhysicalFileSystem());
            }

            if (!string.IsNullOrWhiteSpace(ArtifactStagingDirectory)) {
                context.ArtifactStagingDirectory = ArtifactStagingDirectory;
            }

            if (context.BuildRoot != null) {
                Log.LogMessage(MessageImportance.Low, "Build root: " + context.BuildRoot);
            }

            PipelineService.Publish(context);

            return !Log.HasLoggedErrors;
        }

        private void ReadBranchConfigFile(string branchConfigFile, IFileSystem fileSystem) {
            if (fileSystem.FileExists(branchConfigFile)) {
                var stream = fileSystem.OpenFile(branchConfigFile);

                var document = XDocument.Load(stream);

                ResolverSettingsReader.ReadResolverSettings(null, document, branchConfigFile, Context.BuildRoot, out string sharedDependencyDirectory);
                if (!string.IsNullOrWhiteSpace(sharedDependencyDirectory)) {
                    SharedDependencyDirectory = sharedDependencyDirectory;
                }
            }
        }

        private void SetFlavor(BuildOperationContext context) {
            if (context.BuildMetadata != null) {
                if (!string.IsNullOrEmpty(context.BuildMetadata.Flavor)) {
                    BuildFlavor = context.BuildMetadata.Flavor;
                } else {
                    if (Context.Switches.Release) {
                        BuildFlavor = "Release";
                    } else {
                        BuildFlavor = "Debug";
                    }

                    context.BuildMetadata.Flavor = BuildFlavor;
                }
            }
        }
    }
}
