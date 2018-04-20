using System;
using System.IO;

namespace Aderant.Build {
    public static class PathUtility {
        public static string GetPathWithForwardSlashes(string path) {
            return path.Replace('\\', '/');
        }

        public static string EnsureTrailingSlash(string path) {
            return EnsureTrailingCharacter(path, Path.DirectorySeparatorChar);
        }

        private static string EnsureTrailingCharacter(string path, char trailingCharacter) {
            if (path == null) {
                throw new ArgumentNullException(nameof(path));
            }

            // if the path is empty, we want to return the original string instead of a single trailing character.
            if (path.Length == 0
                || path[path.Length - 1] == trailingCharacter) {
                return path;
            }

            return path + trailingCharacter;
        }

        public static string Quote(this string text) {
            return SurroundWith(text, "\"");
        }

        public static string SurroundWith(this string text, string ends) {
            return ends + text + ends;
        }

        public static string MakeRelative(string basePath, string path) {
            if (basePath.Length == 0) {
                return path;
            }
            Uri uri = new Uri(EnsureTrailingSlash(basePath), UriKind.Absolute);
            Uri uri2 = CreateUriFromPath(path);
            if (!uri2.IsAbsoluteUri) {
                uri2 = new Uri(uri, uri2);
            }
            Uri uri3 = uri.MakeRelativeUri(uri2);
            string text = Uri.UnescapeDataString(uri3.IsAbsoluteUri ? uri3.LocalPath : uri3.ToString());
            return text.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        private static Uri CreateUriFromPath(string path) {
            Uri result = null;
            if (!Uri.TryCreate(path, UriKind.Absolute, out result)) {
                result = new Uri(path, UriKind.Relative);
            }
            return result;
        }

        internal static bool EndsWithSlash(string fileSpec) {
            return fileSpec.Length > 0 && IsSlash(fileSpec[fileSpec.Length - 1]);
        }

        internal static bool IsSlash(char c) {
            return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
        }
    }
}