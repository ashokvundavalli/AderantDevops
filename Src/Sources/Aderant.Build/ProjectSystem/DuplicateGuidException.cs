using System;
using System.Runtime.Serialization;

namespace Aderant.Build.ProjectSystem {
    [Serializable]
    public class DuplicateGuidException : Exception {

        public DuplicateGuidException(Guid guid, string message)
            : base(message) {
            this.Guid = guid;
        }

        protected DuplicateGuidException(SerializationInfo info, StreamingContext context) {
        }

        /// <summary>
        /// Gets the unique identifier.
        /// </summary>
        public Guid Guid { get; private set; }
    }
}