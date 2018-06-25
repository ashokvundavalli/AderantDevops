using System;
using System.Runtime.Serialization;

namespace Aderant.Build.DependencyAnalyzer {
    [Serializable]
    internal class AmbiguousNameException : Exception {
        public AmbiguousNameException(string message)
            : base(message) {
        }

        protected AmbiguousNameException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
        }
    }
}
