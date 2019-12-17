using Aderant.Build.DependencyResolver.Models;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyGroup {

        // The group that this requirement originates from.
        DependencyGroup DependencyGroup { get; set; }
    }
}