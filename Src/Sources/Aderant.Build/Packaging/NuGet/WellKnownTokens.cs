using System.Collections.Generic;
using Aderant.Build.Logging;

namespace Aderant.Build.Tasks {
    internal class WellKnownTokens {
        private static string id = "$id$";

        public static string Id {
            get { return id; }
        }
    }

    internal class PackageDifference {
        public PackageDifference(IFileSystem2 fileSystem, ILogger logger) {
        }

        public void GetChanges(IEnumerable<string> currentContents, IEnumerable<string> existingContents) {
        }
    }
}