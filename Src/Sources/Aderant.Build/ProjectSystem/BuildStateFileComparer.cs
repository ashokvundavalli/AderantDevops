using System.Collections.Generic;
using System.Windows.Navigation;
using Aderant.Build.ProjectSystem.StateTracking;

namespace Aderant.Build.ProjectSystem {
    /// <summary>
    /// A comparer to implement the domain rule that newer state files are preferred over older ones
    /// </summary>
    /// <remarks>
    /// A more recent build does not necessarily mean the content is correct or newer
    /// </remarks>
    internal class BuildStateFileComparer : IComparer<BuildStateFile> {

        public int Compare(BuildStateFile x, BuildStateFile y) {
            if (ReferenceEquals(x, y)) {
                return 0;
            }

            if (ReferenceEquals(null, y)) {
                return 1;
            }

            if (ReferenceEquals(null, x)) {
                return -1;
            }

            // y -> x so to ensure descending sort order
            return y.BuildId.CompareTo(x.BuildId);
        }

        public static IComparer<BuildStateFile> Default { get; } = new BuildStateFileComparer();

        public static int S(BuildStateFile arg) {
            return arg.BuildId;
        }
    }
}