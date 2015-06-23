﻿using System;
using System.Security;
using System.Security.Principal;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class MakeSymlink : Microsoft.Build.Utilities.Task {

        [Required]
        public string Link { get; set; }

        [Required]
        public string Target { get; set; }

        public string Type { get; set; }

        public override bool Execute() {
            if (!ValidateIsAdmin()) {
                return !Log.HasLoggedErrors;
            }

            NativeUtilities.SymbolicLink link = NativeUtilities.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;

            if (!string.IsNullOrEmpty(Type)) {
                switch (Char.ToLowerInvariant(Type[0])) {
                    case 'f':
                        link = NativeUtilities.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE;
                        break;
                    case 'd':
                        link = NativeUtilities.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;
                        break;
                }
            }

            try {
                Log.LogMessage("Creating symlink {0} <=====> {1}", Link, Target);

                NativeUtilities.CreateSymbolicLink(Link, Target, (uint)link);
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