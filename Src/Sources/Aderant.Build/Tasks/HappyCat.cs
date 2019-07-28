using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class HappyCat : Task {

        [Output]
        public string PadLeft { get; set; }

        [Output]
        public string PadRight { get; set; }

        public int TotalNumberOfBuildGroups { get; set; }

        public int BuildGroupId { get; set; }

        public override bool Execute() {
            var d1 = (double)BuildGroupId / (double)TotalNumberOfBuildGroups;
            var d2 = d1 * 100;
            var d3 = (int)Math.Round(d2);
            var d4 = 100 - d3;

            PadLeft = new string(' ', d3);
            PadRight = new string(' ', d4);

            return true;
        }
    }
}