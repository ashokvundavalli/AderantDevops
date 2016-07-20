using Aderant.Build.Packaging;

namespace Aderant.Build.Versioning {
    internal interface IVersionAnalyzer<in T> : IVersionAnalyzer {
        /// <summary>
        /// Gets the version of a given file.
        /// </summary>
        /// <param name="file">The file.</param>
        FileVersionDescriptor GetVersion(T file);
    }
}