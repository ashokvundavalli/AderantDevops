using System.Collections.Generic;
using System.Diagnostics;

namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild project Target element.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class Target : Element {
        private string name;
        private IList<Element> elements = new List<Element>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Target"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public Target(string name) {
            this.name = name;

            DependsOnTargets = new List<Target>();
            BeforeTargets = new List<Target>();
            AfterTargets = new List<Target>();
        }

        /// <summary>
        /// Gets the name of this element.
        /// </summary>
        public string Name {
            get {
                return name;
            }
        }

        /// <summary>
        /// Gets the elements this target owns.
        /// </summary>
        public IEnumerable<Element> Elements {
            get {
                return elements;
            }
        }

        /// <summary>
        /// Gets or sets the depends on targets list.
        /// </summary>
        /// <value>
        /// The depends on targets.
        /// </value>
        public ICollection<Target> DependsOnTargets {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the before targets.
        /// </summary>
        /// <value>
        /// The before targets.
        /// </value>
        public ICollection<Target> BeforeTargets {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the after targets.
        /// Think of this as the RunAfterTargets list.
        /// </summary>
        /// <value>
        /// The after targets.
        /// </value>
        public ICollection<Target> AfterTargets {
            get;
            set;
        }

        /// <summary>
        /// Adds the specified element to this target.
        /// </summary>
        /// <param name="element">The project.</param>
        public void Add(Element element) {
            elements.Add(element);
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}