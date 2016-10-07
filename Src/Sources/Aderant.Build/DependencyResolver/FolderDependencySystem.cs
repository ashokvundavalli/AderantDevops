﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver {
    internal class FolderDependencySystem {
        private readonly IFileSystem2 fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="FolderDependencySystem"/> class.
        /// </summary>
        /// <param name="fileSystem">The file system.</param>
        public FolderDependencySystem(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
        }

        protected virtual bool HasDropPath(string requirementPath) {
            return fileSystem.DirectoryExists(requirementPath);
        }

        /// <summary>
        /// Gets the binaries path for a requirement.
        /// </summary>
        /// <param name="resolverRequestDropPath">The resolver request drop path.</param>
        /// <param name="requirement">The requirement.</param>
        public virtual string GetBinariesPath(string resolverRequestDropPath, IDependencyRequirement requirement) {
            string newRequirementPath = AdjustDropPathToBranch(resolverRequestDropPath, requirement);

            string requirementPath = HandleRequirementType(newRequirementPath, requirement);

            if (!HasDropPath(requirementPath)) {
                throw new BuildNotFoundException($"Drop location {requirementPath} does not exist");
            }

            if (requirement.VersionRequirement != null && !string.IsNullOrEmpty(requirement.VersionRequirement.AssemblyVersion)) {
                string[] entries = fileSystem.GetDirectories(requirementPath).ToArray();
                string[] orderedBuilds = OrderBuildsByBuildNumber(entries);

                foreach (string build in orderedBuilds) {
                    var files = fileSystem.GetFiles(build, null, false);

                    foreach (string file in files) {
                        if (file.IndexOf("build.failed", StringComparison.OrdinalIgnoreCase) >= 0) {
                            break;
                        }

                        if (file.IndexOf("build.succeeded", StringComparison.OrdinalIgnoreCase) >= 0) {
                            string binariesPath;
                            if (HasBinariesFolder(fileSystem.GetFullPath(build), fileSystem, out binariesPath)) {
                                return binariesPath;
                            }
                        }
                    }

                    string buildLog = files.FirstOrDefault(f => f.EndsWith("BuildLog.txt", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(buildLog)) {
                        if (CheckLog(fileSystem.GetFullPath(buildLog))) {
                            string binariesPath;
                            if (HasBinariesFolder(fileSystem.GetFullPath(build), fileSystem, out binariesPath)) {
                                return binariesPath;
                            }
                        }
                    }
                }
                throw new BuildNotFoundException($"No latest build found for {requirement.Name}. Considered directory '{requirementPath}'.");
            }

            return requirementPath;
        }

        private string HandleRequirementType(string resolverRequestDropPath, IDependencyRequirement requirement) {
            if (requirement.Name.IndexOf("Help", StringComparison.OrdinalIgnoreCase) >= 0) {
                return Path.Combine(resolverRequestDropPath, requirement.Name, "bin");
            }

            if (requirement.VersionRequirement.AssemblyVersion == null) {
                throw new ArgumentNullException(nameof(requirement.VersionRequirement.AssemblyVersion), string.Format(CultureInfo.InvariantCulture, "The requirement {0} does not have an assembly version specified.", requirement.Name));
            }

            return Path.Combine(resolverRequestDropPath, requirement.Name, requirement.VersionRequirement.AssemblyVersion); ;
        }

        private static bool HasBinariesFolder(string build, IFileSystem2 fileSystem, out string binariesFolder) {
            string binaries = Path.Combine(build, "Bin", "Module");

            if (fileSystem.DirectoryExists(binaries)) {
                // Guard against empty drop folders, if we run into one it will cause lots of runtime problems
                // due to missing binaries.
                if (fileSystem.GetFiles(binaries, "*", false).Any()) {
                    binariesFolder = binaries;
                    return true;
                }
            }
            binariesFolder = null;
            return false;
        }

        internal static string[] OrderBuildsByBuildNumber(string[] entries) {
            // Converts the dotted version into an int64 to get the highest build number
            // This differs from the PowerShell implementation that padded each part of the version string and used an alphanumeric sort

            List<KeyValuePair<Version, string>> numbers = new List<KeyValuePair<Version, string>>(entries.Length);
            foreach (var entry in entries) {
                string directoryName = Path.GetFileName(entry);
                Version version;
                if (Version.TryParse(directoryName, out version)) {
                    numbers.Add(new KeyValuePair<Version, string>(version, entry));
                }
            }

            return numbers.OrderByDescending(d => d.Key).Select(s => s.Value).ToArray();
        }

        internal static bool CheckLog(string logfile) {
            // UCS-2 Little Endian files sometimes get created which makes it difficult
            // to produce an efficient solution for reading a text file backwards
            IEnumerable<string> lineReader = File.ReadAllLines(logfile).Reverse().Take(10);

            int i = 0;
            foreach (string s in lineReader) {
                if (i > 10) {
                    break;
                }

                if (s.IndexOf("0 Error(s)", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }

                i++;
            }

            return false;
        }

        protected static string AdjustDropPathToBranch(string dropLocationDirectory, IDependencyRequirement module) {
            FolderBasedRequirement folderRequirement = module as FolderBasedRequirement;

            if (folderRequirement != null) {
                if (!string.IsNullOrEmpty(folderRequirement.Branch)) {
                    return PathHelper.ChangeBranch(dropLocationDirectory, folderRequirement.Branch);
                }
            }

            return dropLocationDirectory;
        }
    }
}