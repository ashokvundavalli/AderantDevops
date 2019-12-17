using System;
using System.Collections.Generic;
using System.IO;
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
        /// Begins looking buildable items under the paths provided by <see cref="includePaths" />
        /// </summary>
        public void Grovel(IReadOnlyList<string> includePaths, IReadOnlyList<string> excludePaths) {
            var filePathCollector = new List<string>();

            var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in includePaths) {
                if (physicalFileSystem.DirectoryExists(path)) {
                    filePathCollector.AddRange(GrovelForFiles(path, excludePaths, seenDirectories));
                }
            }

            AssignPaths(filePathCollector);
        }

        private void AssignPaths(List<string> filePathCollector) {
            foreach (var path in filePathCollector) {
                string fileName = Path.GetFileName(path);

                if (string.Equals(fileName, WellKnownPaths.EntryPointFileName)) {
                    makeFiles.Add(path);
                    AddDirectoryInBuild(new[] { Path.GetFullPath(Path.GetDirectoryName(path) + "\\..\\") });
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
            string[] extensions = new string[] {
                "*.csproj",
                "*.dbprojx",
                "*.wixproj",
                WellKnownPaths.EntryPointFileName,
                "dir.props",
                "*.sqlproj"
            };

            return new DirectoryScanner(physicalFileSystem, Logger) {
                ExcludeFilterPatterns = excludeFilterPatterns,
                PreviouslySeenDirectories = seenDirectories ?? new HashSet<string>(),
            }.TraverseDirectoriesAndFindFiles(root, extensions);
        }

        /// <summary>
        /// Expands the scope of the build tree by pulling in directories that are referenced by other sources of dependency
        /// information
        /// </summary>
        public void ExpandBuildTree(IBuildTreeContributorService pipelineService, IEnumerable<string> inputDirectories) {
            HashSet<string> contributorsAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string directory in inputDirectories) {
                var file = Path.Combine(directory, "Build", DependencyManifest.DependencyManifestFileName);

                if (physicalFileSystem.FileExists(file)) {
                    var manifest = new DependencyManifest(file, XDocument.Parse(physicalFileSystem.ReadAllText(file)));

                    foreach (ExpertModule module in manifest.ReferencedModules) {
                        string moduleName = module.Name;

                        // Construct a path that represents a possible directory that also needs to be built.
                        // We will check if it actually exists, or is needed later in the processing
                        string contributorRoot = Path.Combine(Directory.GetParent(directory.TrimTrailingSlashes()).FullName, moduleName);
                        var buildDirectory = Path.Combine(contributorRoot, "Build");

                        if (!contributorsAdded.Contains(buildDirectory)) {
                            string contributorMakeFile = Path.Combine(buildDirectory, WellKnownPaths.EntryPointFileName);

                            pipelineService.AddBuildDirectoryContributor(
                                new BuildDirectoryContribution(contributorMakeFile) {
                                    DependencyFile = file
                                });

                            contributorsAdded.Add(contributorRoot);

                            AddDirectoryInBuild(new[] { contributorRoot });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a directory to the build tree.
        /// </summary>
        public void AddDirectoryInBuild(IEnumerable<string> inputDirectories) {
            foreach (string inputDirectory in inputDirectories) {
                if (inputDirectory != null) {
                    directoriesInBuild.Add(PathUtility.TrimTrailingSlashes(inputDirectory));
                }
            }
        }
    }
}
