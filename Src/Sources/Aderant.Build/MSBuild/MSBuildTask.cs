namespace Aderant.Build.MSBuild {
    public class MSBuildTask : Element {

        /// <summary>
        /// Initializes a new instance of the <see cref="MSBuildTask"/> class.
        /// </summary>
        public MSBuildTask() {
            Properties = string.Empty;
            Projects = string.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to set the BuildInParallel flag.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [build in parallel]; otherwise, <c>false</c>.
        /// </value>
        public bool BuildInParallel {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the projects property string. Ensure that this string is formatted with the correct metadata selector.
        /// </summary>
        /// <value>
        /// The projects.
        /// </value>
        public string Projects {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the global properties to pass to the projects.
        /// </summary>
        /// <value>
        /// The properties.
        /// </value>
        public string Properties {
            get;
            set;
        }

        public string Targets { get; set; }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}