using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyRequirement {
        string Name { get; }
        VersionRequirement VersionRequirement { get; }
    }
}