using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.Model {

    [DebuggerDisplay("AssemblyReference: {Name}")]
    internal class AssemblyRef : IDependencyRef {

        public AssemblyRef(string dependency, string referenceHintPath) {
            Name = dependency;
            ReferenceHintPath = referenceHintPath;
        }

        public AssemblyRef(string dependency)
            : this(dependency, null) {
        }

        public string ReferenceHintPath { get; }

        public string Name { get; }

        public IReadOnlyCollection<IDependencyRef> DependsOn {
            get { return null; }
        }

        public void AddDependency(IDependencyRef dependency) {
        }

        public void Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
            (visitor as GraphVisitor).Visit(this, outputFile);
        }

        public bool Equals(IDependencyRef other) {
            if (ReferenceEquals(null, other)) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj.GetType() != GetType()) {
                return false;
            }

            return Equals((AssemblyRef)obj);
        }

        public override int GetHashCode() {
            return (Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0);
        }
    }
}
