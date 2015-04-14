using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class MakeSymlink : Microsoft.Build.Utilities.Task {
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.I1)]
        static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, UInt32 dwFlags);

        private enum SymbolicLink {
            SYMBOLIC_LINK_FLAG_FILE = 0,
            SYMBOLIC_LINK_FLAG_DIRECTORY = 1
        }

        [Required]
        public string Link { get; set; }

        [Required]
        public string Target { get; set; }

        public string Type { get; set; }

        public override bool Execute() {
            if (!ValidateIsAdmin()) {
                return !Log.HasLoggedErrors;
            }

            SymbolicLink link = SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;

            if (!string.IsNullOrEmpty(Type)) {
                switch (Char.ToLowerInvariant(Type[0])) {
                    case 'f':
                        link = SymbolicLink.SYMBOLIC_LINK_FLAG_FILE;
                        break;
                    case 'd':
                        link = SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;
                        break;
                }
            }

            try {
                Log.LogMessage("Creating symlink {0} <=====> {1}", Link, Target);

                CreateSymbolicLink(Link, Target, (UInt32)link);
            } catch (Exception ex) {
                Log.LogErrorFromException(ex);
                return false;
            }

            return true;
        }

        private bool ValidateIsAdmin() {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            if (identity != null) {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator)) {
                    Log.LogError("Cannot create symlinks. Process must be run with Administrator rights");
                    return false;
                }
            }
            return true;
        }
    }
}