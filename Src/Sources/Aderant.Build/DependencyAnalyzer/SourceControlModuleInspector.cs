using System;
using System.Linq;
using Aderant.Build.Logging;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.VersionControl.Common;

namespace Aderant.Build.DependencyAnalyzer {

    internal class SourceControlModuleInspector {
        private readonly ILogger logger;
        private readonly VersionControlServer service;

        public SourceControlModuleInspector(ILogger logger, VersionControlServer service) {
            this.logger = logger;
            this.service = service;
        }

        /// <summary>
        /// Determines whether the given name is a buildable module
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="sourceBranch">The source branch.</param>
        public bool IsValidModule(string moduleName, string sourceBranch) {
            string sccPath;

            if (moduleName.EndsWith(".Help", StringComparison.OrdinalIgnoreCase)) {
                sccPath = VersionControlPath.Combine("$/ExpertSuite", sourceBranch + "/Modules/" + moduleName);
                return FileExistsInSourceControl(sccPath, ItemType.Folder);
            }

            ModuleType type = ExpertModule.GetModuleType(moduleName);

            if (type != ModuleType.ThirdParty) {
                sccPath = VersionControlPath.Combine("$/ExpertSuite", sourceBranch + "/Modules/" + moduleName + "/Build/TFSBuild.proj");
                return FileExistsInSourceControl(sccPath, ItemType.File);
            }

            if (type == ModuleType.ThirdParty || type == ModuleType.Help) {
                sccPath = VersionControlPath.Combine("$/ExpertSuite", sourceBranch + "/Modules/ThirdParty/" + moduleName + "/bin");
                return FileExistsInSourceControl(sccPath, ItemType.Folder);
            } 

            return true;
        }

        private bool FileExistsInSourceControl(string sccPath, ItemType type) {
            logger.Info("Checking source control: {0}", sccPath);

            ItemSet itemSet = service.GetItems(sccPath, VersionSpec.Latest, RecursionType.OneLevel, DeletedState.NonDeleted, type, false);

            if (itemSet != null) {
                return itemSet.Items.Any();
            }
            return false;
        }
    }
}