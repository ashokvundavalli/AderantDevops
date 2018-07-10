using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Aderant.Build.DependencyAnalyzer.Model {
    [DebuggerDisplay("ProjectReference: {Name}")]
    internal class ProjectRef : IDependencyRef {

        public ProjectRef(string reference) {
            this.Name = reference;
        }

        public ProjectRef(Guid reference, string name) {
            ProjectGuid = reference;
            Name = name;
        }

        public string Name { get; private set; }

        public IReadOnlyCollection<IDependencyRef> DependsOn {
            get { return null; }
        }

        public Guid? ProjectGuid { get; set; }

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

            return Equals((ProjectRef)obj);
        }

        public override int GetHashCode() {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name) ^ ProjectGuid.GetHashCode();
        }
    }
}
