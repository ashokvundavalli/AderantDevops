using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using Aderant.Build.Logging;

namespace Aderant.Build.ProjectSystem {

    internal class DirectoryGroveler {
        private readonly IFileSystem physicalFileSystem;

        private readonly SortedSet<string> directoriesInBuild = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> extensibilityFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SortedSet<string> makeFiles = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
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

        public void Grovel(string[] contextInclude, string[] excludePaths) {
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

        internal IEnumerable<string> GrovelForFiles(string root, IReadOnlyCollection<string> excludeFilterPatterns, HashSet<string> seenDirectories = null) {
            string[] extensions = new[] {
                "*.csproj",
                "*.wixproj",
                "TFSBuild.proj",
                "dir.props",
            };

            return new Traverse(physicalFileSystem) {
                ExcludeFilterPatterns = excludeFilterPatterns,
                PreviouslySeenDirectories = seenDirectories,
            }.TraverseDirectoriesAndFindFiles(root, extensions);
        }
    }

    internal class Traverse {
        private readonly IFileSystem physicalFileSystem;

        public Traverse(IFileSystem physicalFileSystem) {
            this.physicalFileSystem = physicalFileSystem;

        }

        public IReadOnlyCollection<string> ExcludeFilterPatterns { get; set; }
        public HashSet<string> PreviouslySeenDirectories { get; set; }

        public IEnumerable<string> TraverseDirectoriesAndFindFiles(string root, string[] extensions) {
            var files = new List<string>();

            TraverseDirectoriesAndFindFilesInternal(root, extensions, files);

            return files;
        }

        private void TraverseDirectoriesAndFindFilesInternal(string root, string[] extensions, List<string> files) {
            IEnumerable<string> directories = physicalFileSystem.GetDirectories(root);

            foreach (var directory in directories) {
                if (PreviouslySeenDirectories != null && PreviouslySeenDirectories.Contains(directory)) {
                    continue;
                }

                var skip = DoesPathContainExcludeFilterSegment(directory, ExcludeFilterPatterns);

                if (!skip) {
                    skip = physicalFileSystem.IsSymlink(directory);
                }

                if (skip) {
                    if (PreviouslySeenDirectories != null) {
                        PreviouslySeenDirectories.Add(directory);
                    }
                    continue;
                }

                foreach (var extension in extensions) {
                    files.AddRange(physicalFileSystem.GetFiles(directory, extension, false));
                }

                if (PreviouslySeenDirectories != null) {
                    PreviouslySeenDirectories.Add(directory);
                }

                TraverseDirectoriesAndFindFilesInternal(directory, extensions, files);
            }
        }

        internal static bool DoesPathContainExcludeFilterSegment(string path, IReadOnlyCollection<string> excludeFilterPatterns) {
            if (excludeFilterPatterns != null) {
                foreach (var pattern in excludeFilterPatterns) {
                    string resolvedPath = pattern;

                    if (pattern.Contains("..")) {
                        resolvedPath = Path.GetFullPath(pattern);
                    }

                    if (WildcardPattern.ContainsWildcardCharacters(resolvedPath)) {
                        WildcardPattern wildcardPattern = new WildcardPattern(resolvedPath, WildcardOptions.IgnoreCase);

                        if (wildcardPattern.IsMatch(path)) {
                            return true;
                        }
                    }

                    if (path.IndexOf(resolvedPath, StringComparison.OrdinalIgnoreCase) >= 0) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}