using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Aderant.Build.MSBuild {
    public class CallTarget : MSBuildProjectElement {
        public CallTarget(IEnumerable<string> targets) {
            Targets = targets.ToArray();
        }

        public string[] Targets { get; set; }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }

    /// <summary>
    /// Represents an MSBuild project Target element.
    /// </summary>
    [DebuggerDisplay("{Name}")]
    public class Target : MSBuildProjectElement {
        private string name;
        private IList<MSBuildProjectElement> elements = new List<MSBuildProjectElement>();

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
        public IEnumerable<MSBuildProjectElement> Elements {
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
        public void Add(MSBuildProjectElement element) {
            if (element is Target) {
                return;
            }
            elements.Add(element);
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}
