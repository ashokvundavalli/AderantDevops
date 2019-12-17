using System.Collections.Generic;

namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild PropertyGroup element.
    /// </summary>
    public class PropertyGroup : MSBuildProjectElement {

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyGroup"/> class.
        /// </summary>
        /// <param name="props">The props.</param>
        public PropertyGroup(IDictionary<string, string> props) {
            Properties = new Dictionary<string, string>(props);
        }

        /// <summary>
        /// Gets the properties.
        /// </summary>
        public Dictionary<string, string> Properties {
            get;
            private set;
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}
