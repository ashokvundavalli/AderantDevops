using System;
using System.Collections.Generic;
using Aderant.Build.Model;

namespace Aderant.Build {
    internal sealed class DependencyEqualityComparer : IEqualityComparer<IDependable> {
        private static IEqualityComparer<IDependable> defaultComparer;

        public static IEqualityComparer<IDependable> Default {
            get {
                var equalityComparer = defaultComparer;
                if (equalityComparer == null) {
                    equalityComparer = new DependencyEqualityComparer();
                    defaultComparer = equalityComparer;
                }
                return equalityComparer;
            }
        }

        public bool Equals(IDependable x, IDependable y) {
            return y != null && x != null && string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(IDependable obj) {
            return obj.Id.GetHashCode();
        }
    }
}
