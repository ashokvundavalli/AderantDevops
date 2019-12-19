using System;
using System.Diagnostics;

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

    }
}