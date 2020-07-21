using System;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <remarks>
    /// The built in Microsoft.TextTemplating.Build.Tasks.TransformTemplate task fails to load
    /// the VS telemetry assembly under the VS 2019 build engine so we need to drag this assembly in
    /// ourselves.
    /// </remarks>
    public sealed class TextTemplatingAssemblyHelper : Task {
        const string buildTasks = "Microsoft.TextTemplating.Build.Tasks";

        const string visualStudioTelemetry = "Microsoft.VisualStudio.Telemetry";

        static TextTemplatingAssemblyHelper() {
            System.AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            System.AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) {
            if (args.LoadedAssembly.GetName().Name == buildTasks) {
                if (LoadTelemetry(args.LoadedAssembly, visualStudioTelemetry) != null) {
                    // Resolution done - we no longer need this hook
                    System.AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                }
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            var assemblyNameToResolve = new AssemblyName(args.Name);

            if (assemblyNameToResolve.Name == visualStudioTelemetry) {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(s => s.GetName().Name == buildTasks)
                    .ToList();

                foreach (var assembly in assemblies) {
                    var loadAssembly = LoadTelemetry(assembly, assemblyNameToResolve.Name);
                    if (loadAssembly != null) {

                        // Resolution done - we no longer need this hook
                        System.AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
                        return loadAssembly;
                    }
                }
            }

            return null;
        }

        private static Assembly LoadTelemetry(Assembly assembly, string assemblyFileNameToLoad) {
            if (!string.IsNullOrEmpty(assembly.Location)) {
                var directoryOfAssembly = System.IO.Path.GetDirectoryName(assembly.Location);

                var fileToFind = System.IO.Path.Combine(directoryOfAssembly, assemblyFileNameToLoad + ".dll");

                if (System.IO.File.Exists(fileToFind)) {
                    return Assembly.LoadFrom(fileToFind);
                }
            }

            return null;
        }

        public override bool Execute() {
            return !Log.HasLoggedErrors;
        }
    }
}