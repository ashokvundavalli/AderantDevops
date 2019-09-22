using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Added this SmartReference to watch project references. 
    /// 
    /// During development a project reference may be used, or automatically modified by Visual Studio even it was a dll reference. 
    /// For Customization Build, any *.Library projects will not be shipped to the client, so we need to change it back to a dll reference.
    /// 
    /// This class can be further used to handle more generic reference resolving works.
    /// </summary>
    /// <remarks>
    /// Use of this requires this binary, the Aderant.Build.dll, being shipped into BuildScripts.zip in ExpertSource\Customization to the client.
    /// The packing is processed in Framework, TFSBuild.proj.
    /// </remarks>
    public class SmartReference : Microsoft.Build.Utilities.Task {
        
        /// <summary>
        /// Project references.
        /// </summary>
        public ITaskItem[] ProjectReferences { get; set; }
        
        /// <summary>
        /// DLL references.
        /// </summary>
        public ITaskItem[] References { get; set; }

        /// <summary>
        /// Return changed project references. 
        /// </summary>
        [Output]
        public ITaskItem[] ModifiedProjectReferences { get; set; }

        /// <summary>
        /// Return changed DLL references.
        /// </summary>
        [Output]
        public ITaskItem[] ModifiedReferences { get; set; }

        /// <summary>
        /// If it's a customization build or not.
        /// </summary>
        public bool Customization { get; set; }

        /// <summary>
        /// The staging build output directory for customization build.
        /// </summary>
        public string AlternativeOutputDirectory { get; set; }

        /// <summary>
        /// The ExpertSource where all DLLs should be looked for.
        /// </summary>
        public string ExpertSourceDirectory { get; set; }

        public override bool Execute() {
            var newAssemblyReferences = new List<TaskItem>();

            if (Customization) {

                if (ExpertSourceDirectory == null) {
                    // Nowhere to look for alternative DLLs.
                    return true;
                }
                
                if (ProjectReferences != null) {
                    var projectReferences = ProjectReferences.ToList();

                    var libraryReferences = projectReferences.Where(p => p.ItemSpec.IndexOf(".Library", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (libraryReferences.Any()) {

                        var newProjectReferences = projectReferences.Except(libraryReferences);

                        foreach (var library in libraryReferences) {
                            var newReference =
                                new TaskItem(Path.Combine(ExpertSourceDirectory, library.GetMetadata("Name")) + ".dll");

                            Log.LogMessage(MessageImportance.Low,
                                $"{library.ItemSpec} has been substituted by {newReference.ItemSpec}");

                            newAssemblyReferences.Add(newReference);
                        }

                        ModifiedProjectReferences = newProjectReferences.ToArray();
                        ModifiedReferences = newAssemblyReferences.ToArray();
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
