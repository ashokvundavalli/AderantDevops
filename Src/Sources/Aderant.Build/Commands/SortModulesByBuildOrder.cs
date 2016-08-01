using System;
using System.Linq;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Commands {
    [Cmdlet("Sort", "ExpertModulesByBuildOrder")]
    public class SortModulesByBuildOrder : PSCmdlet {

        [Parameter(Mandatory = false, Position = 0)]
        public string[] ModuleNames { get; set; }

        [Parameter(Mandatory = false, ValueFromPipeline = true, Position = 1)]
        public ExpertModule[] Modules { get; set; }

        [Parameter(Mandatory = false, Position = 2)]
        public string BranchPath { get; set; }

        [Parameter(Mandatory = true, Position = 3)]
        public string ProductManifestPath { get; set; }

        protected override void ProcessRecord() {
            if (Modules == null && ModuleNames == null) {
                throw new ArgumentException("Must specify Modules or ModuleNames");
            }

            ExpertManifest manifest = ExpertManifest.Create(ProductManifestPath);
            manifest.ModulesDirectory = BranchPath;

            DependencyBuilder builder = new DependencyBuilder(manifest);

            if (Modules == null) {
                Modules = (from module in builder.GetAllModules()
                           where ModuleNames.Contains(module.Name, StringComparer.OrdinalIgnoreCase)
                           select module).ToArray();
            }

            if (ModuleNames != null && ModuleNames.Length == 1) {
                WriteObject(Modules.FirstOrDefault(m => m.Name.Equals(ModuleNames[0], StringComparison.OrdinalIgnoreCase)), true);
                return;
            }

            var modules = (from build in builder.GetTree(true)
                           from module in build.Modules
                           where Modules.Contains(module)
                           select new {Module = module,
                               BuildNumber = build.Order}).OrderBy(x => x.BuildNumber).Select(x => x.Module).ToArray();

            WriteObject(modules, true);
        }
    }
}