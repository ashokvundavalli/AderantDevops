using System;
using System.IO;

namespace Aderant.Build.Packaging {
    internal class ArtifactStorageInfo {
        public string FullPath { get; set; }
        public string Name { get; set; }

        public string ComputeVsoPath() {
            var pos = FullPath.IndexOf(Name, StringComparison.OrdinalIgnoreCase);
            return FullPath.Remove(pos, Name.Length)
                .TrimEnd(Path.DirectorySeparatorChar)
                .TrimEnd(Path.AltDirectorySeparatorChar);
        }
    }
}
