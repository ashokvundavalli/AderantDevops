using System.IO;
using System.Management.Automation;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;

namespace Aderant.Build.Commands {
    /// <summary>
    /// Handles the creation of a MS Build project file to control the build process.
    /// </summary>
    [Cmdlet(VerbsData.Update, "BuildAllProject")]
    public sealed class UpdateBuildAllProject : PSCmdlet {
        /// <summary>
        /// Gets or sets the path to the module project.
        /// </summary>
        /// <value>
        /// The module project.
        /// </value>
        [Parameter(Mandatory = false, Position = 0, HelpMessage = "Specifies the path to a MS Build project containing a single item group which specifies the branch build configuration.")]
        public string ModuleProject { get; set; }

        [Parameter(Mandatory = false, Position = 1, HelpMessage = "Specifies the path to a MS Build project containing a single item group which specifies the branch build configuration.")]
        public string ProductManifest { get; set; }

        protected override void ProcessRecord() {
            base.ProcessRecord();

            string branchPath = ParameterHelper.GetBranchPath(Path.GetDirectoryName(ModuleProject), SessionState);

            if (string.IsNullOrEmpty(ModuleProject)) {
                ModuleProject = Path.Combine(branchPath, Path.Combine("Modules", "Modules.proj"));
            }

            if (string.IsNullOrEmpty(ProductManifest)) {
                ProductManifest = ParameterHelper.GetExpertManifestPath(SessionState);
            }

            if (!string.IsNullOrEmpty(ModuleProject)) {
                var provider = ExpertManifest.Load(ProductManifest);
                provider.ModulesDirectory = Path.Combine(branchPath, Path.Combine("Modules"));

                SequenceBuilds(provider, ModuleProject);
            }
        }

        private void SequenceBuilds(IModuleProvider provider, string moduleProject) {
            ITeamFoundationWorkspace workspace = ServiceLocator.GetInstance<ITeamFoundationWorkspace>();

            Host.UI.WriteLine("Sequencing builds...");

            string sequence = BuildProjectSequencer.CreateOrUpdateBuildSequence(workspace, moduleProject, provider);

            Host.UI.WriteLine();
            Host.UI.WriteLine("Writing updated project file: " + moduleProject);

            File.WriteAllText(moduleProject, sequence);
        }
    }
}