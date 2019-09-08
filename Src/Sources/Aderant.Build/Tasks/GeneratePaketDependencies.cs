using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.DependencyResolver;

namespace Aderant.Build.Tasks {
    public class GeneratePaketDependencies : Task {

        [Required]
        public string OutputDirectory { get; set; }

        [Required]
        public string ExpertManifest{ get; set; }

        [Required]
        public ITaskItem[] DependencyMappings { get; set; }

        public override bool Execute() {
            if (!File.Exists(ExpertManifest)) {
                Log.LogError($"Manifest: '{ExpertManifest}' does not exist.");
                return !Log.HasLoggedErrors;
            }

            if (DependencyMappings == null || DependencyMappings.Length == 0) {
                Log.LogWarning("No dependency mappings specified. Skipping paket dependencies generation.");
                return !Log.HasLoggedErrors;
            }

            ExpertManifest expertManifest = new ExpertManifest(new PhysicalFileSystem(), ExpertManifest);

            OutputDirectory = Path.GetFullPath(OutputDirectory);

            if (!Directory.Exists(OutputDirectory)) {
                Directory.CreateDirectory(OutputDirectory);
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "paket.dependencies"), GeneratePaketDependenciesContent(expertManifest, DependencyMappings), Encoding.UTF8);

            return !Log.HasLoggedErrors;
        }

        internal static string GeneratePaketDependenciesContent(ExpertManifest expertManifest, ITaskItem[] dependencyMappings) {
            StringBuilder paketDependenciesContent = new StringBuilder();

            IWellKnownSources sources = WellKnownPackageSources.Default;
            IReadOnlyList<PackageSource> packageSources = sources.GetSources();
            foreach (var source in packageSources) {
                paketDependenciesContent.AppendLine($"source {source.Url}");
            }

            foreach (ITaskItem mapping in dependencyMappings) {
                string versionRequirement = AcquireVersionRequirement(expertManifest, mapping.GetMetadata("ReferenceRequirement"));

                paketDependenciesContent.AppendLine($"nuget {mapping.ItemSpec} {versionRequirement}");
            }

            return paketDependenciesContent.ToString();
        }

        private static string AcquireVersionRequirement(ExpertManifest expertManifest, string moduleName) {
            ExpertModule module = expertManifest.GetModule(moduleName);

            return module.VersionRequirement.ConstraintExpression;
        }
    }
}
