using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public class GetCopyLocalItemsToRemove : Task {
        private IList<ITaskItem> copyLocalFiles;

        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        public ITaskItem[] Dependencies { get; set; }

        [Output]
        public ITaskItem[] CopyLocalFiles { get; private set; }

        public override bool Execute() {
            copyLocalFiles = new List<ITaskItem>(SourceFiles.Length);

            char[] directorySeparatorChar = new[] {Path.DirectorySeparatorChar};

            foreach (ITaskItem item in SourceFiles) {
                // Try remove the file if it comes as part of the .NET Framework
                if (!RemoveFrameworkFile(item)) {
                    // Inspect the resolved path metadata to decide this items fate
                    string metadata = item.GetMetadata("ResolvedFrom");

                    if (!string.IsNullOrEmpty(metadata)) {
                        if (metadata.TrimEnd(directorySeparatorChar).EndsWith("Dependencies", StringComparison.OrdinalIgnoreCase)) {
                            AddItem(item);
                            continue;
                        }

                        if (metadata.IndexOf(".NETFramework", StringComparison.OrdinalIgnoreCase) >= 0) {
                            AddItem(item);
                            continue;
                        }

                        if (metadata.IndexOf("{TargetFrameworkDirectory}", StringComparison.OrdinalIgnoreCase) >= 0) {
                            AddItem(item);
                        }
                    }

                    // Sometimes the build will find the dependency in the bin directory itself (due to copy local)
                    // so these items have sightly different metadata
                    RemoveItemResolvedFromBin(item);
                }
            }

            CopyLocalFiles = copyLocalFiles.ToArray();

            return true;
        }

        private void RemoveItemResolvedFromBin(ITaskItem item) {
            string hintPath = item.GetMetadata("HintPath");
            string buildReference = item.GetMetadata("BuildReference");

            if (!string.IsNullOrEmpty(hintPath)) {
                bool result;
                if (bool.TryParse(buildReference, out result)) {
                    if (hintPath.IndexOf("Dependencies", StringComparison.OrdinalIgnoreCase) >= 0) {
                        AddItem(item);
                        return;
                    }
                }
            }

            if (Dependencies != null) {
                string itemFileName = GetFileName(item);

                foreach (ITaskItem taskItem in Dependencies) {
                    string fileName = GetFileName(taskItem);

                    if (string.Equals(itemFileName, fileName, StringComparison.OrdinalIgnoreCase)) {
                        AddItem(item);
                        return;
                    }
                }
            }
        }

        private static string GetFileName(ITaskItem taskItem) {
            string fileName = taskItem.GetMetadata("FileName");
            string extension = taskItem.GetMetadata("Extension");
           
            return fileName + extension;
        }

        private bool RemoveFrameworkFile(ITaskItem item) {
            string metadata = item.GetMetadata("FrameworkFile");
            if (!string.IsNullOrEmpty(metadata)) {
                bool result;
                if (bool.TryParse(metadata, out result)) {
                    AddItem(item);
                    return true;
                }
            }
            return false;
        }

        private void AddItem(ITaskItem item) {
            copyLocalFiles.Add(item);
            Log.LogMessage(MessageImportance.Low, "Removing copy local item: " + item.ItemSpec, null);
        }
    }
}