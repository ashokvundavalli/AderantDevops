using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class RetrieveArtifacts : BuildOperationContextTask {

    }

    public class MakeSymlink : Microsoft.Build.Utilities.Task {
        [Required]
        public string Link { get; set; }

        [Required]
        public string Target { get; set; }

        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to fail if the link is directory.
        /// People like to check in a dependencies or package folder which breaks us so we fail if we bump into one of these gems.
        /// </summary>
        public bool FailIfLinkIsDirectoryWithContent { get; set; }

        public override bool Execute() {
            if (!ValidateIsAdmin()) {
                return !Log.HasLoggedErrors;
            }

            NativeMethods.SymbolicLink link = NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;

            if (!string.IsNullOrEmpty(Type)) {
                switch (Char.ToLowerInvariant(Type[0])) {
                    case 'f':
                        link = NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE;
                        break;
                    case 'd':
                        link = NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;
                        break;
                }
            }

            try {
                Target = Path.GetFullPath(Target);
                Link = Path.GetFullPath(Link);

                if (!Directory.Exists(Target)) {
                    throw new InvalidOperationException(string.Format("Target: {0} does not exist", Target));
                }
                Log.LogMessage("Creating symlink {0} <=====> {1}", Link, Target);

                DirectoryInfo info = new DirectoryInfo(Link);
                if (FailIfLinkIsDirectoryWithContent && info.Exists && !info.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    if (info.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly).Any()) {
                        Log.LogError($"Error: Unable to create symbolic link. The link '{Link}' exists and is not a reparse point. Is this folder committed in error?");
                        return false;
                    }
                }

                if (info.Exists) {
                    info.Delete(true);
                }

                if (!NativeMethods.CreateSymbolicLink(Link, Target, (uint)link)) {
                    Log.LogError($"Error: Unable to create symbolic link '{Link}'. (Error Code: {Marshal.GetLastWin32Error()})");
                }
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
                return false;
            }

            return !Log.HasLoggedErrors;
        }

        private bool ValidateIsAdmin() {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) {
                if (identity != null) {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator)) {
                        Log.LogError("Cannot create symlinks. Process must be run with Administrator rights");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
