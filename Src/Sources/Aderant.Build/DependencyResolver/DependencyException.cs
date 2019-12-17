using System;
using System.Runtime.Serialization;

namespace Aderant.Build.DependencyResolver {
    [Serializable]
    internal class DependencyException : Exception {
        public DependencyException(string message) : base(message) {
        }

        protected DependencyException(SerializationInfo info, StreamingContext context) {
        }
    }
}