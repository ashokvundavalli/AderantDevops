using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.Logging;

namespace Aderant.Build.ProjectSystem {

    internal class DirectoryGroveler {
        private readonly IFileSystem physicalFileSystem;

        private readonly List<string> extensibilityFiles = new List<string>();
        private readonly List<string> makeFiles = new List<string>();
        private readonly List<string> projectFiles = new List<string>();
        private ICollection<string> directoriesInBuild = new List<string>();

        public DirectoryGroveler(IFileSystem physicalFileSystem) {
            this.physicalFileSystem = physicalFileSystem;
        }

        public ICollection<string> DirectoriesInBuild {
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
            var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) {
                { "TFSBuild.proj", makeFiles },
                { "dir.props", extensibilityFiles }
            };

            foreach (var path in filePathCollector) {
                Log("Found file: " + path);

                string fileName = Path.GetFileName(path);

                List<string> listToAddTo;
                if (map.TryGetValue(fileName, out listToAddTo)) {
                    listToAddTo.Add(path);
                } else {
                    directoriesInBuild.Add(Path.GetDirectoryName(path));

                    projectFiles.Add(path);
                }
            }

            var rootPaths = FindCommonBuildPaths();
            directoriesInBuild = rootPaths;
        }

        private HashSet<string> FindCommonBuildPaths() {
            string commonPath = FindCommonPath(Path.DirectorySeparatorChar.ToString(), directoriesInBuild);

            HashSet<string> rootPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var splitChar = new[] { Path.DirectorySeparatorChar };

            foreach (string s in directoriesInBuild) {
                int pos = s.IndexOf(commonPath, StringComparison.OrdinalIgnoreCase);
                if (pos == 0) {
                    string remove = s.Remove(0, commonPath.Length);

                    string[] rootPart = remove.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);

                    rootPaths.Add(Path.Combine(commonPath, rootPart[0]));
                } else {
                    rootPaths.Add(s);
                }
            }

            return rootPaths;
        }

        private void Log(string message) {
            if (Logger != null) {
                Logger.Info(message);
            }
        }

        internal static string FindCommonPath(string separator, ICollection<string> paths) {
            string commonPath = String.Empty;

            List<string> separatedPath = paths
                .First(str => str.Length == paths.Max(st2 => st2.Length))
                .Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            foreach (string pathSegment in separatedPath) {
                if (commonPath.Length == 0 && paths.All(str => str.StartsWith(pathSegment, StringComparison.OrdinalIgnoreCase))) {
                    commonPath = pathSegment;
                } else if (paths.All(str => str.StartsWith(commonPath + separator + pathSegment, StringComparison.OrdinalIgnoreCase))) {
                    commonPath += separator + pathSegment;
                } else {
                    break;
                }
            }

            return commonPath;
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