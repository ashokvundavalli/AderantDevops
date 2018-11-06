using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        /// <param name="cancellationToken">The cancellation token.</param>
        public void ResolveDependencies(ResolverRequest resolverRequest, CancellationToken cancellationToken = default(CancellationToken)) {
            resolverRequest.Modules.ToList().ForEach(s => logger.Info($"Resolving dependencies for {s.Name}"));

            List<IDependencyRequirement> requirements = new List<IDependencyRequirement>();

            GatherRequirements(resolverRequest, requirements);

            AddAlwaysRequired(resolverRequest, requirements);

            //TODO: Restore this behaviour
            //RemoveRequirementsBeingBuilt(resolverRequest, requirements);

            List<IDependencyRequirement> distinctRequirements = requirements.Distinct().ToList();

            logger.Info("Required inputs: {0}", string.Join(",", distinctRequirements.Select(s => s.Name)));
            
            foreach (IDependencyResolver resolver in resolvers) {
                if (resolver.ReplicationExplicitlyDisabled != null) {
                    resolverRequest.ReplicationExplicitlyDisabled = true;
                }

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

            if (resolverRequest.ModuleFactory != null) {
                module = resolverRequest.ModuleFactory.GetModule("Aderant.Build.Analyzer");
            }

            IDependencyRequirement analyzer = requirements.FirstOrDefault(r => string.Equals(r.Name, "Aderant.Build.Analyzer"));

            if (analyzer != null) {
                requirements.Remove(analyzer);
            }

            IDependencyRequirement requirement;
            if (module != null) {
                requirement = DependencyRequirement.Create(module);
            } else {
                requirement = DependencyRequirement.Create("Aderant.Build.Analyzer", Constants.MainDependencyGroup);
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
                        loopRequirements.AddRange(dependencyRequirements.ToList());
                    }
                }

                resolverRequest.AssociateRequirements(module, loopRequirements);
                requirements.AddRange(loopRequirements);
            }

            //ValidateRequirements(requirements);
        }

        private void ValidateRequirements(List<IDependencyRequirement> requirements) {
            StringBuilder sb = new StringBuilder();

            IEnumerable<IGrouping<string, IDependencyRequirement>> groupings = requirements.GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groupings) {
                List<IDependencyRequirement> requirementsWithConstraints = new List<IDependencyRequirement>();

                foreach (var requirement in group) {
                    if (requirement.VersionRequirement != null) {
                        requirementsWithConstraints.Add(requirement);
                    }
                }

                var unique = requirementsWithConstraints
                    .GroupBy(req => GetExpression(req), StringComparer.OrdinalIgnoreCase)
                    .Select(s => s.First());

                if (unique.Count() > 1) {
                    var @join = string.Join(Environment.NewLine, unique.Select(s => $"{s.VersionRequirement.ConstraintExpression} in {s.VersionRequirement.OriginatingFile ?? s.Location}"));
                    sb.AppendLine($"The dependency {group.Key} in has incompatible version constraints: {join}");
                }
            }

            if (sb.Length > 0) {
                throw new InvalidOperationException(sb.ToString());
            }
        }

        private string GetExpression(IDependencyRequirement dependencyRequirement) {
            if (dependencyRequirement.VersionRequirement != null) {
                if (string.IsNullOrWhiteSpace(dependencyRequirement.VersionRequirement.ConstraintExpression)) {
                    return null;
                }
                return dependencyRequirement.VersionRequirement.ConstraintExpression;
            }

            return null;
        }

        private static void RemoveRequirementsBeingBuilt(ResolverRequest resolverRequest, List<IDependencyRequirement> requirements) {
            IEnumerable<ExpertModule> modules = resolverRequest.GetModulesInBuild();
            foreach (var module in modules) {
                requirements.RemoveAll(req => string.Equals(req.Name, module.Name, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
