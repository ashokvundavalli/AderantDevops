namespace Aderant.Build {
    internal class DependencySources {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencySources"/> class.
        /// </summary>
        /// <param name="dropPath">The drop path.</param>
        public DependencySources(string dropPath) {
            DropLocation = dropPath;
        }

        /// <summary>
        /// Gets or sets the drop location.
        /// </summary>
        /// <value>
        /// The drop location.
        /// </value>
        public string DropLocation { get; private set; }
    }
}