using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Aderant.Build.Utilities {

    internal class RoslynLocator {
        private readonly string pathToBuildTools;

        private static readonly List<string> acceptedCodeAnalysisCSharpVersions = new List<string> {
            "3.5.0.0",  // VS 2019
            "3.4.0.0",  // VS 2019
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
            const string codeAnalysis = "Microsoft.CodeAnalysis.CSharp";
            const string collectionsImmutable = "System.Collections.Immutable";

            string name = args.Name.Split(',')[0];

            string[] paths = new [] {
                string.Empty,
                "Roslyn"
            };

            if (string.Equals(name, codeAnalysis)) {
                foreach (var path in paths) {
                    string file = Path.Combine(pathToBuildTools, path, name + ".dll");

                    if (File.Exists(file)) {
                        AssemblyName assemblyName = AssemblyName.GetAssemblyName(file);

                        if (acceptedCodeAnalysisCSharpVersions.Contains(assemblyName.Version.ToString()) || assemblyName.Version.Major >= 3) {
                            return Assembly.LoadFrom(file);
                        }
                    }
                }
            }

            if (string.Equals(name, collectionsImmutable)) {
                Assembly immutable = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(asm => asm.FullName.Split(',')[0] == collectionsImmutable);
                if (immutable != null) {
                    return immutable;
                }

                return Assembly.LoadFile($"{AppDomain.CurrentDomain.BaseDirectory}\\{collectionsImmutable}.dll");
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

            // Try and load each of the accepted versions of the Microsoft.CodeAnalysis.CSharp assembly.
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
