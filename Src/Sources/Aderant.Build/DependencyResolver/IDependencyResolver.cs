using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver {
    internal interface IDependencyResolver {
        IModuleProvider ModuleFactory { get; set; }

        /// <summary>
        /// Determines whether or not to enable Verbose logging for Paket.
        /// </summary>>
        bool EnableVerboseLogging { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether replication explicitly disabled. 
        /// Modules can tell the build system to not replicate packages to the dependencies folder via DependencyReplication=false in the DependencyManifest.xml
        /// </summary>>
        bool? ReplicationExplicitlyDisabled { get; set; }

        IEnumerable<IDependencyRequirement> GetDependencyRequirements(ResolverRequest resolverRequest, ExpertModule module);

        void Resolve(ResolverRequest resolverRequest, IEnumerable<IDependencyRequirement> requirements, CancellationToken cancellationToken = default(CancellationToken));
    }
}
