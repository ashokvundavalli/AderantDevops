using System.Collections.Generic;
using Aderant.Build.Versioning;

namespace Aderant.Build.Packaging {
    internal class PackageSpecification {
        private readonly SemanticVersion packageVersion;
        private readonly List<PhysicalPackageFile> files;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageSpecification"/> class.
        /// </summary>
        /// <param name="packageVersion">The package version.</param>
        /// <param name="files">The files.</param>
        public PackageSpecification(SemanticVersion packageVersion, List<PhysicalPackageFile> files) {
            this.packageVersion = packageVersion;
            this.files = files;
        }

        public IEnumerable<IPackageFile> Files {
            get { return files; }
        }
    }
}