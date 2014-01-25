using System.IO;
using Microsoft.Build.Framework;

namespace Build.Tasks {

    public class GetAssemblyPlatform : Microsoft.Build.Utilities.Task {
        public virtual bool Success { get; set; }

        [Output]
        public Microsoft.Build.Framework.ITaskItem[] Assemblies { get; set; }

        [Output]
        public bool MustRun32Bit { get; set; }

        public override bool Execute() {
            if (Assemblies != null) {
                var inspectionDomain = System.AppDomain.CreateDomain("InspectionDomain", null, Path.GetDirectoryName(this.GetType().Assembly.Location), null, false);

                foreach (var item in Assemblies) {
                    if ((item.ItemSpec.EndsWith(".dll") || item.ItemSpec.EndsWith(".exe")) && System.IO.File.Exists(item.ItemSpec)) {
                        inspectionDomain.SetData("Assembly", item.ItemSpec);

                        inspectionDomain.DoCallBack(new System.CrossAppDomainDelegate(Inspect));

                        var peKind = (System.Reflection.PortableExecutableKinds)inspectionDomain.GetData("PortableExecutableKinds");
                        if (peKind.HasFlag(System.Reflection.PortableExecutableKinds.Required32Bit)) {
                            item.SetMetadata("Configuration", "Required32Bit");
                            MustRun32Bit = true;
                        } else {
                            item.SetMetadata("Configuration", "ILOnly");
                        }

                    }
                }

                System.AppDomain.Unload(inspectionDomain);
            }

            return true;
        }

        private static void Inspect() {
            string assembly = System.AppDomain.CurrentDomain.GetData("Assembly") as string;

            var asm = System.Reflection.Assembly.ReflectionOnlyLoadFrom(assembly);

            System.Reflection.PortableExecutableKinds peKind;
            System.Reflection.ImageFileMachine imageFileMachine;
            asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);
            System.AppDomain.CurrentDomain.SetData("PortableExecutableKinds", peKind);
        }
    }
}
