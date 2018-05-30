using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Aderant.Build.Logging;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Paket;

namespace Aderant.Build.Packaging {
    public sealed class Packager {
        private const string BuildInfrastructureWorkingDirectory = "_BUILD_";

        private readonly IFileSystem2 fs;
        private readonly ILogger logger;

        internal Packager(IFileSystem2 fs, ILogger logger) {
            this.fs = fs;
            this.logger = logger;
        }

        public PackResult Pack(string version) {
            var files = fs.GetFiles(fs.Root, "paket.dependencies", false);

            string dependenciesFilePath = null;
            int packedTemplates = 0;

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

                var lockFile = LockFile.LoadFrom(dependenciesFile.FindLockfile().FullName);

                var mainGroup = lockFile.GetGroupedResolution().Where(g => string.Equals(g.Key.Item1, Domain.GroupName(Constants.MainDependencyGroup)));
                var dependencyMap = mainGroup.ToDictionary(d => d.Key.Item2, d => d.Value.Version);

                ReplicateDependenciesToTemplate(dependencyMap, () => fs.OpenFileForWrite(fs.GetFullPath(file)));

                try {
                    logger.Info("Processing " + file);

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
                        projectUrl: FSharpOption<string>.None,
                        pinProjectReferences: true);

                    packedTemplates++;
                } catch (Exception ex) {
                    logger.LogErrorFromException(ex, true, true);
                    throw;
                }
            }

            logger.Info($"{packedTemplates} templates were processed into {spec.OutputPath}.");

            return new PackResult(spec);
        }

        internal IReadOnlyCollection<string> ReplicateDependenciesToTemplate(Dictionary<Domain.PackageName, SemVerInfo> dependencyMap, Func<Stream> templateFileStream) {
            PackageTemplateFile templateFile;

            using (var reader = new StreamReader(templateFileStream())) {
                templateFile = new PackageTemplateFile(reader.ReadToEnd());
            }

            foreach (var item in dependencyMap) {
                templateFile.AddDependency(item.Key);
            }

            templateFile.RemoveSelfReferences();

            if (templateFile.IsDirty) {
                templateFile.Save(templateFileStream());
            }

            return templateFile.Dependencies;
        }

        private FSharpOption<IEnumerable<string>> GenerateExcludedTemplates() {
            return null;
        }

        /// <summary>
        /// Search for all .paket.template files under all subfolders.
        /// </summary>
        /// <returns>A list containing all templates files being found</returns>
        internal IEnumerable<string> GetTemplateFiles() {
            var files = fs.GetFiles(fs.Root, "*paket.template", true);

            foreach (var file in files) {
                if (file.StartsWith(".git")) {
                    continue;
                }
                // Ignore files under the Build Infrastructure working directory, as it mat contain test resources 
                // which would erroneously be picked up
                if (file.IndexOf(BuildInfrastructureWorkingDirectory, StringComparison.OrdinalIgnoreCase) >= 0) {
                    continue;
                }

                if (file.IndexOf("packages\\", StringComparison.OrdinalIgnoreCase) >= 0) {
                    continue;
                }

                // Ignore files under the obj\ folder which may be created by the compiler
                if (file.IndexOf("obj\\", StringComparison.OrdinalIgnoreCase) >= 0) {
                    continue;
                }

                yield return file;
            }
        }

        public static string CreatePackageVersion(string versionJson) {
            dynamic o = JsonConvert.DeserializeObject<dynamic>(versionJson);

            string preReleaseLabel = o.PreReleaseLabel;
            string nugetVersion2 = o.NuGetVersionV2;

            return PackageVersion.CreateVersion(preReleaseLabel, nugetVersion2);
        }
    }
}