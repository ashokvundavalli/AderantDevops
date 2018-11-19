﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Determines the platform of an assembly.
    /// Used by the build process to determine the type of test runner that should be used. If we have at least one 32-bit only
    /// assembly
    /// then the build process must use the 32-bit of VS Test.
    /// </summary>
    public sealed class GetAssemblyPlatform : Task {
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

        public string[] AssemblyDependencies { get; set; }

        /// <summary>
        /// Gets or sets  the flag indicating if the 32-bit test runner should be used.
        /// </summary>
        [Output]
        public bool MustRun32Bit { get; set; }

        [Output]
        public string AssemblyPlatformDataKey { get; private set; } = "GetAssemblyPlatformData";

        [Output]
        public string[] ReferencesToFind { get; private set; }

        public override bool Execute() {
            if (Assemblies == null) {
                return true;
            }

            Log.LogMessage(MessageImportance.High, "Building assembly platform architecture list...");

            AssemblyInspector inspector = CreateInspector();

            Dictionary<ITaskItem, string> analyzedAssemblies = new Dictionary<ITaskItem, string>();

            // TODO: No longer needed
            IEnumerable<ITaskItem> filteredAssemblies = Assemblies.Where(item => !item.GetMetadata("FullPath").Contains("SharedBin"));

            Queue<ITaskItem> scanQueue = new Queue<ITaskItem>(filteredAssemblies);

            List<ITaskItem> failedItems = new List<ITaskItem>();

            List<string> assemblyReferencesToFind = new List<string>();

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
                            AssemblyName[] referencedAssemblies;
                            ProcessorArchitecture[] referenceArchitectures;
                            PortableExecutableKinds peKind = inspector.Inspect(item.ItemSpec, out referencedAssemblies, out referenceArchitectures);

                            if (referencedAssemblies != null) {
                                assemblyReferencesToFind.AddRange(referencedAssemblies.Select(s => s.Name));
                            }

                            if ((peKind & PortableExecutableKinds.Required32Bit) != 0) {
                                MustRun32Bit = true;
                            }

                            if (referenceArchitectures != null) {
                                foreach (var arch in referenceArchitectures) {
                                    if (arch == ProcessorArchitecture.X86) {
                                        MustRun32Bit = true;
                                        break;
                                    }
                                }
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
                            if (!failedItems.Contains(item)) {
                                Log.LogWarning($"Creating new inspection domain for {item.ItemSpec} due to: {ex.Message}. You should delete this file or the other file with the same identity to resolve this warning.");

                                inspector = CreateInspector();

                                scanQueue.Enqueue(item);

                                // Record this failure so we don't get stuck in a loop
                                failedItems.Add(item);
                            }
                        }
                    }
                }
            }

            if (inspectionDomain != null) {
                System.Threading.Tasks.Task.Run(
                    () => {
                        AppDomain.Unload(inspectionDomain);
                        inspectionDomain = null;
                    });
            }

            this.ReferencesToFind = assemblyReferencesToFind.ToArray();

            BuildEngine4.UnregisterTaskObject(AssemblyPlatformDataKey, RegisteredTaskObjectLifetime.Build);

            // Stash the object for downstream tasks
            BuildEngine4.RegisterTaskObject(
                AssemblyPlatformDataKey,
                new AssemblyPlatformData {
                    Assemblies = Assemblies,
                    ReferencesToFind = ReferencesToFind,
                },
                RegisteredTaskObjectLifetime.Build,
                false);

            return !Log.HasLoggedErrors;
        }

        private AssemblyInspector CreateInspector() {
            Assembly thisAssembly = typeof(GetAssemblyPlatform).Assembly;

            if (inspectionDomain != null) {
                AppDomain.Unload(inspectionDomain);
                inspectionDomain = null;
            }

            inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, Path.GetDirectoryName(thisAssembly.Location), null, false);
            var inspector = (AssemblyInspector)inspectionDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, typeof(AssemblyInspector).FullName);
            inspector.AssemblyDependencies = AssemblyDependencies;
            inspector.Logger = Log;

            return inspector;
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

    public class AssemblyPlatformData {
        public ITaskItem[] Assemblies { get; set; }
        public string[] ReferencesToFind { get; set; }
    }

    internal class AssemblyInspector : MarshalByRefObject {
        private Dictionary<string, string> fileMap;
        private HashSet<string> seenAssemblies = new HashSet<string>();

        public string[] AssemblyDependencies { get; set; }
        public TaskLoggingHelper Logger { get; set; }

        public PortableExecutableKinds Inspect(string assembly, out AssemblyName[] referencesToFind, out ProcessorArchitecture[] referenceArchitectures) {
            referencesToFind = null;
            referenceArchitectures = null;

            if (fileMap == null && AssemblyDependencies != null) {
                Dictionary<string, string> dictionary = new Dictionary<string, string>();

                foreach (var dependency in AssemblyDependencies) {
                    string key = Path.GetFileName(dependency);
                    if (!dictionary.ContainsKey(key)) {
                        dictionary.Add(key, dependency);
                    } else {
                        if (Logger != null) {
                            Logger.LogWarning("Already seen file: " + dependency);
                        }
                    }
                }

                fileMap = dictionary;
            }

            Assembly asm = Assembly.ReflectionOnlyLoadFrom(assembly);

            PortableExecutableKinds peKind;
            ImageFileMachine imageFileMachine;
            asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);

            var references = asm.GetReferencedAssemblies();

            if (ShouldDeployMissingReferences(asm)) {
                referencesToFind = references;
            }

            if (fileMap != null) {
                var architectures = GetReferenceArchitectures(references);
                referenceArchitectures = architectures.ToArray();
            }

            return peKind;
        }

        private List<ProcessorArchitecture> GetReferenceArchitectures(AssemblyName[] references) {
            List<ProcessorArchitecture> architectures = new List<ProcessorArchitecture>();

            foreach (var reference in references) {
                try {
                    if (!seenAssemblies.Contains(reference.Name)) {
                        var names = new string[] {
                            reference.Name + ".dll",
                            reference.Name + ".exe",
                            reference.Name + ".winmd",
                        };

                        for (var i = 0; i < names.Length; i++) {
                            var name = names[i];
                            if (SetArchitecture(name, architectures)) {
                                break;
                            }
                        }
                    }
                } catch (Exception) {

                } finally {
                    seenAssemblies.Add(reference.Name);
                }
            }

            return architectures;
        }

        private bool SetArchitecture(string fileName, List<ProcessorArchitecture> architectures) {
            string locationOnDisk;
            if (fileMap.TryGetValue(fileName, out locationOnDisk)) {
                var assemblyName = AssemblyName.GetAssemblyName(locationOnDisk);

                if (assemblyName.ProcessorArchitecture != ProcessorArchitecture.MSIL) {
                    architectures.Add(assemblyName.ProcessorArchitecture);
                }
            }

            return locationOnDisk != null;
        }

        private static bool ShouldDeployMissingReferences(Assembly asm) {
            IList<CustomAttributeData> customAttributes = asm.GetCustomAttributesData();
            foreach (var customAttribute in customAttributes) {
                if (customAttribute.AttributeType == typeof(AssemblyMetadataAttribute)) {
                    if ((string)customAttribute.ConstructorArguments[0].Value == "TestRun:DeployMissingReferences") {
                        var deployMissingReferences = bool.Parse((string)customAttribute.ConstructorArguments[1].Value);

                        if (deployMissingReferences) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
