namespace Aderant.Build.MSBuild {
    /// <summary>
    /// Represents an MSBuild BuildStep element.
    /// </summary>
    public class BuildStep : Element {
        /// <summary>
        /// Initializes a new instance of the <see cref="BuildStep"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public BuildStep(string message) {
            Message = message;
            OutputBuildStepId = true;
            IsSucceededStep = false;
        }

        public BuildStep() {
            OutputBuildStepId = false;
            IsSucceededStep = true;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="BuildStep"/> will output a BuildStepId
        /// </summary>
        /// <value>
        ///   <c>true</c> if output; otherwise, <c>false</c>.
        /// </value>
        public bool OutputBuildStepId {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this BuildStep is a Succeeded message.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [output succeeded]; otherwise, <c>false</c>.
        /// </value>
        public bool IsSucceededStep {
            get;
            set;
        }

        public string Message {
            get;
            private set;
        }

        public override void Accept(BuildElementVisitor visitor) {
            visitor.Visit(this);
        }
    }
}
