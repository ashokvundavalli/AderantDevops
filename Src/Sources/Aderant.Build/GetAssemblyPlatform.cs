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
                var inspectionDomain = AppDomain.CreateDomain("Inspection Domain", null, Path.GetDirectoryName(this.GetType().Assembly.Location), null, false);

                foreach (var item in Assemblies) {
                    if ((item.ItemSpec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || item.ItemSpec.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) && File.Exists(item.ItemSpec)) {
                        inspectionDomain.SetData("Assembly", item.ItemSpec);

                        inspectionDomain.DoCallBack(Inspect);

                        var peKind = (PortableExecutableKinds) inspectionDomain.GetData("PortableExecutableKinds");
                        if (peKind.HasFlag(PortableExecutableKinds.Required32Bit)) {
                            MustRun32Bit = true;
                        }
                        item.SetMetadata("Platform", peKind.ToString());
                        
                    }
                }

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
}