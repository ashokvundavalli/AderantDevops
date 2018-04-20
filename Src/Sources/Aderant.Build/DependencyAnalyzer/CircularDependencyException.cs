using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Aderant.Build.DependencyAnalyzer {

    /// <summary>
    /// Occurs when circular references are detected
    /// </summary>
    [Serializable]
    public class CircularDependencyException : Exception {
        private string[] conflicts;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
        /// </summary>
        public CircularDependencyException() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularDependencyException"/> class.
        /// </summary>
        /// <param name="message">The dependencies.</param>
        public CircularDependencyException(string message) : base(message) {
        }

        protected CircularDependencyException(SerializationInfo info, StreamingContext context)
            : base(info, context) {
        }

        public CircularDependencyException(IEnumerable<string> conflicts) : this("There is a circular dependency between the following: " + string.Join(", ", conflicts.ToArray())) {
            this.conflicts = conflicts.ToArray();
        }

        public string[] Conflicts {
            get { return conflicts; }
        }
    }
}