﻿using System;
using System.IO;

namespace Aderant.Build.Packaging {
    internal class BuildArtifact {
        public string FullPath { get; set; }
        public string Name { get; set; }

        public string ComputeVsoPath() {
            var pos = FullPath.IndexOf(Name, StringComparison.OrdinalIgnoreCase);
            return FullPath.Remove(pos)
                .TrimEnd(Path.DirectorySeparatorChar)
                .TrimEnd(Path.AltDirectorySeparatorChar);
        }
    }
}
