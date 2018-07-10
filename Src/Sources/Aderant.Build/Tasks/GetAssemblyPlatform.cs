using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Determines the platform of an assembly. 
    /// Used by the build process to determine the type of test runner that should be used. If we have at least one 32-bit only assembly
    /// then the build process must use the 32-bit of VS Test. 
    /// </summary>
    public sealed class GetAssemblyPlatform : Microsoft.Build.Utilities.Task {
        private readonly string[] allowedAssemblyExtensions = {
            ".winmd",
            ".dll",
            ".exe"
        };

        private AppDomain inspectionDomain;

        /// <summary>
        /// Gets or sets the assemblies to analyze.
        /// This is an output property (two way) as it need to return modified metadata to the build process.
        /// </summary>
        /// <value>
        /// The assemblies.
        /// </value>
        [Output]
        public ITaskItem[] Assemblies { get; set; }

        /// <summary>
        /// Gets or sets  the flag indicating if the 32-bit test runner should be used.
        /// </summary>
        [Output]
        public bool MustRun32Bit { get; set; }

        public override bool Execute() {
            if (Assemblies != null) {
                Log.LogMessage(MessageImportance.High, "Building assembly platform architecture list...");

                AssemblyInspector inspector = CreateInspector();

                Dictionary<ITaskItem, string> analyzedAssemblies = new Dictionary<ITaskItem, string>();

                IEnumerable<ITaskItem> filteredAssemblies = Assemblies.Where(item => !item.GetMetadata("FullPath").Contains("SharedBin"));

                Queue<ITaskItem> scanQueue = new Queue<ITaskItem>(filteredAssemblies);
               
                 while (scanQueue.Count > 0) {
                    var item = scanQueue.Dequeue();
                  
                    string fullPath = item.GetMetadata("FullPath");
                    
                    if (PathUtility.HasExtension(item.ItemSpec, allowedAssemblyExtensions) && File.Exists(item.ItemSpec)) {
                        string hash;
                        using (var cryptoProvider = new SHA1CryptoServiceProvider()) {
                            byte[] fileBytes = File.ReadAllBytes(fullPath);
                            hash = BitConverter.ToString(cryptoProvider.ComputeHash(fileBytes));
                        }
                    
                        if (ShouldAnalyze(analyzedAssemblies, hash, item)) {
                            try {
                                PortableExecutableKinds peKind = inspector.GetAssemblyKind(item.ItemSpec);

                                if ((peKind & PortableExecutableKinds.Required32Bit) != 0) {
                                    MustRun32Bit = true;
                                }

                                // Optimization to reduce boxing for the common case
                                if (peKind == PortableExecutableKinds.ILOnly) {
                                    item.SetMetadata("Platform", "ILOnly");
                                } else if (peKind == PortableExecutableKinds.Required32Bit) {
                                    item.SetMetadata("Platform", "x86");
                                } else {
                                    item.SetMetadata("Platform", peKind.ToString());
                                }

                                analyzedAssemblies[item] = hash;
                            } catch (FileLoadException ex) {
                                Log.LogWarning($"Creating new inspection domain for {item.ItemSpec} due to: {ex.Message}. You should delete this file or other file with the same identity to resolve this warning.");

                                inspector = CreateInspector();

                                scanQueue.Enqueue(item);
                            }
                        }
                    }
                }

                if (inspectionDomain != null) {
                    AppDomain.Unload(inspectionDomain);
                    GC.Collect();
                }
            }

            return !Log.HasLoggedErrors;
        }

        private AssemblyInspector CreateInspector() {
            Assembly thisAssembly = GetType().Assembly;

            if (inspectionDomain != null) {
                AppDomain.Unload(inspectionDomain);
                inspectionDomain = null;
            }

            inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, Path.GetDirectoryName(thisAssembly.Location), null, false);
            return (AssemblyInspector)inspectionDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, typeof(AssemblyInspector).FullName);
        }

        private bool ShouldAnalyze(Dictionary<ITaskItem, string> analyzedAssemblies, string hash, ITaskItem item) {
            // We have already seen this assembly based on it's file name. Loading it again will probably cause a FileLoadException as
            // the CLR will not allow the loading of identical assemblies from two different locations in to the same domain.
            // This issue can arise where a project has copy local turned on and we end up with the build process finding the same output assembly under multiple
            // locations within the module, for example web projects which copy files from the packages\dependencies folder to project\bin
            foreach (var analyzedAssembly in analyzedAssemblies) {
                if (string.Equals(analyzedAssembly.Value, hash)) {
                    var assembly = analyzedAssembly.Key;

                    // Copy the platform from the already processed input
                    item.SetMetadata("Platform", assembly.GetMetadata("Platform"));
                    return false;
                }
            }

            return true;
        }
    }

    [Serializable]
    internal class AssemblyInspector : MarshalByRefObject {
        public PortableExecutableKinds GetAssemblyKind(string assembly) {
            Assembly asm = Assembly.ReflectionOnlyLoadFrom(assembly);

            PortableExecutableKinds peKind;
            ImageFileMachine imageFileMachine;
            asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);

            return peKind;
        }
    }
}
