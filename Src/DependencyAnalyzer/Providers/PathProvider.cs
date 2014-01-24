using System;
using System.IO;
using System.Linq;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace DependencyAnalyzer.Providers {

    public static class PathHelper {
        /// <summary>
        /// Combines an arbitrary number of paths.
        /// </summary>
        /// <param name="path1">The path1.</param>
        /// <param name="paths">The paths.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">
        /// path1
        /// or
        /// paths
        /// </exception>
        public static string Aggregate(string path1, params string[] paths) {
            if (path1 == null) {
                throw new ArgumentNullException("path1");
            }
            if (paths == null) {
                throw new ArgumentNullException("paths");
            }
            return paths.Aggregate(path1, Path.Combine);
        }

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

            return Aggregate(branchPath, expertModule.Name, "Dependencies");
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
                return Aggregate(branchPath, expertModule.Name, "Bin");
            }
            return Aggregate(branchPath, expertModule.Name, "Bin", "Module");
        }

        /// <summary>
        /// Gets two part branch name from a path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The two part branch name</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when name detection fails</exception>
        public static string GetBranch(string path) {
            var parts = path.Split(new char[] {Path.DirectorySeparatorChar}, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Count(); i++) {
                
                if (parts[i].Equals("dev", StringComparison.OrdinalIgnoreCase)) {
                    return Path.Combine(parts[i], parts[i + 1]);
                }
                if (parts[i].Equals("releases", StringComparison.OrdinalIgnoreCase)) {
                    return Path.Combine(parts[i], parts[i + 1]);
                }
                if (parts[i].Equals("main", StringComparison.OrdinalIgnoreCase)) {
                    return parts[i];
                }
            }

            throw new InvalidOperationException("Unknown branch: " + path);
        }

        /// <summary>
        /// Gets the path to the module build project file from the module root.
        /// </summary>
        /// <value>
        /// The path to module build.
        /// </value>
        public static string PathToModuleBuild {
            get {
                return @"Build.Infrastructure\Src\Build\ModuleBuild.proj";
            }
        }

        /// <summary>
        /// Gets the path to the module build order project file from the module root.
        /// </summary>
        /// <value>
        /// The path to module build.
        /// </value>
        public static string PathToBuildOrderProject {
            get {
                return @"Modules.proj";
            }
        }

        /// <summary>
        /// Gets the path to the product manifest file from the module root.
        /// </summary>
        /// <value>
        /// The path to product manifest.
        /// </value>
        public static string PathToProductManifest {
            get {
                return @"Build.Infrastructure\Src\Package\ExpertManifest.xml";
            }
        }

        /// <summary>
        /// Gets the source control path to module directory.
        /// </summary>
        /// <param name="branch">The branch.</param>
        /// <returns></returns>
        public static string GetServerPathToModuleDirectory(string branch) {
            string root = VersionControlPath.PrependRootIfNeeded("ExpertSuite");

            return Combine(root, branch, "Modules");
        }

        /// <summary>
        /// Combines the specified paths as required for Team Foundation
        /// </summary>
        /// <param name="path1">The path1.</param>
        /// <param name="paths">The paths.</param>
        /// <returns></returns>
        internal static string Combine(string path1, params string[] paths) {
            return paths.Aggregate(path1, VersionControlPath.Combine);
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
    }
}