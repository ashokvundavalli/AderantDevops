using Aderant.Build.DependencyResolver.Model;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyGroup {
        /// <summary>
        /// The group that this requirement originates from.
        /// </summary>
        DependencyGroup DependencyGroup { get; set; }
    }
}