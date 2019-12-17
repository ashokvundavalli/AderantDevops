using System;
using System.Diagnostics;

namespace Aderant.Build.VersionControl.Model {
    [DebuggerDisplay("Branch:{" + nameof(Name) + "}")]
    public struct BranchName {
        public const string RefsHeads = "refs/heads/";

        public BranchName(string name) {
            Name = name;
        }

        public static BranchName CreateFromRef(string gitRef) {
            if (!gitRef.StartsWith(RefsHeads)) {
                throw new ArgumentException("Wrong gitRef format: " + gitRef);
            }

            return new BranchName(gitRef.Substring(RefsHeads.Length));
        }

        public string Name { get; set; }
        public string GitRef => RefsHeads + Name;

        public string SanitizedName => Sanitize(Name);

        public static string Sanitize(string name) {
            return name.Replace('/', '-');
        }

        public override string ToString() {
            return Name;
        }
    }
}
