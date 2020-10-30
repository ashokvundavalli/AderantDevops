﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;

namespace Aderant.Build.DependencyResolver {
    internal class Resolver {
        private readonly ILogger logger;
        private List<IDependencyResolver> resolvers = new List<IDependencyResolver>();
        private readonly IFileSystem2 fileSystem;

        /// <summary>
        /// Initializes a new instance of the <see cref="Resolver"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="resolvers">The resolvers.</param>
        public Resolver(ILogger logger, params IDependencyResolver[] resolvers) : this(logger, new PhysicalFileSystem(), resolvers) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Resolver"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="fileSystem"></param>
        /// <param name="resolvers">The resolvers.</param>
        public Resolver(ILogger logger, IFileSystem2 fileSystem, params IDependencyResolver[] resolvers) {
            this.logger = logger;
            this.fileSystem = fileSystem;

            foreach (var resolver in resolvers) {
                this.resolvers.Add(resolver);
            }
        }

        /// <summary>
        /// Resolves the dependencies for the given request.
        /// </summary>
        /// <param name="resolverRequest">The resolver request.</param>
        /// <param name="enableVerboseLogging">Emit verbose details from the resolver.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void ResolveDependencies(ResolverRequest resolverRequest, bool enableVerboseLogging = false, CancellationToken cancellationToken = default(CancellationToken)) {
            List<IDependencyRequirement> requirements = new List<IDependencyRequirement>();

            GatherRequirements(resolverRequest, requirements);

            if (!resolverRequest.ReadOnly) {
                AddAlwaysRequired(resolverRequest, requirements);
            }

            List<IDependencyRequirement> distinctRequirements = requirements.Distinct().ToList();

            logger.Info("Required inputs: {0}", string.Join(",", distinctRequirements.Select(s => s.Name)));

            foreach (IDependencyResolver resolver in resolvers) {
                if (resolver.ReplicationExplicitlyDisabled != null) {
                    resolverRequest.ReplicationExplicitlyDisabled = resolver.ReplicationExplicitlyDisabled.Value;
                }

                resolver.EnableVerboseLogging = enableVerboseLogging;
                resolver.Resolve(resolverRequest, distinctRequirements, cancellationToken);

                IEnumerable<IDependencyRequirement> unresolved = resolverRequest.GetRequirementsByType(DependencyState.Unresolved);
                distinctRequirements = unresolved.ToList();
            }

            if (distinctRequirements.Any()) {
                throw new InvalidOperationException($"The following requirements could not be resolved: {string.Join(", ", distinctRequirements.Select(s => s.Name))}");
            }

            string dependenciesDirectory = null;
            try {
                dependenciesDirectory = Path.Combine(resolverRequest.GetDependenciesDirectory(requirements[0]), Constants.PaketLock);
            } catch (InvalidOperationException) {
                // No assigned dependencies directory.
            }

            if (!string.IsNullOrWhiteSpace(dependenciesDirectory)) {
                PaketLockOperations paketLockOperations = new PaketLockOperations(resolverRequest, dependenciesDirectory, fileSystem);
                paketLockOperations.SaveLockFileForModules();
            }
        }

        private void AddAlwaysRequired(ResolverRequest resolverRequest, List<IDependencyRequirement> requirements) {
            const string buildAnalyzer = "Aderant.Build.Analyzer";

            if (resolverRequest.Modules.All(m => string.Equals(m.Name, "Build.Infrastructure"))) {
                return;
            }

            ExpertModule module = null;

            if (resolverRequest.ModuleFactory != null) {
                module = resolverRequest.ModuleFactory.GetModule(buildAnalyzer);
            }

            var analyzer = requirements.FirstOrDefault(r => string.Equals(r.Name, buildAnalyzer));

            if (analyzer != null) {
                requirements.Remove(analyzer);
            }

            IDependencyRequirement newRequirement;
            if (module != null) {
                newRequirement = DependencyRequirement.Create(module);
            } else {
                newRequirement = DependencyRequirement.Create(buildAnalyzer, Constants.MainDependencyGroup);
                if (analyzer != null) {
                    newRequirement.VersionRequirement = analyzer.VersionRequirement;
                }
            }

            newRequirement.ReplaceVersionConstraint = true;
            newRequirement.ReplicateToDependencies = false;

            requirements.Add(newRequirement);
        }

        internal void GatherRequirements(ResolverRequest resolverRequest, List<IDependencyRequirement> requirements) {
            foreach (ExpertModule module in resolverRequest.Modules) {
                List<IDependencyRequirement> loopRequirements = new List<IDependencyRequirement>();

                foreach (IDependencyResolver resolver in resolvers) {
                    if (resolver.ModuleFactory == null) {
                        resolver.ModuleFactory = resolverRequest.ModuleFactory;
                    }

                    IEnumerable<IDependencyRequirement> dependencyRequirements = resolver.GetDependencyRequirements(resolverRequest, module);

                    if (dependencyRequirements != null) {
                        loopRequirements.AddRange(dependencyRequirements);
                    }
                }

                resolverRequest.AssociateRequirements(module, loopRequirements);
                requirements.AddRange(loopRequirements);

                module.DependencyRequirements = loopRequirements;

                // Ensure the Build Analyzer is always present.
                AddAlwaysRequired(resolverRequest, module.DependencyRequirements);
            }
        }
    }
}
