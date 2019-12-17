using System.IO;

namespace Aderant.Build.ProjectSystem.References {

    internal interface IUnresolvedBuildDependencyProjectReference : IBuildDependencyProjectReference, IUnresolvedReference {

        string ProjectPath { get; }

        /// <summary>
        /// The file name portion of <see cref="ProjectPath"/>.
        /// </summary>
        string ProjectFileName { get; }

    }
}
