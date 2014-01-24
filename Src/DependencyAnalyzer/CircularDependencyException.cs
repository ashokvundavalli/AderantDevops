using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace DependencyAnalyzer {
    /// <summary>
    /// Occurs when circular references are detected
    /// </summary>
    [Serializable]
    public class CircularDependencyException : Exception {
        /// <summary>
        /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
        /// </summary>
        public CircularDependencyException() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
        /// </summary>
        /// <param name="dependencies">The dependencies.</param>
        public CircularDependencyException(string dependencies) : base(dependencies) {
        }

        protected CircularDependencyException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
        }
    }
}