namespace Aderant.Build.DependencyResolver {
    internal class DependencyFetchContext : IPackageContext {
        private readonly bool includeDevelopmentDependencies;

        public DependencyFetchContext()
            : this(true) {
        }

        public DependencyFetchContext(bool includeDevelopmentDependencies) {
            this.includeDevelopmentDependencies = includeDevelopmentDependencies;
        }

        public bool IncludeDevelopmentDependencies {
            get { return includeDevelopmentDependencies; }
        }

        public bool AllowExternalPackages {
            get { return false; }
        }
    }
}