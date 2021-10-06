using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.ProjectSystem.StateTracking {
    internal static class SortStateFileExtensions {

        /// <summary>
        /// A sort to implement the domain rule that newer state files are preferred over older ones
        /// </summary>
        public static IEnumerable<BuildStateFile> SortStateFiles(this IEnumerable<BuildStateFile> items) {
            return items.OrderByDescending(s => s.BuildId);
        }

        /// <summary>
        /// A sort to implement the domain rule that newer state files are preferred over older ones
        /// </summary>
        public static IOrderedEnumerable<BuildStateFile> SortStateFiles(this IOrderedEnumerable<BuildStateFile> items) {
            return items.ThenByDescending(s => s.BuildId);
        }
    }
}