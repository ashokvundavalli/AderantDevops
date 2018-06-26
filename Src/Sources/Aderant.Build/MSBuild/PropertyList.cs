using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Aderant.Build.MSBuild {
    internal class PropertyList : Collection<string> {

        public override string ToString() {
            return string.Join(";" + Environment.NewLine, Items.Select(s => s));
        }
    }
}
