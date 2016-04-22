using Microsoft.TeamFoundation.VersionControl.Client;

namespace Aderant.Build.Providers {
    internal interface ITeamFoundationWorkspace : IWorkspace {
        string TryGetLocalItemForServerItem(string path);
        string TryGetServerItemForLocalItem(string path);
        void Get(string[] getRequest, VersionSpec versionSpec, RecursionType recursionType, GetOptions options);
        void PendAdd(string project);
        void PendEdit(string serverPathToManifest);
        void PendBranch(string sourcePath, string targetPath, VersionSpec versionSpec);
        PendingChange[] GetPendingChanges();
        string ServerUri { get; }
        string TeamProject { get; }
    }
}