using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks.TextTemplating {
    /// <summary>
    /// Attaches a one-time resolver for this domain to work around a multi-core build bug
    /// https://developercommunity.visualstudio.com/t/could-not-load-file-or-assembly-microsoftvisualstu-14/1411438?from=email
    /// </summary>
    public sealed class TextTemplatingAssemblyResolver : Task {
        static TextTemplatingAssemblyResolver() {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        public override bool Execute() {
            return true;
        }

        static readonly AssemblyName telemetryAssembly = new AssemblyName("Microsoft.VisualStudio.Telemetry, Version=16.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");

        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args) {
            if (string.Equals(args.Name, telemetryAssembly.FullName, StringComparison.Ordinal)) {
                var visualStudioInstances = Microsoft.Build.Locator.MSBuildLocator.QueryVisualStudioInstances();

                foreach (var instance in visualStudioInstances) {
                    if (instance.Version.Major == telemetryAssembly.Version.Major) {
                        var assemblyFile = Path.Combine(instance.VisualStudioRootPath, "MSBuild", "Microsoft", "VisualStudio", TextTemplatingPathResolver.CreateDottedMajorVersion(instance), "TextTemplating", $"{telemetryAssembly.Name}.dll");
                        if (File.Exists(assemblyFile)) {
                            var assemblyName = AssemblyName.GetAssemblyName(assemblyFile);

                            if (string.Equals(assemblyName.FullName, telemetryAssembly.FullName, StringComparison.Ordinal)) {
                                // We found it so this we no longer need to be attached.
                                AppDomain.CurrentDomain.AssemblyResolve -= AssemblyResolve;

                                return Assembly.LoadFrom(assemblyFile);
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}