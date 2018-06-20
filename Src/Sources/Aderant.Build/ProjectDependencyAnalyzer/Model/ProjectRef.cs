using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer {
    [DebuggerDisplay("ProjectReference: {Name}")]
    internal class ProjectRef : IDependencyRef {
        
        private bool isResolved;

        public ProjectRef(string reference) {
            this.Name = reference;
        }

        public string Name { get; private set; }
        public ICollection<IDependencyRef> DependsOn { get; set; }

        public void Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
            (visitor as GraphVisitor).Visit(this, outputFile);
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

        public bool Resolve(List<VisualStudioProject> projectVertices) {
            if (isResolved) {
                return true;
            }

            VisualStudioProject project = projectVertices.FirstOrDefault(f => f.ProjectGuid == ProjectGuid);
            if (project != null) {
                Name = project.AssemblyName;
                this.isResolved = true;
            }

            return false;
        }
    }
}