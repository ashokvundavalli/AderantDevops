using System;
using System.Globalization;
using System.IO;

namespace Aderant.Build.Providers {
    internal static class PathHelper {

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