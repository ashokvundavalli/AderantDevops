using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    internal interface IAssemblyReference : IReference {
        string GetAssemblyName();
    }
}
