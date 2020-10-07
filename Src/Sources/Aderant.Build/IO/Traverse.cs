using System.Collections.Generic;
using Aderant.Build.Logging;

namespace Aderant.Build.IO {
    internal class DirectoryScanner {
        private readonly ILogger logger;
        private readonly IFileSystem physicalFileSystem;

        public DirectoryScanner(IFileSystem physicalFileSystem, ILogger logger) {
            this.physicalFileSystem = physicalFileSystem;
            this.logger = logger;
        }

        public IReadOnlyList<string> ExcludeFilterPatterns { get; set; }

        public HashSet<string> PreviouslySeenDirectories { get; set; }

        public IEnumerable<string> TraverseDirectoriesAndFindFiles(string root, IList<string> extensions) {
            List<string> files = new List<string>();

            TraverseDirectoriesAndFindFilesInternal(root, extensions, files);

            return files;
        }

        private void TraverseDirectoriesAndFindFilesInternal(string root, IList<string> extensions, IList<string> files) {
            // This algorithm can result in a stack overflow if PreviouslySeenDirectories is not supplied
            List<string> directories = new List<string>();
            if (!string.IsNullOrEmpty(root)) {
                directories.Add(root);
            }

            directories.AddRange(physicalFileSystem.GetDirectories(root));

            foreach (var directory in directories) {
                if (PreviouslySeenDirectories != null && PreviouslySeenDirectories.Contains(directory)) {
                    continue;
                }

                if (logger != null) {
                    logger.Debug("Traversing directory: " + directory);
                }

                var skip = PathUtility.IsPathExcludedByFilters(directory, ExcludeFilterPatterns);

                if (!skip) {
                    skip = physicalFileSystem.IsSymlink(directory);
                }

                if (skip) {
                    if (PreviouslySeenDirectories != null) {
                        PreviouslySeenDirectories.Add(directory);
                    }

                    if (logger != null) {
                        logger.Debug("Skipped file: " + directory);
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