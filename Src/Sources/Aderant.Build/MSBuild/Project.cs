using System.Collections.Generic;

namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild project.
    /// </summary>
    public class Project : MSBuildProjectElement {

        private HashSet<MSBuildProjectElement> elements = new HashSet<MSBuildProjectElement>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class.
        /// </summary>
        public Project() {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class.
        /// </summary>
        /// <param name="projectElements">The project elements.</param>
        public Project(IEnumerable<MSBuildProjectElement> projectElements) {
            Add(projectElements);
        }

        /// <summary>
        /// Gets the elements belonging to this project.
        /// </summary>
        public IEnumerable<MSBuildProjectElement> Elements {
            get {
                return elements;
            }
        }

        private Target defaultTarget;

        /// <summary>
        /// Gets or sets the default target for this project.
        /// </summary>
        /// <value>
        /// The default target.
        /// </value>
        public Target DefaultTarget {
            get {
                return defaultTarget;
            }
            set {
                elements.Add(value);
                defaultTarget = value;
            }
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }

        /// <summary>
        /// Adds the specified elements to this project. 
        /// The element position is not deterministic.
        /// </summary>
        /// <param name="elements">The elements.</param>
        public void Add(IEnumerable<MSBuildProjectElement> elements) {
            foreach (MSBuildProjectElement element in elements) {
                Add(element);
            }
        }

        /// <summary>
        /// Adds a single element to the project.
        /// </summary>
        /// <param name="element">The element.</param>
        public void Add(MSBuildProjectElement element) {
            elements.Add(element);
        }
    }
}