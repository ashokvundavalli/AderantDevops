using System;

namespace Aderant.Build.ProjectSystem.References {
    internal interface IUnresolvedAssemblyReference : IAssemblyReference, IUnresolvedReference {
        string GetHintPath();
    }
}
