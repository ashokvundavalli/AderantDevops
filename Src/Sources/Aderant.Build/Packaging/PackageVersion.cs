using System;

namespace Aderant.Build.Packaging {
    internal static class PackageVersion {
        internal static string CreateVersion(string preReleaseLabel, string nugetVersion2) {
            if (!string.IsNullOrEmpty(preReleaseLabel)) {
                var pos = nugetVersion2.IndexOf(preReleaseLabel, StringComparison.Ordinal);

                if (pos >= 0) {
                    nugetVersion2 = nugetVersion2.Replace(preReleaseLabel, RemoveIllegalCharacters(preReleaseLabel));
                }
            }

            return nugetVersion2;
        }

        private static string RemoveIllegalCharacters(string text) {
            return text.Replace("-", string.Empty).Replace("_", String.Empty);
        }
    }
}