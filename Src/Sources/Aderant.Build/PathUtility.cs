using System;
using System.Globalization;
using System.IO;

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
        /// The path we need to make relative to basePath.  The path can be either absolute path or a relative path in which case it is relative to the base path.
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
    }
}
