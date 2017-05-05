using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    public sealed class GetAssembliesWithTraits : Task {
        [Required]
        public string CompanyName { get; set; }

        [Required]
        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] Assemblies { get; set; }

        public override bool Execute() {
            List<ITaskItem> arrayList = new List<ITaskItem>();

            foreach (var file in Files) {
                string fullPath = file.GetMetadata("FullPath");

                Log.LogMessage("ItemSpec: " + file.ItemSpec);
                Log.LogMessage("Scanning: " + fullPath);

                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(fullPath);

                bool isMatch = false;

                if (versionInfo.CompanyName != null) {
                    Log.LogMessage("Reading CompanyName out of file " + fullPath);

                    isMatch = versionInfo.CompanyName.IndexOf(CompanyName, StringComparison.OrdinalIgnoreCase) >= 0;

                    Log.LogMessage("CompanyName: " + versionInfo.CompanyName);
                } else {
                    Log.LogMessage("CompanyName was not set on file " + fullPath + " (" + versionInfo.FileVersion + ")");
                }

                if (isMatch) {
                    var taskItem = GetFileVersionInfo.CreateTaskItemFromVersionInfo(file, versionInfo);

                    arrayList.Add(taskItem);
                }
            }

            Assemblies = arrayList.ToArray();

            return !Log.HasLoggedErrors;
        }
    }
}