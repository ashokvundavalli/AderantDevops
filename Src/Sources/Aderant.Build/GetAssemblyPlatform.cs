using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Aderant.Build {
    public class GetAssemblyPlatform : Microsoft.Build.Utilities.Task {
        public virtual bool Success { get; set; }

        [Output]
        public ITaskItem[] Assemblies { get; set; }

        [Output]
        public bool MustRun32Bit { get; set; }

        public override bool Execute() {
            if (Assemblies != null) {
                var thisAssembly = GetType().Assembly;

                var inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, Path.GetDirectoryName(thisAssembly.Location), null, false);

                var inspector = (AssemblyInspector)inspectionDomain.CreateInstanceAndUnwrap(thisAssembly.FullName, typeof(AssemblyInspector).FullName);

                foreach (var item in Assemblies) {
                    if ((item.ItemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || item.ItemSpec.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) && File.Exists(item.ItemSpec)) {
                        PortableExecutableKinds peKind = inspector.GetAssemblyKind(item.ItemSpec);
                        
                        if (peKind.HasFlag(PortableExecutableKinds.Required32Bit)) {
                            MustRun32Bit = true;
                        }

                        item.SetMetadata("Platform", peKind.ToString());
                    }
                }

                inspector = null;
                AppDomain.Unload(inspectionDomain);
                GC.Collect();
            }

            return true;
        }

        private static void Inspect() {
            string assembly = AppDomain.CurrentDomain.GetData("Assembly") as string;

            var asm = Assembly.ReflectionOnlyLoadFrom(assembly);

            PortableExecutableKinds peKind;
            ImageFileMachine imageFileMachine;
            asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);
            
            AppDomain.CurrentDomain.SetData("PortableExecutableKinds", peKind);
        }
    }

    [Serializable]
    internal class AssemblyInspector : MarshalByRefObject {

        public PortableExecutableKinds GetAssemblyKind(string assembly) {
            var asm = Assembly.ReflectionOnlyLoadFrom(assembly);

            PortableExecutableKinds peKind;
            ImageFileMachine imageFileMachine;
            asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);

            return peKind;
        }
        
    }
}