using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Aderant.Build.MSBuild {
    internal class PropertyList : Collection<string> {
        private static string joinString = "; " + Environment.NewLine;

        public override string ToString() {
            return Join(Items);
        }

        private static string Join(IList<string> items) {
            return string.Join(joinString, items.Select(str => str));
        }

        public static string CreatePropertyString(params string[] props) {
            return Join(props);
        }
    }
}
