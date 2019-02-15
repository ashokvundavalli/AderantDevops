using System.Collections.Generic;

namespace Aderant.Build.IO {
    internal class DirectoryScanner {
        private readonly IFileSystem physicalFileSystem;

        public DirectoryScanner(IFileSystem physicalFileSystem) {
            this.physicalFileSystem = physicalFileSystem;
        }

        public IReadOnlyList<string> ExcludeFilterPatterns { get; set; }

        public HashSet<string> PreviouslySeenDirectories { get; set; }

        public IEnumerable<string> TraverseDirectoriesAndFindFiles(string root, string[] extensions) {
            var files = new List<string>();

            TraverseDirectoriesAndFindFilesInternal(root, extensions, files);

            return files;
        }

        private void TraverseDirectoriesAndFindFilesInternal(string root, string[] extensions, List<string> files) {
            List<string> directories = new List<string> { root };
            directories.AddRange(physicalFileSystem.GetDirectories(root));

            foreach (var directory in directories) {
                if (PreviouslySeenDirectories != null && PreviouslySeenDirectories.Contains(directory)) {
                    continue;
                }

                var skip = PathUtility.IsPathExcludedByFilters(directory, ExcludeFilterPatterns);

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
                    var filesFromDirectory = physicalFileSystem.GetFiles(directory, extension, false);
                    foreach (var file in filesFromDirectory) {
                        if (!PathUtility.IsPathExcludedByFilters(file, ExcludeFilterPatterns)) {
                            files.Add(file);
                        }
                    }
                }

                if (PreviouslySeenDirectories != null) {
                    PreviouslySeenDirectories.Add(directory);
                }

                TraverseDirectoriesAndFindFilesInternal(directory, extensions, files);
            }
        }
    }
}