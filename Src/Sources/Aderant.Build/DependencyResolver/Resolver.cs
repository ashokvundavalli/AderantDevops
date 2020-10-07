using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;

namespace Aderant.Build.DependencyResolver {
    internal class Resolver {
        private readonly ILogger logger;
        private List<IDependencyResolver> resolvers = new List<IDependencyResolver>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Resolver"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="resolvers">The resolvers.</param>
        public Resolver(ILogger logger, params IDependencyResolver[] resolvers) {
            this.logger = logger;
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

            AddAlwaysRequired(resolverRequest, requirements);

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
        }

        private void AddAlwaysRequired(ResolverRequest resolverRequest, List<IDependencyRequirement> requirements) {
            if (resolverRequest.Modules.All(m => string.Equals(m.Name, "Build.Infrastructure"))) {
                return;
            }

            ExpertModule module = null;

            const string buildAnalyzer = "Aderant.Build.Analyzer";

            if (resolverRequest.ModuleFactory != null) {
                module = resolverRequest.ModuleFactory.GetModule(buildAnalyzer);
            }

            IDependencyRequirement analyzer = requirements.FirstOrDefault(r => string.Equals(r.Name, buildAnalyzer));

            if (analyzer != null) {
                requirements.Remove(analyzer);
            }

            IDependencyRequirement requirement;
            if (module != null) {
                requirement = DependencyRequirement.Create(module);
            } else {
                requirement = DependencyRequirement.Create(buildAnalyzer, Constants.MainDependencyGroup);
            }

            requirement.ReplaceVersionConstraint = true;
            requirement.ReplicateToDependencies = false;

            requirements.Add(requirement);
        }

        private void GatherRequirements(ResolverRequest resolverRequest, List<IDependencyRequirement> requirements) {
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
            }
        }
    }
}
