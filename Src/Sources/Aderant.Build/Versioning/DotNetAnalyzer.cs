using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Aderant.Build.Packaging;

namespace Aderant.Build.Versioning {
    internal class DotNetAnalyzer : IVersionAnalyzer<FileInfo> {
        private static string[] assemblyExtensions = new[] { ".dll", ".exe" };

        private AppDomain inspectionDomain;
        private readonly string assemblyLocation;

        public DotNetAnalyzer(string assemblyLocation) {
            this.assemblyLocation = assemblyLocation;
        }

        public FileVersionDescriptor GetVersion(FileInfo file) {
            try {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(file.FullName);

                AssemblyVersionInspector versionInspector = CreateInspector();

                versionInspector.Image = file.FullName;
                string assemblyVersion = versionInspector.GetAssemblyVersion();

                if (!string.IsNullOrEmpty(assemblyVersion)) {
                    return new FileVersionDescriptor(versionInfo, assemblyVersion);
                } else {
                    return new FileVersionDescriptor(versionInfo, null);
                }
            } finally {
                AppDomain.Unload(inspectionDomain);
            }
        }

        public bool CanAnalyze(FileInfo file) {
            foreach (string extension in assemblyExtensions) {
                if (file.Extension.EndsWith(extension, StringComparison.InvariantCultureIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        private AssemblyVersionInspector CreateInspector() {
            Assembly thisAssembly = GetType().Assembly;
            inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, assemblyLocation, null, false);
            AssemblyVersionInspector versionInspector = (AssemblyVersionInspector)inspectionDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, typeof(AssemblyVersionInspector).FullName);
            return versionInspector;
        }
    }
}