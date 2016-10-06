using Aderant.Build.DependencyResolver.Resolvers;

namespace Aderant.Build.DependencyResolver {
    internal class DependencySource {
        public DependencySource(string path, string type) {
            Path = path;
            Type = type;
        }

        public string Type { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Aderant.Build.DependencyResolver.DependencySource"/> class.
        /// </summary>
        /// <param name="dropPath">The drop path.</param>
        public DependencySource(string dropPath)
            : this(dropPath, ExpertModuleResolver.DropLocation) {
        }

        /// <summary>
        /// Gets or sets the dependency source location.
        /// </summary>
        /// <value>
        /// The drop location.
        /// </value>
        public string Path { get; private set; }
    }
}