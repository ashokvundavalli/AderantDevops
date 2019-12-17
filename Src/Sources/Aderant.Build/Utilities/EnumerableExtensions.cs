using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Utilities {
    internal static class EnumerableExtensions {

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable) {
            if (enumerable != null) {
                return !enumerable.Any();
            }

            return true;
        }
    }
}