using System;
using System.Runtime.Serialization;

namespace Aderant.Build.Packaging {
    [Serializable]
    internal class InvalidPrereleaseLabel : Exception {
        public InvalidPrereleaseLabel(string message) : base(message) {
        }

        protected InvalidPrereleaseLabel(SerializationInfo info,
            StreamingContext context) {
        }
    }
}