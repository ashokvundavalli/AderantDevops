﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Aderant.Build.Model;
using Aderant.Build.ProjectSystem.References;

namespace Aderant.Build.DependencyAnalyzer.Model {
    public abstract class AbstractArtifact : IArtifact {
        private List<IResolvedDependency> resolvedDependencies = new List<IResolvedDependency>();
        private List<IUnresolvedDependency> unresolvedDependencies = new List<IUnresolvedDependency>();

        public virtual IReadOnlyCollection<IDependable> GetDependencies() {
            return resolvedDependencies.Select(d => d.ResolvedReference).ToList();
        }

        public virtual void AddResolvedDependency(IUnresolvedDependency unresolvedDependency, IDependable dependable) {
            IResolvedDependency resolvedDependency;

            if (unresolvedDependency == null) {
                resolvedDependency = ResolvedDependency.Create(this, dependable);
            } else {
                resolvedDependency = ResolvedDependency.Create(this, dependable, unresolvedDependency);
            }

            resolvedDependencies.Add(resolvedDependency);

            if (unresolvedDependency != null) {
                unresolvedDependencies.Remove(unresolvedDependency);
            }
        }

        public abstract string Id { get; }
    }

    [DebuggerDisplay("DirectoryNode: {" + nameof(Id) + "}")]
    internal sealed class DirectoryNode : AbstractArtifact {

        [Obsolete]
        public DirectoryNode(string name, bool isCompletion) {

        }

        public DirectoryNode(string id, string directory, bool isAfterTargets) {
            ModuleName = id;
            IsAfterTargets = isAfterTargets;
            Directory = directory;
            Id = CreateName(id, isAfterTargets);
        }

        public override string Id { get; }

        public bool IsAfterTargets { get; }

        public string ModuleName { get; }
        public string Directory { get; set; }

        public static string CreateName(string name, bool isCompletion) {
            if (isCompletion) {
                name += ".AfterBuild";
            } else {
                name += ".BeforeBuild";
            }

            return name;
        }
    }
}
