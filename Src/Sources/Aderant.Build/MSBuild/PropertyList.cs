using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.MSBuild {
    internal class PropertyList : Dictionary<string, string> {
        private static readonly string joinString = "; " + Environment.NewLine;

        public PropertyList() : base(StringComparer.OrdinalIgnoreCase) {
        }

        public override string ToString() {
            return string.Join(joinString, this.Select(x => string.Concat(x.Key, "=\"", x.Value, "\"")));
        }

        private static string Join(IList<string> items) {
            return string.Join(joinString, items.Select(str => str));
        }

        public static string CreatePropertyString(params string[] props) {
            return Join(props);
        }

        public bool TryRemove(string key) {
            if (this.ContainsKey(key)) {
                return this.Remove(key);
            }

            return false;
        }
    }
}
