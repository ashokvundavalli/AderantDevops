using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Paket;

namespace Aderant.Build.Packaging {
    public sealed class Packager {
        private readonly IFileSystem2 physicalFileSystem;

        private Packager(IFileSystem2 physicalFileSystem) {
            this.physicalFileSystem = physicalFileSystem;
        }

        public PackResult Pack() {
            //System.Diagnostics.Debugger.Launch();

            var files = physicalFileSystem.GetFiles(physicalFileSystem.Root, "paket.dependencies", false);

            var dependenciesFile = files.FirstOrDefault();

            if (dependenciesFile == null) {
                return null;
            }

            var spec = new PackSpecification {
                DependenciesFile = dependenciesFile,
                OutputPath = Path.Combine(physicalFileSystem.Root, "Bin", "Packages")
            };

            Paket.PackageProcess.Pack<string[]>(physicalFileSystem.Root, DependenciesFile.ReadFromFile(spec.DependenciesFile), spec.OutputPath, FSharpOption<string>.None, FSharpOption<string>.None, FSharpOption<string>.None, new List<Tuple<string, string>>(), FSharpOption<string>.None, FSharpOption<string>.None, null, false, false, false, false, FSharpOption<string>.None);

            return new PackResult(spec);
        }

        public static PackResult Package(string repository) {
            var packager = new Packager(new PhysicalFileSystem(repository));
            return packager.Pack();

        }

    }

    public sealed class PackResult {
        private readonly PackSpecification spec;

        internal PackResult(PackSpecification spec) {
            this.spec = spec;
        }

        public string OutputPath {
            get { return spec.OutputPath; }
        }

    }

    internal class PackSpecification {
        public string DependenciesFile { get; set; }
        public string OutputPath { get; set; }
    }
}
