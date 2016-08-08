using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Paket;

namespace Aderant.Build.Packaging {
    public sealed class Packager {
        private const string BuildInfrastructureWorkingDirectory = "_BUILD_";

        private readonly IFileSystem2 fs;

        internal Packager(IFileSystem2 fs) {
            this.fs = fs;
        }

        public PackResult Pack(string version) {
            var files = fs.GetFiles(fs.Root, "paket.dependencies", false);

            string dependenciesFilePath = null;

            foreach (var file in files) {
                if (file.IndexOf(BuildInfrastructureWorkingDirectory, StringComparison.OrdinalIgnoreCase) >= 0) {
                    continue;
                }
                dependenciesFilePath = file;
                break;

            }

            if (dependenciesFilePath == null) {
                return null;
            }

            var spec = new PackSpecification {
                DependenciesFile = Path.Combine(fs.Root, dependenciesFilePath),
                OutputPath = Path.Combine(fs.Root, "Bin", "Packages")
            };

            foreach (var file in GetTemplateFiles()) {
                var dependenciesFile = DependenciesFile.ReadFromFile(spec.DependenciesFile);

                FSharpMap<Domain.PackageName, Paket.VersionRequirement> map = dependenciesFile.GetDependenciesInGroup(Paket.Constants.MainDependencyGroup);
                
                ReplicateDependenciesToTemplate(map.ToDictionary(d => d.Key, d => d.Value), () => fs.OpenFileForWrite(fs.GetFullPath(file)));

                PackageProcess.Pack(workingDir: fs.Root, 
                    dependenciesFile: dependenciesFile, 
                    packageOutputPath: spec.OutputPath, 
                    buildConfig: FSharpOption<string>.Some("Release"),
                    buildPlatform: FSharpOption<string>.Some("AnyCPU"),
                    version: FSharpOption<string>.Some(version), 
                    specificVersions: new List<Tuple<string, string>>(), 
                    releaseNotes: FSharpOption<string>.None, 
                    templateFile: FSharpOption<string>.Some(fs.GetFullPath(file)), 
                    excludedTemplates: GenerateExcludedTemplates(), 
                    lockDependencies: true, 
                    minimumFromLockFile: true, 
                    symbols: false, 
                    includeReferencedProjects: true, 
                    projectUrl: FSharpOption<string>.None);
            }

            return new PackResult(spec);
        }

        internal List<string> ReplicateDependenciesToTemplate(Dictionary<Domain.PackageName, Paket.VersionRequirement> dependencyMap, Func<Stream> templateFileStream) {
            PackageTemplateFile templateFile;

            using (var reader = new StreamReader(templateFileStream())) {
                templateFile = new PackageTemplateFile(reader.ReadToEnd());
            }

            foreach (var item in dependencyMap) {
                templateFile.AddDependency(item.Key, item.Value);
            }

            templateFile.Save(templateFileStream());

            return templateFile.Dependencies;

        }

        private FSharpOption<IEnumerable<string>> GenerateExcludedTemplates() {
            return null;
        }

        private IEnumerable<string> GetTemplateFiles() {
            var files = fs.GetFiles(fs.Root, "*paket.template", true);
            
            foreach (var file in files) {
                if (file.IndexOf(BuildInfrastructureWorkingDirectory, StringComparison.OrdinalIgnoreCase) >= 0) {
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
}