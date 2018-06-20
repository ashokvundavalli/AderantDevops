﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Tasks.BuildTime.ProjectDependencyAnalyzer {
    /// <summary>
    /// Represents a reference to a module.
    /// </summary>
    [DebuggerDisplay("Module Reference: {Name}")]
    internal class ModuleRef : IDependencyRef {
        private readonly ExpertModule module;

        public ModuleRef(ExpertModule module) {
            this.module = module;
        }

        public string Name => module.Name;
        

        public ICollection<IDependencyRef> DependsOn {
            get { return module.DependsOn; }
            set { module.DependsOn = value; }
        }

        public void Accept(GraphVisitorBase visitor, StreamWriter outputFile) {
            (visitor as GraphVisitor).Visit(this, outputFile);

            foreach (IDependencyRef dep in module.DependsOn) {
                dep.Accept(visitor, outputFile);
            }
        }

        public bool Equals(IDependencyRef dependency) {
            var moduleReference = dependency as ModuleRef;
            if (moduleReference != null && string.Equals(Name, moduleReference.Name, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            return false;
        }

        public override int GetHashCode() {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
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
            return Equals((ModuleRef)obj);
        }
    }
}