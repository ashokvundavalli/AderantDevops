using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI;

namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild property list (a semi-colon separated list of properties)
    /// </summary>
    internal class PropertyList : Dictionary<string, string> {

        private static readonly string joinString = Separator + Environment.NewLine;

        public PropertyList(IDictionary<string, string> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase) {
        }

        public PropertyList() : base(StringComparer.OrdinalIgnoreCase) {
        }

        internal const string Separator = ";";

        private static string Join(IEnumerable<string> items) {
            return string.Join(joinString, items.Select(str => str));
        }

        public static string CreatePropertyListString(params string[] props) {
            return Join(props);
        }

        public static string CreatePropertyListString(IEnumerable<string> select) {
            return Join(select);
        }

        public override string ToString() {
            var combinedString = string.Join(joinString, this.Select(x => {
                if (x.Value.Contains(Separator)) {
                    return string.Concat(x.Key, "=", x.Value.Replace(Separator, "%3B"));
                } else {
                    return string.Concat(x.Key, "=", x.Value);
                }
            }));
            return combinedString;
        }

        public bool TryRemove(string key) {
            if (this.ContainsKey(key)) {
                return this.Remove(key);
            }

            return false;
        }

    }
}