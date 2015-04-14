using System;

namespace Aderant.Build {

    [Serializable]
    internal class DuplicateModuleInManifestException : Exception {

        public DuplicateModuleInManifestException(string message) : base(message) {
            
        }
    }
}