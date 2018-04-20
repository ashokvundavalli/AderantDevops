using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild ItemGroup element.
    /// </summary>
    public class ItemGroup : MSBuildProjectElement, IEnumerable<ItemGroupItem> {
        public ItemGroup(string name, IEnumerable<string> items) {
            Name = name;
            Include = items.Select(s => new ItemGroupItem(s));
        }

        public ItemGroup(string name, IEnumerable<ItemGroupItem> items) {
            Name = name;
            Include = items.Where(i => i != null);
        }

        /// <summary>
        /// Gets the name of this ItemGroup.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the items to include.
        /// </summary>
        public IEnumerable<ItemGroupItem> Include { get; private set; }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }

        public IEnumerator<ItemGroupItem> GetEnumerator() {
            return Include.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)Include).GetEnumerator();
        }
    }
}