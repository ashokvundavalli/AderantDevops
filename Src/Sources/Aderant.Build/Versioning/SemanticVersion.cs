using System;
using System.Globalization;

namespace Aderant.Build.Versioning {
    public class SemanticVersion {
        /// <summary>
        /// Determines if the version string is a prerelease version.
        /// </summary>
        public static bool IsPreRelease(string version) {
            return version != null && version.IndexOf('-') >= 0;
        }
    }
}