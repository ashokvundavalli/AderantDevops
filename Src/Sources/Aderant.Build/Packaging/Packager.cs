using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Paket;

namespace Aderant.Build.Packaging {
    public sealed class Packager {
        private readonly IFileSystem2 physicalFileSystem;

        private Packager(IFileSystem2 physicalFileSystem) {
            this.physicalFileSystem = physicalFileSystem;
        }

        public PackResult Pack(string version) {
            var files = physicalFileSystem.GetFiles(physicalFileSystem.Root, "paket.dependencies", false);

            var dependenciesFile = files.FirstOrDefault();

            if (dependenciesFile == null) {
                return null;
            }

            var spec = new PackSpecification {
                DependenciesFile = Path.Combine(physicalFileSystem.Root, dependenciesFile),
                OutputPath = Path.Combine(physicalFileSystem.Root, "Bin", "Packages")
            };

            foreach (var file in GetTemplateFiles()) {
                PackageProcess.Pack(physicalFileSystem.Root, DependenciesFile.ReadFromFile(spec.DependenciesFile), spec.OutputPath, FSharpOption<string>.None, FSharpOption<string>.None, FSharpOption<string>.Some(version), new List<Tuple<string, string>>(), FSharpOption<string>.None, FSharpOption<string>.Some(file), GenerateExcludedTemplates(), false, false, false, false, FSharpOption<string>.None);
            }

            return new PackResult(spec);
        }

        private FSharpOption<IEnumerable<string>> GenerateExcludedTemplates() {
            return null;
        }

        private IEnumerable<string> GetTemplateFiles() {
            var files = physicalFileSystem.GetFiles(physicalFileSystem.Root, "*paket.template", true);
            
            foreach (var file in files) {
                if (file.IndexOf("_BUILD_", StringComparison.OrdinalIgnoreCase) >= 0) {
                    continue;
                }
                yield return file;
            }
        }

        public static PackResult Package(string repository, string version) {
            var packager = new Packager(new PhysicalFileSystem(repository));
            return packager.Pack(version);
        }

        public static string CreatePackageVersion(string versionJson) {
            dynamic o = JsonConvert.DeserializeObject<dynamic>(versionJson);

            string preReleaseLabel = o.PreReleaseLabel;
            string nugetVersion2 = o.NuGetVersionV2;

           return PackageVersion.CreateVersion(preReleaseLabel, nugetVersion2);
        }
    }

    internal class PackageVersion {
        internal static string CreateVersion(string preReleaseLabel, string nugetVersion2) {
            if (!string.IsNullOrEmpty(preReleaseLabel)) {
                var i = char.ToLower(preReleaseLabel[0]);

                if (i > 'u') {
                    throw new InvalidPrereleaseLabel("The package name cannot start with any letter with a lexicographical order greater than 'u' to preserve NuGet prerelease sorting.");
                }

                var pos = nugetVersion2.IndexOf(preReleaseLabel, StringComparison.Ordinal);

                if (pos >= 0) {
                    nugetVersion2 = nugetVersion2.Replace(preReleaseLabel, RemoveIllegalCharacters(preReleaseLabel));
                }
            }

            return nugetVersion2;
        }

        private static string RemoveIllegalCharacters(string text) {
            return text.Replace("-", string.Empty).Replace("_", String.Empty);
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