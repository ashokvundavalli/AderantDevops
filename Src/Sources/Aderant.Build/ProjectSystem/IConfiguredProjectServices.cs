using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.ProjectSystem {
    internal interface IConfiguredProjectServices {
        IAssemblyReferencesService AssemblyReferences { get; }

        IBuildDependencyProjectReferencesService ProjectReferences { get; }

        ITextTemplateReferencesServices TextTemplateReferences { get; }
    }
}
