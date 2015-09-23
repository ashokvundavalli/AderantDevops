using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Providers {
    /// <summary>
    /// Represents an ExpertSuite developement environment . 
    /// 
    /// The ideal is to make this class the single entry point for all services required for working with Expert Suite.
    /// This class should manage the various manifest files and provide a set of dependency analysis services.
    /// 
    /// The class also talks to Team Foundation. 
    /// </summary>
    public class ModuleWorkspace {
        private readonly string branchPath;
        private string teamProject;
        private string teamFoundationServerUri;
        private IServiceProvider teamFoundationFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleWorkspace"/> class.
        /// </summary>
        /// <param name="branchPath">The server branch path.</param>
        /// <param name="teamFoundationServerUri">The team foundation server URI.</param>
        /// <param name="teamProject">The team project.</param>
        public ModuleWorkspace(string branchPath, string teamFoundationServerUri, string teamProject) {
            this.branchPath = branchPath;
            this.teamProject = teamProject;
            this.teamFoundationServerUri = teamFoundationServerUri;

            Task.Run(() => {
                WorkspaceWrapper workspace = GetWorkspaceForItem(branchPath);

                // We may have been passed a local path C:\Foo rather than $/Path/ so convert it if needed.
                branchPath = workspace.GetServerPath(branchPath);

                WorkspaceItem item = workspace.Find(CombineServerPaths(branchPath, "*ExpertManifest.xml"), "Src");

                Initialize(item.LocalItem);
            });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleWorkspace"/> class.
        /// </summary>
        /// <param name="expertManifestPath">The expert manifest path.</param>
        public ModuleWorkspace(string expertManifestPath) {
            Initialize(expertManifestPath);
        }

        private void Initialize(string expertManifestPath) {
            IModuleProvider manifest = ExpertManifest.Load(expertManifestPath);
            DependencyAnalyzer = new DependencyBuilder(manifest);
        }

        private string CombineServerPaths(params string[] paths) {
            return string.Join(Path.AltDirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), paths);
        }

        /// <summary>
        /// Gets or sets the team foundation server factory.
        /// </summary>
        /// <value>
        /// The team foundation factory.
        /// </value>
        public IServiceProvider TeamFoundationServiceFactory {
            get {
                if (teamFoundationFactory == null) {
                    teamFoundationFactory = TfsTeamProjectCollectionFactory.GetTeamProjectCollection(new Uri(teamFoundationServerUri));
                }
                return teamFoundationFactory;
            }
            set { teamFoundationFactory = value; }
        }

        public DependencyBuilder DependencyAnalyzer { get; private set; }

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
            WorkspaceInfo[] workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();

            HashSet<string> moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < workspaceInfo.Length; i++) {
                WorkspaceInfo info = workspaceInfo[i];

                Workspace workspace;
                try {
                    workspace = info.GetWorkspace((TfsTeamProjectCollection) TeamFoundationServiceFactory);
                } catch (InvalidOperationException) {
                    continue;
                }

                char[] splitCharArray = new char[] {Path.DirectorySeparatorChar};

                if (workspace != null) {
                    PendingChange[] pendingChanges = workspace.GetPendingChanges();

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
                }
            }

            return moduleNames.ToArray();
        }

        /// <summary>
        /// Gets the workspace for the given TFS item.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Could not determine current TFS workspace</exception>
        public WorkspaceWrapper GetWorkspaceForItem(string path) {
            TfsTeamProjectCollection server = (TfsTeamProjectCollection) TeamFoundationServiceFactory;

            WorkspaceInfo[] workspaceInfo = Workstation.Current.GetAllLocalWorkspaceInfo();

            for (int i = 0; i < workspaceInfo.Length; i++) {
                WorkspaceInfo info = workspaceInfo[i];

                Workspace workspace;
                try {
                    workspace = info.GetWorkspace(server);
                } catch (InvalidOperationException) {
                    continue;
                }

                if (path.StartsWith("$")) {
                    string serverPath = workspace.TryGetLocalItemForServerItem(path);
                    if (serverPath != null) {
                        return new WorkspaceWrapper(workspace);
                    }
                } else {
                    string serverPath = workspace.TryGetServerItemForLocalItem(path);
                    if (serverPath != null) {
                        return new WorkspaceWrapper(workspace);
                    }
                }
            }

            throw new InvalidOperationException("Could not determine current TFS workspace");
        }
    }

    public class WorkspaceWrapper {
        private readonly Workspace workspace;

        public WorkspaceWrapper(Workspace workspace) {
            this.workspace = workspace;
        }

        /// <summary>
        /// TFS queries do not support mulitple wildcards. Specifiy a file filter and an optional path filter if multiple matches are found.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="pathFilter">The filter.</param>
        public WorkspaceItem Find(string query, string pathFilter) {
            WorkspaceItemSet[] items = workspace.GetItems(new[] {new ItemSpec(query, RecursionType.Full)}, DeletedState.NonDeleted, ItemType.File, false, GetItemsOptions.LocalOnly);

            if (items != null) {
                foreach (WorkspaceItemSet set in items) {
                    if (set.Items.Length == 1) {
                        return set.Items[0];
                    }

                    if (pathFilter != null) {
                        foreach (WorkspaceItem item in set.Items) {
                            if (item.LocalItem.IndexOf(pathFilter, StringComparison.OrdinalIgnoreCase) >= 0) {
                                return item;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public string GetServerPath(string branchPath) {
            return workspace.TryGetServerItemForLocalItem(branchPath);
        }
    }
}