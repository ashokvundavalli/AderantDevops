using System;
using System.Collections.Generic;

namespace Aderant.Build.Utilities {
    internal class PathComparer : IEqualityComparer<string> {

        /// <summary>Determines whether the specified objects are equal.</summary>
        public bool Equals(string x, string y) {
            if (x != null && y != null) {

                if (x.EndsWith(PathUtility.DirectorySeparator) && y.EndsWith(PathUtility.DirectorySeparator)) {
                    return PathsAreEqual(x, y);
                }

                x = x.TrimTrailingSlashes();
                y = y.TrimTrailingSlashes();

                return PathsAreEqual(x, y);
            }

            return PathsAreEqual(x, y);
        }

        /// <summary>Returns a hash code for the specified object.</summary>
        public int GetHashCode(string obj) {
            string s = obj;
            return StringComparer.OrdinalIgnoreCase.GetHashCode(s.TrimTrailingSlashes());
        }

        private static bool PathsAreEqual(string x, string y) {
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// A path comparer that ignores trailing directory separator characters
        /// </summary>
        public static IEqualityComparer<string> Default { get; } = new PathComparer();
    }
}
