using System;
using System.Reflection;

namespace Aderant.Build {
    [Serializable]
    public class AssemblyVersionInspector : MarshalByRefObject {
    
        private string image;
        private Assembly asm;
        private bool loadImpossible;

        /// <summary>
        /// Gets the kind of the assembly.
        /// </summary>
        /// <returns></returns>
        public PortableExecutableKinds GetAssemblyKind() {
            if (CheckAssemblyToInspect()) {

                PortableExecutableKinds peKind;
                ImageFileMachine imageFileMachine;
                asm.ManifestModule.GetPEKind(out peKind, out imageFileMachine);

                return peKind;
            }

            return PortableExecutableKinds.NotAPortableExecutableImage;
        }

        public string Image {
            get { return image; }
            set {
                image = value;
                loadImpossible = false;

                try {
                    asm = Assembly.ReflectionOnlyLoadFrom(image);
                } catch (BadImageFormatException) {
                    // The image is not a portable image so we cannot load it
                    loadImpossible = true;
                }
            }
        }

        public string GetAssemblyVersion() {
            if (CheckAssemblyToInspect()) {

                Version version = asm.GetName().Version;

                return version.ToString();
            }
            return null;
        }

        private bool CheckAssemblyToInspect() {
            if (!loadImpossible && image == null) {
                throw new ArgumentException("No assembly to inspect is specified.");
            }

            return !loadImpossible;
        }
    }
}