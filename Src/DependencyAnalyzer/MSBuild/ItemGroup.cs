using System.Collections.Generic;

namespace DependencyAnalyzer.MSBuild {
    /// <summary>
    /// Represents an MSBuild ItemGroup element.
    /// </summary>
    public class ItemGroup : Element {

        public ItemGroup(string name, IEnumerable<string> items) {
            Name = name;
            Include = items;
        }

        /// <summary>
        /// Gets the name of this ItemGroup.
        /// </summary>
        public string Name {
            get;
            private set;
        }

        /// <summary>
        /// Gets the items to include.
        /// </summary>
        public IEnumerable<string> Include {
            get;
            private set;
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}