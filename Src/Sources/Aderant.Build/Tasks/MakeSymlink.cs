﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Aderant.Build.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Aderant.Build.Logging;

namespace Aderant.Build.Tasks {
    public class MakeSymlink : Task {

        [Required]
        [Output]
        public string Link { get; set; }

        [Required]
        [Output]
        public string Target { get; set; }

        public string Type { get; set; }

        /// <summary>
        /// Creates the directory specified by <see cref="Link"/> if it does not exist.
        /// </summary>
        public bool CreateLinkParent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to fail if the link is directory.
        /// People like to check in a dependencies or package folder which breaks us so we fail if we bump into one of these gems.
        /// </summary>
        public bool FailIfLinkIsDirectoryWithContent { get; set; }

        public override bool Execute() {
            if (!ValidateIsAdmin()) {
                return !Log.HasLoggedErrors;
            }

            var logger = new BuildTaskLogger(this);

            ExecuteInternal(logger);

            return !Log.HasLoggedErrors;
        }

        public void ExecuteInternal(Logging.ILogger logger) {
            NativeMethods.SymbolicLink link = NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;

            bool useJunction = false;

            if (!string.IsNullOrEmpty(Type)) {
                switch (Char.ToLowerInvariant(Type[0])) {
                    case 'f':
                        link = NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_FILE;
                        break;
                    case 'd':
                        link = NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY;
                        break;
                    case 'j': {
                            useJunction = true;
                            break;
                        }
                }
            }

            if (!useJunction && !ValidateIsAdmin()) {
                return;
            }

            try {
                Target = Path.GetFullPath(Target);
                Link = Path.GetFullPath(Link);

                if (!Directory.Exists(Target)) {
                    throw new InvalidOperationException(string.Format("Target: {0} does not exist", Target));
                }

                DirectoryInfo info = new DirectoryInfo(Link);
                if (FailIfLinkIsDirectoryWithContent && info.Exists && !info.Attributes.HasFlag(FileAttributes.ReparsePoint)) {
                    if (info.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly).Any()) {
                        logger.Error($"Error: Unable to create symbolic link. The link '{Link}' exists and is not a reparse point. Is this folder committed in error?");
                        return;
                    }
                }

                if (info.Exists) {
                    logger.Info("Deleting directory: " + info.FullName);
                    info.Delete(true);
                }

                if (CreateLinkParent && (useJunction || link == NativeMethods.SymbolicLink.SYMBOLIC_LINK_FLAG_DIRECTORY)) {
                    var parentDirectory = info.Parent;
                    if (parentDirectory != null && !parentDirectory.Exists) {
                        logger.Info("Creating directory: " + parentDirectory.FullName);
                        parentDirectory.Create();
                    }
                }

                if (useJunction) {
                    logger.Info("Creating junction {0} <=====> {1}", Link, Target);
                    JunctionNativeMethods.CreateJunction(Target, Link, false);
                } else {
                    logger.Info("Creating symlink {0} <=====> {1}", Link, Target);
                    if (!NativeMethods.CreateSymbolicLink(Link, Target, (uint)link)) {
                        logger.Error($"Error: Unable to create symbolic link '{Link}'. (Error Code: {Marshal.GetLastWin32Error()})");
                    }
                }
            } catch (Exception ex) {
                logger.LogErrorFromException(ex, true, true);
                return;
            }
        }

        private bool ValidateIsAdmin() {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) {
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
