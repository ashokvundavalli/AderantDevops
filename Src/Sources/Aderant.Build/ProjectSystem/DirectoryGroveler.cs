using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.Logging;

namespace Aderant.Build.ProjectSystem {

    internal class DirectoryGroveler {
        private readonly IFileSystem physicalFileSystem;

        private readonly HashSet<string> extensibilityFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> makeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> projectFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> directoriesInBuild = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

        public void Grovel(string[] contextInclude, string[] excludePaths) {
            var filePathCollector = new List<string>();

            foreach (var path in contextInclude) {
                filePathCollector.AddRange(GrovelForFiles(path, excludePaths));
            }

            AssignPaths(filePathCollector);
        }

        private void AssignPaths(List<string> filePathCollector) {
            foreach (var path in filePathCollector) {
                string fileName = Path.GetFileName(path);

                if (string.Equals(fileName, "TFSBuild.proj")) {
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

        internal IEnumerable<string> GrovelForFiles(string directory, IReadOnlyCollection<string> excludeFilterPatterns) {
            var files = new List<string>();

            string[] extensions = new[] {
                "*.csproj",
                "*.wixproj",
                "TFSBuild.proj",
                "dir.props",
            };

            foreach (var extension in extensions) {
                files.AddRange(physicalFileSystem.GetFiles(directory, extension, true));
            }

            return FilterFiles(files, excludeFilterPatterns);
        }

        public static IEnumerable<string> FilterFiles(List<string> files, IReadOnlyCollection<string> excludeFilterPatterns) {
            foreach (var projectFilePath in files) {
                bool skip = false;

                if (excludeFilterPatterns != null) {
                    foreach (var pattern in excludeFilterPatterns) {
                        string resolvedPath = pattern;

                        if (pattern.Contains("..")) {
                            resolvedPath = Path.GetFullPath(pattern);
                        }

                        if (WildcardPattern.ContainsWildcardCharacters(resolvedPath)) {
                            WildcardPattern wildcardPattern = new WildcardPattern(resolvedPath, WildcardOptions.IgnoreCase);

                            if (wildcardPattern.IsMatch(projectFilePath)) {
                                skip = true;
                                break;
                            }
                        }

                        if (projectFilePath.IndexOf(resolvedPath, StringComparison.OrdinalIgnoreCase) >= 0) {
                            skip = true;
                            break;
                        }
                    }
                }

                if (!skip) {
                    yield return projectFilePath;
                }
            }
        }
    }
}