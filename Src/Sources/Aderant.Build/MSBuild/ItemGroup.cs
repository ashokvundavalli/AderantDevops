using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild ItemGroup element.
    /// </summary>
    public class ItemGroup : MSBuildProjectElement, IEnumerable<ItemGroupItem> {
        private readonly IEnumerable<ItemGroupItem> include;

        public ItemGroup(string name, IEnumerable<string> items) {
            Name = name;
            include = items.Select(s => new ItemGroupItem(s));
        }

        public ItemGroup(string name, IEnumerable<ItemGroupItem> items) {
            Name = name;
            include = items.Where(i => i != null);
        }

        public ItemGroup(string name) {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of this ItemGroup.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the items to include.
        /// </summary>
        public IEnumerable<ItemGroupItem> Include {
            get { return include ?? Enumerable.Empty<ItemGroupItem>(); }
        }

        public string Condition { get; set; }

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
