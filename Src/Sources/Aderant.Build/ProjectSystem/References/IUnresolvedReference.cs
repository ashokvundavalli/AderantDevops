using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {

    /// <summary>
    /// Represents an a reference to an unresolved compiler reference.
    /// </summary>
    public interface IUnresolvedReference : IUnresolvedDependency, IReference {
    }
}
