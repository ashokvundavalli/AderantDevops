using System;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Providers {
    
    internal class TeamFoundationWorkspace : ITeamFoundationWorkspace {
        private readonly string teamProject;
        private readonly Workspace workspace;

        public TeamFoundationWorkspace(string project, Workspace workspace) {
            this.teamProject = project;
            this.workspace = workspace;
        }

        public static ITeamFoundationWorkspace Create(string teamProjectServerUri, string branchRoot, string workspaceName, string workspaceOwner) {
            if (!string.IsNullOrEmpty(teamProjectServerUri)) {
                TfsTeamProjectCollection teamProjectServer = new TfsTeamProjectCollection(new Uri(teamProjectServerUri));
                teamProjectServer.EnsureAuthenticated();

                VersionControlServer vcs = teamProjectServer.GetService<VersionControlServer>();

                if (!string.IsNullOrEmpty(workspaceName) && !string.IsNullOrEmpty(workspaceOwner)) {
                    Workspace workspace = vcs.GetWorkspace(workspaceName, workspaceOwner);
                    if (workspace != null) {
                        return new TeamFoundationWorkspace(null, workspace);
                    }
                } else {
                    if (workspaceName != null) {
                        Workspace workspace = vcs.GetWorkspace(workspaceName, teamProjectServer.AuthorizedIdentity.UniqueName);
                        if (workspace != null) {
                            return new TeamFoundationWorkspace(null, workspace);
                        }
                    }
                }
            }

            return ResolveWorkspaceFromPath(branchRoot);
        }

        private static TeamFoundationWorkspace ResolveWorkspaceFromPath(string branchRoot) {
            var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(branchRoot);
            if (workspaceInfo != null) {
                return new TeamFoundationWorkspace(null, workspaceInfo.GetWorkspace(new TfsTeamProjectCollection(workspaceInfo.ServerUri)));
            }
  
            throw new InvalidOperationException("Failed to determine Team Foundation Workspace for path: " + branchRoot);
        }

        public string ServerUri {
            get {
                return workspace.VersionControlServer.TeamProjectCollection.Uri.ToString();
            }
        }

        public string TeamProject {
            get {
                return teamProject;
            }
        }

        public string TryGetLocalItemForServerItem(string path) {
            return workspace.TryGetLocalItemForServerItem(path);
        }

        public string TryGetServerItemForLocalItem(string path) {
            return workspace.TryGetServerItemForLocalItem(path);
        }

        public void Get(string[] getRequest, VersionSpec versionSpec, RecursionType recursionType, GetOptions options) {
            workspace.Get(getRequest, versionSpec, recursionType, options);
        }

        public void PendAdd(string path) {
            workspace.PendAdd(path);
        }

        public void PendEdit(string path) {
            workspace.PendEdit(path);
        }

        public void PendBranch(string sourcePath, string targetPath, VersionSpec versionSpec) {
            workspace.PendBranch(sourcePath, targetPath, versionSpec);
        }

        public PendingChange[] GetPendingChanges() {
            return workspace.GetPendingChanges();
        }
    }
}