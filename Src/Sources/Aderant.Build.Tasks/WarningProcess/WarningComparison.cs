using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class WarningComparison : Tuple<IEnumerable<WarningEntry>, IEnumerable<WarningEntry>> {
        /// <summary>
        /// Gets the source or base line.
        /// </summary>
        public IEnumerable<WarningEntry> Source {
            get {
                return Item1;
            }
        }

        /// <summary>
        /// Gets the target. The newer of the items.
        /// </summary>
        public IEnumerable<WarningEntry> Target {
            get {
                return Item2;
            }
        }

        public WarningComparison(IEnumerable<WarningEntry> @base, IEnumerable<WarningEntry> newer)
            : base(@base, newer) {
        }

        public IEnumerable<WarningEntry> GetDifference() {
            return Target.Except(Source);
        }

        public int GetAdjustedCount() {
            return Target.Count(t => t.AffectsProjectQuality);
        }
    }
}