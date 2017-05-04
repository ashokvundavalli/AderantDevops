using System;
using System.Collections;
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
            ArrayList arrayList = new ArrayList();

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
                    Log.LogMessage("Detected Aderant asset");
                    try {
                        var assemblyName = AssemblyName.GetAssemblyName(fullPath);
                        arrayList.Add(file);
                        Log.LogMessage("File '" + fullPath + "' was detected as an Aderant asset.");
                    } catch (BadImageFormatException ex) {
                        Log.LogMessage("Whoops, something went wrong: ", ex.ToString());
                    }
                }
            }
            this.Assemblies = (ITaskItem[])arrayList.ToArray(typeof(ITaskItem));

            return !Log.HasLoggedErrors;
        }
    }
}