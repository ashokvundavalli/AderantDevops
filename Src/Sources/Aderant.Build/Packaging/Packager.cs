using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using Aderant.Build.Logging;
using Microsoft.FSharp.Collections;
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

                FSharpMap<Domain.PackageName, Paket.VersionRequirement> map = dependenciesFile.GetDependenciesInGroup(Paket.Constants.MainDependencyGroup);
                
                ReplicateDependenciesToTemplate(map.ToDictionary(d => d.Key, d => d.Value), () => fs.OpenFileForWrite(fs.GetFullPath(file)));

                //logger.Info("file="+ file + ",dep=" + dependenciesFile);

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
                                    projectUrl: FSharpOption<string>.None);

                    packedTemplates ++;

                } catch (Exception ex) {
                    logger.Error(ex.Message);
                }
            }

            logger.Info($"{packedTemplates} templates were processed into {spec.OutputPath}.");

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

        /// <summary>
        /// Search for all .paket.template files under all subfolders.
        /// </summary>
        /// <returns>A list containing all templates files being found</returns>
        private IEnumerable<string> GetTemplateFiles() {
            var files = fs.GetFiles(fs.Root, "*paket.template", true);
            
            foreach (var file in files) {
                // Ignore files with BuildInfrastructureWorkingDirectory
                if (file.IndexOf(BuildInfrastructureWorkingDirectory, StringComparison.OrdinalIgnoreCase) >= 0) {
                    continue;
                }
                // Ignore files in obj\ folder which may be created by the compiler
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

    /// <summary>
    /// The Cmdlet for PowerShell "Publish-ExpertPackage"
    /// To pack the project executives into a .nupkg package.
    /// </summary>
    [Cmdlet(VerbsData.Publish, "ExpertPackage")]
    [OutputType(typeof(PackResult))]
    public class Package : PSCmdlet {

        /// <summary>
        /// Root directory of the project and where the .paket.template file resites.
        /// </summary>
        [Parameter(Mandatory = true, Position = 0)]
        public string Repository { get; set; }

        /// <summary>
        /// Nupkg version. e.g. 0.1.0
        /// </summary>
        [Parameter(Mandatory = true, Position = 1)]
        public string Version { get; set; }

        /// <summary>
        /// To execute this cmdlet by Windows PowerShell.
        /// Create a Packager object and execute the Pack method to generate a .nupkg package referencing the .paket.template file and other resources generated by the compiler.
        /// </summary>
        protected override void ProcessRecord() {
            base.ProcessRecord();

            // Create Packager object and pass the root directory of the project and a logger reference to the PowerShell output
            var packager = new Packager(new PhysicalFileSystem(Repository), new PowerShellLogger(this.Host));
            // Writes to the output pipeline, false means to reserve the returned object for later PowerShell scripts to use.
            WriteObject(packager.Pack(Version), false); 
        }
    }
}