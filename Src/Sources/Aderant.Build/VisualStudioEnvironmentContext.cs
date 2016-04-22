using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Aderant.Build {
    public class VisualStudioEnvironmentContext {

        private static string[] visualStudioVersions = new string[] {
            @"%VS140COMNTOOLS%..\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer",
            @"%VS140COMNTOOLS%..\IDE\PrivateAssemblies",
            @"%VS120COMNTOOLS%..\IDE\PrivateAssemblies",
            @"%VS110COMNTOOLS%..\IDE\PrivateAssemblies", //C:\Program Files (x86)\Microsoft Visual Studio 11.0\Common7\IDE\PrivateAssemblies
        };

        private static ResolveEventHandler handler = new ResolveEventHandler(OnAssemblyResolve);

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.Name.IndexOf(".resources", StringComparison.Ordinal) >= 0) {
                return null;
            }

            if (args.Name.StartsWith("System", StringComparison.Ordinal)) {
                return null;
            }

            foreach (var visualStudioVersion in visualStudioVersions) {
                string path = Environment.ExpandEnvironmentVariables(visualStudioVersion);

                if (!string.IsNullOrEmpty(path)) {
                    if (Directory.Exists(path)) {
                        string assemblyFileName = args.Name.Split(',')[0];
                        assemblyFileName = assemblyFileName + ".dll";

                        assemblyFileName = Path.Combine(path, assemblyFileName);
                        if (File.Exists(assemblyFileName)) {
                            return Assembly.LoadFrom(assemblyFileName);
                        }
                    }
                }
            }

            return null;
        }
        public static void SetupContext() {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
            AppDomain.CurrentDomain.AssemblyResolve += handler;
        }

        public static void Shutdown() {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }
    }
}