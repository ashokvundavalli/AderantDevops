using System;
using System.Collections.Generic;
using System.Globalization;
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

                Assembly thisAssembly = GetType().Assembly;

                AppDomain inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, Path.GetDirectoryName(thisAssembly.Location), null, false);

                AssemblyInspector inspector = (AssemblyInspector)inspectionDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, typeof(AssemblyInspector).FullName);

                Dictionary<ITaskItem, string> analyzedAssemblies = new Dictionary<ITaskItem, string>();

                var filteredAssemblies = Assemblies.Where(item => !item.GetMetadata("FullPath").Contains("SharedBin"));
                foreach (ITaskItem item in filteredAssemblies) {
                    string fullPath = item.GetMetadata("FullPath");
                    
                    if ((item.ItemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || item.ItemSpec.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) && File.Exists(item.ItemSpec)) {
                        string hash;
                        using (var cryptoProvider = new SHA1CryptoServiceProvider()) {
                            byte[] fileBytes = File.ReadAllBytes(fullPath);
                            hash = BitConverter.ToString(cryptoProvider.ComputeHash(fileBytes));
                        }
                    
                        if (ShouldAnalyze(analyzedAssemblies, hash, item)) {
                            analyzedAssemblies[item] = hash;

                            PortableExecutableKinds peKind = inspector.GetAssemblyKind(item.ItemSpec);

                            if (peKind.HasFlag(PortableExecutableKinds.Required32Bit)) {
                                MustRun32Bit = true;
                            }

                            item.SetMetadata("Platform", peKind.ToString());
                        }
                    }
                }

                inspector = null;
                AppDomain.Unload(inspectionDomain);
                GC.Collect();
            }

            return true;
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