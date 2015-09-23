using System;
using System.Runtime.Serialization;

namespace Aderant.Build {

    [Serializable]
    internal class BuildNotFoundException : Exception {

        public BuildNotFoundException(string message) : base(message) {
        }

        protected BuildNotFoundException(SerializationInfo info, StreamingContext context) {
        }
    }
}