using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Providers {
    /// <summary>
    /// Represents an ExpertSuite development environment. 
    /// 
    /// The ideal is to make this class the single entry point for all services required for working with Expert Suite.
    /// This class should manage the various manifest files and provide a set of dependency analysis services.
    /// 
    /// The class also talks to Team Foundation. 
    /// </summary>
    [Export(typeof(IWorkspace))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ModuleWorkspace : ISourceControlProvider, IWorkspace {
        private static ITeamFoundationWorkspace tfsWorkspace;
        private static VersionControlServer versionControlServer;
        private DependencyBuilder dependencyAnalyzer;
        private Task workspaceTask;
        private bool workspaceTaskCompleted;

        [ContextualExport(typeof(ITeamFoundationWorkspace), ExportMode.Desktop)]
        ITeamFoundationWorkspace ISourceControlProvider.Workspace {
            get {
                WaitForWorkspace();
                return tfsWorkspace;
            }
        }
      
        [ContextualExport(typeof(VersionControlServer), ExportMode.Desktop)]
        public VersionControlServer VersionControlServer {
            get {
                WaitForWorkspace();

                if (tfsWorkspace == null) {
                    throw new InvalidOperationException("No workspace available.");
                }
                return versionControlServer;
            }
        }

        [Export(typeof(VersionControlServer))]
        public DependencyBuilder DependencyAnalyzer {
            get {
                WaitForWorkspace();

                return dependencyAnalyzer;
            }
        }

        private void WaitForWorkspace() {
            if (workspaceTaskCompleted) {
                return;
            }

            if (workspaceTask != null && !workspaceTask.IsCompleted) {
                workspaceTask.Wait();
            }
        }


        static ModuleWorkspace() {
            VisualStudioEnvironmentContext.SetupContext();
        }

        public ModuleWorkspace() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleWorkspace"/> class.
        /// </summary>
        /// <param name="path">The expert manifest path.</param>
        /// /// <param name="teamProject">The TFS team project.</param>
        public ModuleWorkspace(string path, string teamProject) {
            GetWorkspaceForPath(path, teamProject);
        }

        private void GetWorkspaceForPath(string path, string teamProject) {
            workspaceTask = Task.Run(() => {
                WorkspaceInfo workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(path);

                if (workspaceInfo != null) {
                    Workspace workspace = workspaceInfo.GetWorkspace(new TfsTeamProjectCollection(workspaceInfo.ServerUri));
                    versionControlServer = workspace.VersionControlServer;
                    tfsWorkspace = new TeamFoundationWorkspace(teamProject, workspace);
                }

                string manifestPath;
                if (path.EndsWith(".xml")) {
                    manifestPath = path;
                } else {
                    manifestPath = Path.Combine(path, PathHelper.PathToProductManifest);
                }

                IModuleProvider manifest = ExpertManifest.Load(manifestPath);
                dependencyAnalyzer = new DependencyBuilder(manifest);
            }).ContinueWith(delegate {
                workspaceTask = null;
                workspaceTaskCompleted = true;
            });
        }

        /// <summary>
        /// Gets the modules for the given names.
        /// </summary>
        /// <param name="moduleNames">The module names.</param>
        public ICollection<ExpertModule> GetModules(string[] moduleNames) {
            IEnumerable<ExpertModule> modules = DependencyAnalyzer.GetAllModules();

            return modules.Where(m => moduleNames.Contains(m.Name, StringComparer.OrdinalIgnoreCase)).ToArray();
        }

        /// <summary>
        /// Gets the modules with pending changes.
        /// </summary>
        /// <param name="branchPath">The branch path to restrict changes to.</param>
        public ICollection<ExpertModule> GetModulesWithPendingChanges(string branchPath) {
            string[] names = GetModuleNamesWithPendingChanges(branchPath);

            IEnumerable<ExpertModule> modules = DependencyAnalyzer.GetAllModules();

            List<ExpertModule> modulesWithChanges = new List<ExpertModule>(names.Length);

            foreach (string name in names) {
                foreach (ExpertModule module in modules) {
                    if (string.Equals(module.Name, name, StringComparison.OrdinalIgnoreCase)) {
                        modulesWithChanges.Add(module);
                        break;
                    }
                }
            }

            return modulesWithChanges;
        }

        private string[] GetModuleNamesWithPendingChanges(string branchPath) {
            HashSet<string> moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            char[] splitCharArray = new char[] {Path.DirectorySeparatorChar};

            PendingChange[] pendingChanges = ((ISourceControlProvider) this).Workspace.GetPendingChanges();

            foreach (PendingChange change in pendingChanges) {
                if (change.ItemType == ItemType.File) {
                    if (change.LocalItem.IndexOf(branchPath, StringComparison.OrdinalIgnoreCase) >= 0) {
                        string localItem = change.LocalItem;

                        string folder = localItem.Substring(localItem.IndexOf(branchPath, StringComparison.OrdinalIgnoreCase) + branchPath.Length);

                        folder = folder.Trim(splitCharArray);
                        string moduleName = folder.Split(splitCharArray, StringSplitOptions.RemoveEmptyEntries)[0];

                        moduleNames.Add(moduleName);
                    }
                }
            }

            return moduleNames.ToArray();
        }
    }

    public interface IWorkspace {
    }

    internal interface ISourceControlProvider {
        ITeamFoundationWorkspace Workspace { get; }
    }
}