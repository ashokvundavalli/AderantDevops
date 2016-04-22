using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace Aderant.Build.Providers {
    internal static class PathHelper {
        /// <summary>
        /// Gets the module dependencies directory.
        /// </summary>
        /// <param name="branchPath">The branch path.</param>
        /// <param name="expertModule">The expert module.</param>
        /// <returns></returns>
        public static string GetModuleDependenciesDirectory(string branchPath, ExpertModule expertModule) {
            if (!branchPath.EndsWith("Modules", StringComparison.OrdinalIgnoreCase)) {
                branchPath = Path.Combine(branchPath, "Modules");
            }

            return Path.Combine(branchPath, expertModule.Name, "Dependencies");
        }

        /// <summary>
        /// Gets the module output directory.
        /// </summary>
        /// <param name="branchPath">The branch path.</param>
        /// <param name="expertModule">The expert module.</param>
        /// <returns></returns>
        public static string GetModuleOutputDirectory(string branchPath, ExpertModule expertModule) {
            if (!branchPath.EndsWith("Modules", StringComparison.OrdinalIgnoreCase)) {
                branchPath = Path.Combine(branchPath, "Modules");
            }

            if (expertModule.ModuleType == ModuleType.ThirdParty) {
                return Path.Combine(branchPath, expertModule.Name, "Bin");
            }
            return Path.Combine(branchPath, expertModule.Name, "Bin", "Module");
        }

        public static string GetBranch(string path, bool throwOnNotFound) {
            return GetBranchInternal(path, false);
        }

        /// <summary>
        /// Gets two part branch name from a path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The two part branch name</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when name detection fails</exception>
        public static string GetBranch(string path) {
            return GetBranchInternal(path, true);
        }

        private static string GetBranchInternal(string path, bool throwOnNotFound) {
            string[] parts = path.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            string part1 = null;
            string part2 = null;

            for (int i = parts.Length - 1; i >= 0; i--) {
                if (parts[i].Equals("main", StringComparison.OrdinalIgnoreCase)) {
                    part1 = parts[i];

                    break;
                }

                if (i == parts.Length - 1) {
                    continue;
                }

                if (parts[i].Equals("dev", StringComparison.OrdinalIgnoreCase) || parts[i].Equals("releases", StringComparison.OrdinalIgnoreCase) || parts[i].Equals("automation", StringComparison.OrdinalIgnoreCase)) {
                    part1 = parts[i];
                    part2 = parts[i + 1];

                    break;
                }
            }

            if (part1 == null) {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD"))) {
                    if (throwOnNotFound) {
                        throw new InvalidOperationException("Unknown branch: " + path);
                    }
                }
                return path;
            }

            part1 = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part1);

            if (part2 != null) {
                part2 = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part2);
            }

            return Path.Combine(part1, part2 ?? string.Empty);
        }

        /// <summary>
        /// Gets the path to the module build order project file from the module root.
        /// </summary>
        /// <value>
        /// The path to module build.
        /// </value>
        public static string PathToBuildOrderProject {
            get { return @"Modules.proj"; }
        }

        /// <summary>
        /// Gets the path to the product manifest file from the module root.
        /// </summary>
        /// <value>
        /// The path to product manifest.
        /// </value>
        public static string PathToProductManifest {
            get { return @"Build.Infrastructure\Src\Package\ExpertManifest.xml"; }
        }

        /// <summary>
        /// Gets the source control path to module directory.
        /// </summary>
        /// <param name="branch">The branch.</param>
        /// <returns></returns>
        public static string GetServerPathToModuleDirectory(string branch) {
            string root = VersionControlPath.PrependRootIfNeeded("ExpertSuite");

            return Path.Combine(root, branch, "Modules");
        }

        /// <summary>
        /// Gets the name of the module from a given server path. 
        /// </summary>
        /// <param name="serverItem">The server item.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentOutOfRangeException">serverItem;Must start with $</exception>
        public static string GetModuleName(string serverItem) {
            if (serverItem.TrimStart('/').StartsWith("$")) {
                string substring = serverItem.Substring(serverItem.IndexOf("modules", StringComparison.OrdinalIgnoreCase));

                if (substring.Contains('/')) {
                    return substring.Split('/').Last();
                }
                return null;
            }
            throw new ArgumentOutOfRangeException("serverItem", "Must start with $");
        }

        public static string ChangeBranch(string dropLocationDirectory, string otherBranch) {
            string branch = GetBranch(dropLocationDirectory);

            int index = dropLocationDirectory.IndexOf(branch, StringComparison.OrdinalIgnoreCase);
            string substring = dropLocationDirectory.Substring(0, index);

            dropLocationDirectory = Path.Combine(substring, otherBranch);

            if (!Directory.Exists(dropLocationDirectory)) {
                throw new DirectoryNotFoundException("The path " + dropLocationDirectory + " does not exist");
            }

            return dropLocationDirectory;
        }


    }
}