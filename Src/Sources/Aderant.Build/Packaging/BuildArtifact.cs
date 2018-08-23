using System;
using System.IO;
using System.Runtime.Serialization;
using Aderant.Build.TeamFoundation;
using ProtoBuf;

namespace Aderant.Build.Packaging {

    [DataContract]
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    internal class BuildArtifact {

        [DataMember(Order = 1)]
        public string FullPath { get; set; }

        [DataMember(Order = 2)]
        public string Name { get; set; }

        [DataMember(Order = 3)]
        public VsoBuildArtifactType Type { get; set; }

        public string ComputeVsoPath() {
            var pos = FullPath.IndexOf(Name, StringComparison.OrdinalIgnoreCase);
            return FullPath.Remove(pos)
                .TrimEnd(Path.DirectorySeparatorChar)
                .TrimEnd(Path.AltDirectorySeparatorChar);
        }
    }
}
