using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyResolver {
        IModuleProvider ModuleFactory { get; set; }
        bool? ReplicationExplicitlyDisabled { get; set; }

        IEnumerable<IDependencyRequirement> GetDependencyRequirements(ResolverRequest resolverRequest, ExpertModule module);

        void Resolve(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken = default(CancellationToken));
    }
}