using System;
using System.Diagnostics;
using System.Linq;
using Aderant.Build.Versioning;

namespace Aderant.Build.Packaging {
    public class FileVersionDescriptor {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileVersionDescriptor"/> class.
        /// </summary>
        /// <param name="versionInfo">The version information.</param>
        /// <param name="assemblyVersion">The assembly version.</param>
        public FileVersionDescriptor(FileVersionInfo versionInfo, string assemblyVersion) : this(versionInfo.FileVersion, assemblyVersion) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileVersionDescriptor"/> class.
        /// </summary>
        /// <param name="fileVersion">The file version.</param>
        /// <param name="assemblyVersion">The assembly version.</param>
        public FileVersionDescriptor(string fileVersion, string assemblyVersion) {
            this.FileVersion = fileVersion;

            if (!string.IsNullOrEmpty(assemblyVersion)) {
                this.AssemblyVersion = new Version(assemblyVersion);
            }
        }

        public string FileVersion { get; set; }

        public Version AssemblyVersion { get; private set; }

        public SemanticVersion GetSemanticVersion() {
            string fileVersion = null;
            if (!string.IsNullOrEmpty(FileVersion)) {
                fileVersion = CanonicalizeFileVersion();
            }

            if (AssemblyVersion != null) {
                Version parsedVersion;
                if (Version.TryParse(fileVersion, out parsedVersion)) {
                    Version highestVersion = new Version[] {parsedVersion, AssemblyVersion}.Max();

                    return new SemanticVersion(highestVersion);
                } else {
                    return new SemanticVersion(AssemblyVersion);
                }
            }

            if (!string.IsNullOrEmpty(fileVersion)) {
                Version parsedVersion;
                if (Version.TryParse(fileVersion, out parsedVersion)) {
                    return new SemanticVersion(parsedVersion);
                }
            }

            return null;
        }

        private string CanonicalizeFileVersion() {
            var canonicalizer = new VersionParser();
            return canonicalizer.ParseVersion(FileVersion);
        }
    }
}