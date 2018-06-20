using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.Model {
    [DebuggerDisplay("DirectoryNode: {Name}")]
    internal sealed class DirectoryNode : IDependencyRef {
        private ICollection<IDependencyRef> dependsOn;
        
        public bool IsCompletion { get; set; }
        public string ModuleName { get; set; }

        public DirectoryNode(string name, bool isCompletion) {
            ModuleName = name;
            IsCompletion = isCompletion;
            Name = CreateName(name, isCompletion);
        }

        public static string CreateName(string name, bool isCompletion) {
            if (isCompletion) {
                name += ".Completion";
            } else {
                name += ".Initialize";
            }

            return name;
        }

        public bool Equals(IDependencyRef other) {
            return Equals((object)other);
        }

        private bool Equals(DirectoryNode other) {
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            return obj is DirectoryNode && Equals((DirectoryNode)obj);
        }

        public override int GetHashCode() {
            return (Name != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Name) : 0);
        }

        public string Name { get; set; }

        public ICollection<IDependencyRef> DependsOn {
            get { return dependsOn ?? (dependsOn = new HashSet<IDependencyRef>()); }
            set { dependsOn = value; }
        }

        public void Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
        }

        public void AddDependency(IDependencyRef dependency) {
            if (dependsOn == null) {
                dependsOn = new HashSet<IDependencyRef>();
            }

            dependsOn.Add(dependency);
        }
    }
}