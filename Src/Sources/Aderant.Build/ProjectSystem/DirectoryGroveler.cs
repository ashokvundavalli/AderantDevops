using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.IO;
using Aderant.Build.Logging;
using Aderant.Build.PipelineService;

namespace Aderant.Build.ProjectSystem {

    internal class DirectoryGroveler {

        private readonly SortedSet<string> directoriesInBuild = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> extensibilityFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> makeFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly IFileSystem physicalFileSystem;
        private readonly SortedSet<string> projectFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        public DirectoryGroveler(IFileSystem physicalFileSystem) {
            this.physicalFileSystem = physicalFileSystem;
        }

        public IReadOnlyCollection<string> DirectoriesInBuild {
            get { return directoriesInBuild; }
        }

        public IReadOnlyCollection<string> MakeFiles {
            get { return makeFiles; }
        }

        public IReadOnlyCollection<string> ExtensibilityFiles {
            get { return extensibilityFiles; }
        }

        public IReadOnlyCollection<string> ProjectFiles {
            get { return projectFiles; }
        }

        public BuildTaskLogger Logger { get; set; }

        /// <summary>
        /// Begins looking buildable items under the paths provided by <see cref="contextInclude"/>
        /// </summary>
        public void Grovel(IReadOnlyList<string> contextInclude, IReadOnlyList<string> excludePaths) {
            var filePathCollector = new List<string>();

            var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in contextInclude) {
                filePathCollector.AddRange(GrovelForFiles(path, excludePaths, seenDirectories));
            }

            AssignPaths(filePathCollector);
        }

        private void AssignPaths(List<string> filePathCollector) {
            foreach (var path in filePathCollector) {
                string fileName = Path.GetFileName(path);

                if (string.Equals(fileName, WellKnownPaths.EntryPointFileName)) {
                    makeFiles.Add(path);
                    directoriesInBuild.Add(Path.GetFullPath(Path.GetDirectoryName(path) + "\\..\\"));
                    continue;
                }

                if (string.Equals(fileName, "dir.props")) {
                    extensibilityFiles.Add(path);
                    continue;
                }

                projectFiles.Add(path);
            }
        }

        /// <summary>
        /// Internal API.
        /// </summary>
        internal IEnumerable<string> GrovelForFiles(string root, IReadOnlyList<string> excludeFilterPatterns, HashSet<string> seenDirectories = null) {
            string[] extensions = new[] {
                "*.csproj",
                "*.wixproj",
                WellKnownPaths.EntryPointFileName,
                "dir.props",
            };

            return new DirectoryScanner(physicalFileSystem) {
                ExcludeFilterPatterns = excludeFilterPatterns,
                PreviouslySeenDirectories = seenDirectories,
            }.TraverseDirectoriesAndFindFiles(root, extensions);
        }

        /// <summary>
        /// Expands the scope of the build tree by pulling in directories that are referenced by other sources of dependency information
        /// </summary>
        public void ExpandBuildTree(IBuildTreeContributorService pipelineService) {
            HashSet<string> contributorsAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string directory in DirectoriesInBuild) {
                var file = Path.Combine(directory, "Build", DependencyManifest.DependencyManifestFileName);

                if (physicalFileSystem.FileExists(file)) {
                    var manifest = new DependencyManifest(file, XDocument.Parse(physicalFileSystem.ReadAllText(file)));

                    foreach (ExpertModule module in manifest.ReferencedModules) {
                        string moduleName = module.Name;

                        // Construct a path that represents a possible directory that also needs to be built.
                        // We will check if it actually exists, or is needed later in the processing
                        string contributorRoot = Path.Combine(Directory.GetParent(directory.TrimTrailingSlashes()).FullName, moduleName, "Build");

                        if (!contributorsAdded.Contains(contributorRoot)) {
                            string contributorMakeFile = Path.Combine(contributorRoot, WellKnownPaths.EntryPointFileName);

                            pipelineService.AddBuildDirectoryContributor(
                                new BuildDirectoryContribution(contributorMakeFile) {
                                    DependencyFile = file
                                });

                            contributorsAdded.Add(contributorRoot);
                        }
                    }
                }
            }
        }
    }

}