using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.Model {
    [DebuggerDisplay("AssemblyReference: {Name} ({dependencyType})")]
    internal class AssemblyRef : IDependencyRef {

        private readonly DependencyType dependencyType;

        public AssemblyRef(string reference)
            : this(reference, DependencyType.Unknown) {
        }

        public AssemblyRef(string dependency, DependencyType dependencyType) {
            this.Name = dependency;
            this.dependencyType = dependencyType;
        }

        public string Name { get; }
        public ICollection<IDependencyRef> DependsOn {
            get { return null; }
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

            if (obj.GetType() != this.GetType()) {
                return false;
            }

            return Equals((AssemblyRef)obj);
        }

        public override int GetHashCode() {
            return (Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0);
        }
    }
}