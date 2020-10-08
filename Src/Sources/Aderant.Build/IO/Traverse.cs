using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Aderant.Build.Utilities;

namespace Aderant.Build.IO {
    internal class DirectoryScanner {
        private readonly ILogger logger;
        private readonly IFileSystem physicalFileSystem;
        private readonly ConcurrentDictionary<string, byte> previouslySeenDirectories;

        public DirectoryScanner(IFileSystem physicalFileSystem, ILogger logger) {
            this.physicalFileSystem = physicalFileSystem;
            this.logger = logger;
            this.previouslySeenDirectories = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> ExcludeFilterPatterns { get; set; }

        public ICollection<string> PreviouslySeenDirectories {
            get {
                return previouslySeenDirectories.Keys;
            }
            set {
                if (value != null) {
                    foreach (var directory in value) {
                        previouslySeenDirectories.TryAdd(directory, 0);
                    }
                }
            }
        }

        /// <summary>
        /// Finds files with the pattern(s) specified under the given directory.
        /// Does not follow symlinks.
        /// </summary>
        /// <param name="root">The starting directory</param>
        /// <param name="extensions">The path segment to look for</param>
        /// <param name="maxDepth">The depth of the search with -1 being unbounded</param>
        public IEnumerable<string> TraverseDirectoriesAndFindFiles(string root, IList<string> extensions, int maxDepth = -1) {
            ConcurrentBag<string> files = new ConcurrentBag<string>();

            TraverseDirectoriesAndFindFilesInternal(root, extensions.ToList(), files, maxDepth, 0);

            return new SortedSet<string>(files, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Locate a file in either the directory specified or a location in the
        /// directory structure above that directory.
        /// Searches in this order:
        /// - .
        /// - .\xyz
        /// - .\..\xyz
        /// - ..\..\..\xyz
        /// </summary>
        /// <param name="startingDirectory">The starting directory</param>
        /// <param name="fileName">The file name to look for</param>
        /// <param name="ceiling">The directories that the search cannot look above</param>
        /// <returns></returns>
        public string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName, string[] ceiling) {
            if (ceiling == null) {
                ceiling = Array.Empty<string>();
            }

            // Canonicalize our starting location
            string lookInDirectory = Path.GetFullPath(startingDirectory);

            do {
                // Construct the path that we will use to test against
                string possibleFileDirectory = Path.Combine(lookInDirectory, fileName);

                // If we successfully locate the file in the directory that we're
                // looking in, simply return that location. Otherwise we'll
                // keep moving up the tree.
                if (physicalFileSystem.FileExists(possibleFileDirectory)) {
                    // We've found the file, return the directory we found it in
                    return lookInDirectory;
                } else {
                    // GetDirectoryName will return null when we reach the root
                    // terminating our search
                    lookInDirectory = Path.GetDirectoryName(lookInDirectory);
                }

                foreach (var stopDirectory in ceiling) {
                    if (PathComparer.Default.Equals(lookInDirectory, stopDirectory)) {
                        return string.Empty;
                    }
                }
            }
            while (lookInDirectory != null);

            // When we didn't find the location, then return an empty string
            return string.Empty;
        }


        private void TraverseDirectoriesAndFindFilesInternal(string root, List<string> extensions, ConcurrentBag<string> files, int maxDepth, int currentDepth) {
            if (maxDepth > 0 && currentDepth > maxDepth) {
                return;
            }

            List<string> directories = new List<string>();
            if (!string.IsNullOrEmpty(root)) {
                directories.Add(root);
            }

            directories.AddRange(physicalFileSystem.GetDirectories(root));

            Parallel.ForEach(directories,
                new ParallelOptions {
                    MaxDegreeOfParallelism = ParallelismHelper.MaxDegreeOfParallelism
                },
                directory => {
                    int thisPartitionCurrentDepth = currentDepth;
                    if (previouslySeenDirectories.ContainsKey(directory)) {
                        return;
                    }

                    if (logger != null) {
                        logger.Debug("Traversing directory: {0}", directory);
                    }

                    var skip = PathUtility.IsPathExcludedByFilters(directory, ExcludeFilterPatterns);

                    if (!skip) {
                        skip = physicalFileSystem.IsSymlink(directory);
                    }

                    if (skip) {
                        previouslySeenDirectories.TryAdd(directory, 0);

                        if (logger != null) {
                            logger.Debug("Skipped file: {0}", directory);
                        }

                        return;
                    }

                    foreach (var extension in extensions) {
                        var filesFromDirectory = physicalFileSystem.GetFiles(directory, extension, false);
                        foreach (var file in filesFromDirectory) {
                            if (!PathUtility.IsPathExcludedByFilters(file, ExcludeFilterPatterns)) {
                                files.Add(file);
                            }
                        }
                    }

                    previouslySeenDirectories.TryAdd(directory, 0);

                    TraverseDirectoriesAndFindFilesInternal(directory, extensions, files, maxDepth, ++thisPartitionCurrentDepth);
                });
        }
    }
}
