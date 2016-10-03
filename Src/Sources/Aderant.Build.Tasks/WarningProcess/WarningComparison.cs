using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class WarningComparison : Tuple<IEnumerable<WarningEntry>, IEnumerable<WarningEntry>> {
        public IEnumerable<WarningEntry> Source {
            get {
                return Item1;
            }
        }

        public IEnumerable<WarningEntry> Target {
            get {
                return Item2;
            }
        }

        public WarningComparison(IEnumerable<WarningEntry> item1, IEnumerable<WarningEntry> item2)
            : base(item1, item2) {
        }

        public IEnumerable<WarningEntry> GetDifference() {
            return Target.Except(Source);
        }

        public int GetAdjustedCount() {
            return Target.Where(t => !t.IsTransient).Count();
        }
    }
}