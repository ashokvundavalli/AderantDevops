using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Aderant.Build.Utilities;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.XamlTypes;
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
        private static readonly string[] allowedAssemblyExtensions = {
            ".winmd",
            ".dll",
            ".exe"
        };

        private List<ITaskItem> assemblies;

        private List<ITaskItem> assembliesTargetingX64 = new List<ITaskItem>();
        private List<ITaskItem> assembliesTargetingX86 = new List<ITaskItem>();

        private AppDomain inspectionDomain;
        private InspectionDomainInitializer init;

        /// <summary>
        /// Gets or sets the assemblies to analyze.
        /// This is an output property (two way) as it need to return modified metadata to the build process.
        /// </summary>
        /// <value>
        /// The assemblies.
        /// </value>
        [Output]
        public ITaskItem[] Assemblies {
            get {
                if (assemblies != null) {
                    return assemblies.ToArray();
                }

                return Array.Empty<ITaskItem>();
            }
            set {
                if (value != null) {
                    assemblies = new List<ITaskItem>(value);
                }
            }
        }

        [Output]
        public ITaskItem[] AssembliesTargetingX64 {
            get { return assembliesTargetingX64.ToArray(); }
        }

        [Output]
        public ITaskItem[] AssembliesTargetingX86 {
            get { return assembliesTargetingX86.ToArray(); }
        }


        public string[] AssemblyDependencies { get; set; }

        /// <summary>
        /// Gets or sets the flag indicating if the 32-bit test runner should be used.
        /// </summary>
        [Output]
        public bool MustRun32Bit { get; set; }


        public override bool Execute() {
            if (assemblies == null) {
                return true;
            }

            Log.LogMessage(MessageImportance.High, "Building assembly platform architecture list...");

            AssemblyInspector inspector = CreateInspector();

            Dictionary<ITaskItem, string> analyzedAssemblies = new Dictionary<ITaskItem, string>();

            Queue<ITaskItem> scanQueue = new Queue<ITaskItem>(Assemblies);

            List<ITaskItem> failedItems = new List<ITaskItem>();

            while (scanQueue.Count > 0) {
                ITaskItem item = scanQueue.Dequeue();

                string fullPath = item.GetMetadata("FullPath");

                if (PathUtility.HasExtension(item.ItemSpec, allowedAssemblyExtensions) && File.Exists(item.ItemSpec)) {
                    string hash;
                    using (var cryptoProvider = new SHA1CryptoServiceProvider()) {
                        using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1048576)) {
                            hash = BitConverter.ToString(cryptoProvider.ComputeHash(stream));
                        }
                    }

                    if (ShouldAnalyze(analyzedAssemblies, hash, item)) {
                        try {
                            bool checkCiStatus = fullPath.IndexOf("IntegrationTest", StringComparison.OrdinalIgnoreCase) != -1;

                            ProcessorArchitecture[] referenceArchitectures;
                            ImageFileMachine imageFileMachine;
                            bool ciEnabled;
                            string ciCategory;
                            Exception exception;
                            PortableExecutableKinds peKind = inspector.Inspect(item.ItemSpec, checkCiStatus, out referenceArchitectures, out imageFileMachine, out ciEnabled, out ciCategory, out exception);

                            if (checkCiStatus) {
                                item.SetMetadata("CIEnabled", ciEnabled.ToString());

                                if (!string.IsNullOrWhiteSpace(ciCategory)) {
                                    item.SetMetadata("CICategory", ciCategory);
                                }
                            }

                            if ((peKind & PortableExecutableKinds.Required32Bit) != 0) {
                                item.SetMetadata("Platform", "x86");
                                if (!assembliesTargetingX86.Contains(item)) {
                                    assembliesTargetingX86.Add(item);
                                }

                                MustRun32Bit = true;
                            } else {
                                item.SetMetadata("Platform", "x64");
                            }

                            if (referenceArchitectures != null) {
                                SelectRunPlatformConsideringReferencesOfAssembly(referenceArchitectures, item);
                            }

                            // Optimization to reduce boxing for the common case
                            if (peKind == PortableExecutableKinds.ILOnly) {
                                item.SetMetadata("PEKind", "ILOnly");
                            } else if (peKind == PortableExecutableKinds.Required32Bit) {
                                item.SetMetadata("PEKind", "x86");
                            } else {
                                item.SetMetadata("PEKind", peKind.ToString());
                            }

                            if (exception != null) {
                                throw exception;
                            }

                            analyzedAssemblies[item] = hash;
                        } catch (FileLoadException ex) {
                            if (!failedItems.Contains(item)) {
                                Log.LogWarning($"Creating new inspection domain for {item.ItemSpec} due to: {ex.Message}\r\nYou should delete this file or the other file with the same identity to resolve this warning if it was caused by duplicate assemblies.");

                                inspector = CreateInspector();

                                scanQueue.Enqueue(item);

                                // Record this failure so we don't get stuck in a loop
                                failedItems.Add(item);
                            }
                        }
                    }
                } else {
                    failedItems.Add(item);
                    assemblies.Remove(item);
                }
            }

            if (inspectionDomain != null) {
                System.Threading.Tasks.Task.Run(
                    () => {
                        AppDomain.Unload(inspectionDomain);
                        inspectionDomain = null;
                    });
            }

            if (failedItems.Count > 0) {
                foreach (ITaskItem item in failedItems) {
                    Log.LogWarning($"Unable to identify assembly platform for assembly: '{item.GetMetadata("FullPath")}'.");
                }
            }

            return !Log.HasLoggedErrors;
        }

        private void SelectRunPlatformConsideringReferencesOfAssembly(ProcessorArchitecture[] referenceArchitectures, ITaskItem item) {
            foreach (var arch in referenceArchitectures) {
                if (arch == ProcessorArchitecture.X86) {
                    item.SetMetadata("Platform", "x86");

                    Log.LogMessage(MessageImportance.Low, $"Adding {item} to {nameof(AssembliesTargetingX86)} set");
                    if (!assembliesTargetingX86.Contains(item)) {
                        assembliesTargetingX86.Add(item);
                    }
                }

                // Some test assemblies are ILOnly but reference 64-bit executables so we need to
                // run in 64-bit mode for these assemblies
                if (arch == ProcessorArchitecture.Amd64) {
                    Log.LogMessage(MessageImportance.Low, $"Adding {item} to {nameof(AssembliesTargetingX64)} set");
                    assembliesTargetingX64.Add(item);
                }
            }
        }

        private AssemblyInspector CreateInspector() {
            Assembly thisAssembly = typeof(GetAssemblyPlatform).Assembly;

            if (inspectionDomain != null) {
                System.Threading.Tasks.Task.Run(() => AppDomain.Unload(inspectionDomain));
                inspectionDomain = null;
            }

            inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, Path.GetDirectoryName(thisAssembly.Location), null, false);

            var inspector = (AssemblyInspector)inspectionDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, typeof(AssemblyInspector).FullName);
            inspector.AssemblyDependencies = AssemblyDependencies;

            if (init != null) {
                init.Dispose();
            }

            this.init = (InspectionDomainInitializer)inspectionDomain.CreateInstanceFromAndUnwrap(typeof(InspectionDomainInitializer).Assembly.Location, typeof(InspectionDomainInitializer).FullName);
            init.LoadAssembly(Log.GetType().Assembly.Location);
            init.LoadAssembly(BuildEngine.GetType().Assembly.Location);

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


        public class AssemblyPlatformData {
            public ITaskItem[] Assemblies { get; set; }
        }
    }

    internal class AssemblyInspector : MarshalByRefObject {
        private Dictionary<string, string> fileMap;
        private Dictionary<string, ProcessorArchitecture[]> seenAssemblies = new Dictionary<string, ProcessorArchitecture[]>(StringComparer.OrdinalIgnoreCase);

        public string[] AssemblyDependencies { get; set; }
        public object Logger { get; set; }

        private string assemblyLocation;

        private Assembly ReflectionOnlyAssemblyResolveEventHandler(object sender, ResolveEventArgs args) {
            string assembly = Path.Combine(assemblyLocation, string.Concat(new AssemblyName(args.Name).Name, ".dll"));

            if (File.Exists(assembly)) {
                return Assembly.ReflectionOnlyLoadFrom(assembly);
            }

            return null;
        }

        /// <param name="assembly"></param>
        /// <param name="checkCiStatus"></param>
        /// <param name="referenceArchitectures">
        /// The architectures this assembly has references to Islamic, Romanesque, Bauhaus
        /// etc.
        /// </param>
        /// <param name="imageFileMachine"></param>
        /// <param name="ciEnabled"></param>
        /// <param name="exception"></param>
        public PortableExecutableKinds Inspect(string assembly, bool checkCiStatus, out ProcessorArchitecture[] referenceArchitectures, out ImageFileMachine imageFileMachine, out bool ciEnabled, out string ciCategory, out Exception exception) {
            referenceArchitectures = null;
            exception = null;

            if (fileMap == null && AssemblyDependencies != null) {
                Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dependency in AssemblyDependencies) {
                    string key = Path.GetFileName(dependency);
                    if (!dictionary.ContainsKey(key)) {
                        dictionary.Add(key, dependency);
                    } else {
                        // This generates many thousands of entries that no developer will ever do anything about so give up and log at a low level
                        Log("Already seen file: " + dependency);
                    }
                }

                fileMap = dictionary;
            }

            Assembly asm = Assembly.ReflectionOnlyLoadFrom(assembly);

            PortableExecutableKinds peKind;
            asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);

            if (checkCiStatus) {
                assemblyLocation = Path.GetDirectoryName(asm.Location);
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ReflectionOnlyAssemblyResolveEventHandler;

                Tuple<bool, string> ciProperties = DetermineCiStatus(asm.CustomAttributes);
                ciEnabled = ciProperties.Item1;
                ciCategory = ciProperties.Item2;
            } else {
                ciEnabled = false;
                ciCategory = null;
            }

            try {
                var references = asm.GetReferencedAssemblies();

                if (fileMap != null) {
                    referenceArchitectures = GetReferenceArchitectures(references);
                }
            } catch (Exception ex) {
                exception = ex;
            }

            return peKind;
        }

        internal static Tuple<bool, string> DetermineCiStatus(IEnumerable<CustomAttributeData> customAttributeData) {
            bool ciEnabled = false;
            string ciCategory = null;

            foreach (CustomAttributeData attributeData in customAttributeData) {
                if (attributeData.AttributeType.Name.Equals("AssemblyMetadataAttribute")) {
                    if (!ciEnabled) {
                        if (attributeData.ConstructorArguments[0].Value.ToString().Equals("CIEnabled", StringComparison.OrdinalIgnoreCase)) {
                            ciEnabled = attributeData.ConstructorArguments[1].Value.ToString().Equals("true", StringComparison.OrdinalIgnoreCase);
                            continue;
                        }
                    }

                    if (ciCategory == null) {
                        if (attributeData.ConstructorArguments[0].Value.ToString().Equals("ciCategory", StringComparison.OrdinalIgnoreCase)) {
                            ciCategory = attributeData.ConstructorArguments[1].Value.ToString();
                        }
                    }

                    if (ciEnabled && ciCategory != null) {
                        break;
                    }
                }
            }

            return new Tuple<bool, string>(ciEnabled, ciCategory);
        }

        private void LogWarning(string message) {
            var logger = Logger as TaskLoggingHelper;
            if (logger != null) {
                logger.LogWarning(message);
            }
        }

        private void Log(string message) {
            var logger = Logger as TaskLoggingHelper;
            if (logger != null) {
                logger.LogMessage(MessageImportance.Low, message);
            }
        }

        private ProcessorArchitecture[] GetReferenceArchitectures(AssemblyName[] references) {
            List<ProcessorArchitecture> collector = new List<ProcessorArchitecture>();

            foreach (var reference in references) {
                try {
                    ProcessorArchitecture[] arch;
                    if (seenAssemblies.TryGetValue(reference.Name, out arch)) {
                        if (arch != null) {
                            collector.AddRange(arch);
                        }

                        continue;
                    }

                    FindReference(reference, collector);
                } catch (Exception ex) {
                    Log("Exception while processing reference list. " + ex);
                }
            }

            return collector.ToArray();
        }

        private void FindReference(AssemblyName reference, List<ProcessorArchitecture> collector) {
            var names = new string[] {
                reference.Name + ".dll",
                reference.Name + ".exe",
                reference.Name + ".winmd",
            };

            bool addNullSentinel = true;
            foreach (var name in names) {

                ProcessorArchitecture[] referenceArchitecture;
                if (DiscoverReferenceArchitecture(name, out referenceArchitecture)) {
                    if (referenceArchitecture != null) {
                        seenAssemblies.Add(reference.Name, referenceArchitecture);
                        collector.AddRange(referenceArchitecture);
                        addNullSentinel = false;
                        break;
                    }
                }
            }

            if (addNullSentinel) {
                // Record we never found this assembly to avoid future lookups
                seenAssemblies.Add(reference.Name, null);
            }
        }

        private bool DiscoverReferenceArchitecture(string fileName, out ProcessorArchitecture[] architectures) {
            string locationOnDisk;
            if (fileMap.TryGetValue(fileName, out locationOnDisk)) {
                var assemblyName = AssemblyName.GetAssemblyName(locationOnDisk);

                if (assemblyName.ProcessorArchitecture != ProcessorArchitecture.MSIL) {
                    architectures = new ProcessorArchitecture[] { assemblyName.ProcessorArchitecture };
                    return true;
                }
            }

            architectures = null;
            return locationOnDisk != null;
        }
    }
}
