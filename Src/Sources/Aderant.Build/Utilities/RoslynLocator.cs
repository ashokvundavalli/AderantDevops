using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Aderant.Build.Utilities {

    internal class RoslynLocator {
        private readonly string pathToBuildTools;

        private static readonly List<string> acceptedCodeAnalysisCSharpVersions = new List<string> {
            "3.3.0.0",  // VS 2019
            "3.2.0.0",  // VS 2019
            "3.1.0.0",  // VS 2019
            "3.0.0.0",  // VS 2019
            "2.10.0.0", // VS 2017
            "1.3.1.0",  // VS 2015
            "1.2.0.0",  // VS 2015
            "1.0.0.0"
        };

        public RoslynLocator(string pathToBuildTools) {
            this.pathToBuildTools = pathToBuildTools;
        }

        private Assembly Resolve(object sender, ResolveEventArgs args) {
            var name = args.Name.Split(',')[0];

            var paths = new [] {
                "",
                "Roslyn"
            };

            if (name == "Microsoft.CodeAnalysis.CSharp") {
                foreach (var path in paths) {
                    string file = Path.Combine(pathToBuildTools, path, name + ".dll");

                    if (File.Exists(file)) {
                        AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);

                        if (acceptedCodeAnalysisCSharpVersions.Contains(assemblyName.Version.ToString())) {
                            return Assembly.LoadFrom(file);
                        }
                    }
                }
            }

            if (name == "System.Collections.Immutable") {
                var immutable = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Split(',')[0] == "System.Collections.Immutable");
                if (immutable != null) {
                    return immutable;
                }

                return Assembly.LoadFile($"{AppDomain.CurrentDomain.BaseDirectory}\\System.Collections.Immutable.dll");
            }

            return null;
        }

        public Assembly GetCodeAnalysisCSharpAssembly() {
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;

            try {
                return GetCodeAnalysisCSharpAssemblyInternal();
            } finally {
                AppDomain.CurrentDomain.AssemblyResolve -= Resolve;
            }
        }

        private static Assembly GetCodeAnalysisCSharpAssemblyInternal() {
            const string fullNameUnformatted = "Microsoft.CodeAnalysis.CSharp, Version={0}, Culture=neutral, PublicKeyToken=31bf3856ad364e35";

            //try load each of the accepted versions
            foreach (string version in acceptedCodeAnalysisCSharpVersions) {
                try {
                    return Assembly.Load(string.Format(fullNameUnformatted, version));
                } catch {
                    // Ignored.
                }
            }

            return null;
        }
    }
}
