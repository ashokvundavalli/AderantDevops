using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Management.Automation;

namespace Aderant.Build {
    public static class PathUtility {

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// If the path is an empty string, does not modify it.
        /// </summary>
        /// <param name="fileSpec">The path to check.</param>
        /// <returns>A path with a slash.</returns>
        internal static string EnsureTrailingSlash(string fileSpec) {
            fileSpec = FixFilePath(fileSpec);
            if (fileSpec.Length > 0 && !EndsWithSlash(fileSpec)) {
                fileSpec += Path.DirectorySeparatorChar;
            }

            return fileSpec;
        }

        internal static bool HasExtension(string fileName, string[] allowedExtensions) {
            string extension = Path.GetExtension(fileName);
            for (int i = 0; i < allowedExtensions.Length; i++) {
                string strB = allowedExtensions[i];
                if (string.Compare(extension, strB, true, CultureInfo.CurrentCulture) == 0) {
                    return true;
                }
            }

            return false;
        }

        internal static string FixFilePath(string path) {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');
        }

        public static string Quote(this string text) {
            return SurroundWith(text, "\"");
        }

        public static string SurroundWith(this string text, string ends) {
            return ends + text + ends;
        }

        /// <summary>
        /// Given the absolute location of a file, and a disc location, returns relative file path to that disk location.
        /// Throws UriFormatException.
        /// </summary>
        /// <param name="basePath">
        /// The base path we want to be relative to. Must be absolute.
        /// Should <i>not</i> include a filename as the last segment will be interpreted as a directory.
        /// </param>
        /// <param name="path">
        /// The path we need to make relative to basePath.  The path can be either absolute path or a relative path in which case
        /// it is relative to the base path.
        /// If the path cannot be made relative to the base path (for example, it is on another drive), it is returned verbatim.
        /// If the basePath is an empty string, returns the path.
        /// </param>
        /// <returns>relative path (can be the full path)</returns>
        public static string MakeRelative(string basePath, string path) {
            if (basePath.Length == 0) {
                return path;
            }

            Uri baseUri = new Uri(EnsureTrailingSlash(basePath), UriKind.Absolute); // May throw UriFormatException

            Uri pathUri = CreateUriFromPath(path);

            if (!pathUri.IsAbsoluteUri) {
                // the path is already a relative url, we will just normalize it...
                pathUri = new Uri(baseUri, pathUri);
            }

            Uri relativeUri = baseUri.MakeRelativeUri(pathUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.IsAbsoluteUri ? relativeUri.LocalPath : relativeUri.ToString());

            string result = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return result;
        }

        private static Uri CreateUriFromPath(string path) {
            Uri result = null;
            if (!Uri.TryCreate(path, UriKind.Absolute, out result)) {
                result = new Uri(path, UriKind.Relative);
            }

            return result;
        }

        /// <summary>
        /// Indicates if the given file-spec ends with a slash.
        /// </summary>
        /// <param name="fileSpec">The file spec.</param>
        /// <returns>true, if file-spec has trailing slash</returns>
        internal static bool EndsWithSlash(string fileSpec) {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        internal static bool IsSlash(char c) {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }

        /// <summary>
        /// Gets the resolved path if the input is rooted
        /// </summary>
        public static string GetFullPath(string path) {
            ErrorUtilities.IsNotNull(path, nameof(path));

            if (Path.IsPathRooted(path) && (path[0] != Path.DirectorySeparatorChar && path[0] != Path.AltDirectorySeparatorChar)) {
                return Path.GetFullPath(path);
            }

            return path;
        }

        /// <summary>
        /// Normalizes the path segment
        /// - Fixes double trailing slashes
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string NormalizeTrailingSlashes(this string path) {
            if (path != null && path.EndsWith(@"\\")) {
                //logger.Warning($"! Project {project.ProjectFile} output path ends with two path separators: '{projectOutputPath}'. Normalize this path.");
                // Normalize path as sometimes it ends with two slashes
                return path.Replace(@"\\", @"\");
            }

            return path;
        }

        /// <summary>
        /// Trims trailing path separators
        /// </summary>
        public static string TrimTrailingSlashes(this string path) {
            return path.TrimEnd(Path.DirectorySeparatorChar).TrimEnd(Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Path.GetFileName returns "" when given a path ending with a trailing slash
        /// </summary>
        public static string GetFileName(string fullPath) {
            return Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Test if the provided path is excluded by a set of filter patterns.
        /// Wildcards supported.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="excludeFilterPatterns"></param>
        /// <returns></returns>
        public static bool IsPathExcludedByFilters(string path, IReadOnlyList<string> excludeFilterPatterns) {
            if (excludeFilterPatterns != null) {
                for (var i = 0; i < excludeFilterPatterns.Count; i++) {
                    var pattern = excludeFilterPatterns[i];
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