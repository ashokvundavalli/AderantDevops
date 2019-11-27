using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver {
    internal class ResolverRequest {
        internal List<DependencyState<IDependencyRequirement>> dependencies = new List<DependencyState<IDependencyRequirement>>();
        private readonly List<ModuleState<ExpertModule>> modules = new List<ModuleState<ExpertModule>>();
        private string dependenciesDirectory;
        private bool requiresThirdPartyReplication;
        private Dictionary<string, HashSet<string>> restrictions = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        public ILogger Logger { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolverRequest"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="modules">The modules.</param>
        public ResolverRequest(ILogger logger, params ExpertModule[] modules) {
            this.Logger = logger;

            if (modules != null) {
                var sortedModules = new SortedSet<ExpertModule>(modules);
                this.modules.AddRange(sortedModules.Select(m => new ModuleState<ExpertModule>(m)));
            }
        }

        /// <summary>
        /// Gets the modules in this request.
        /// </summary>
        public IEnumerable<ExpertModule> Modules {
            get { return modules.Select(s => s.Item).ToList(); }
        }

        public virtual IModuleProvider ModuleFactory { get; set; }

        /// <summary>
        /// Gets a value determining if third party packages should be replicated.
        /// </summary>
        public bool RequiresThirdPartyReplication {
            get { return requiresThirdPartyReplication || modules.Any(s => s.RequiresThirdPartyReplication) || modules.Count > 1; }
            set { requiresThirdPartyReplication = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to blat everything and force a clean refresh of every package.
        /// </summary>
        /// <value>
        ///   <c>true</c> if force; otherwise, <c>false</c>.
        /// </value>
        public bool Force { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the dependencies should be updated.
        /// </summary>
        /// <value>
        ///   <c>true</c> if update; otherwise, <c>false</c>.
        /// </value>
        public bool Update { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether replication explicitly disabled.
        /// Modules can tell the build system to not replicate packages to the dependencies folder via DependencyReplication=false in the DependencyManifest.xml
        /// </summary>
        public bool ReplicationExplicitlyDisabled { get; set; }

        public bool ValidatePackageConstraints { get; set; }

        /// <summary>
        /// Sets the dependencies directory to place dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        public void SetDependenciesDirectory(string directory) {
            dependenciesDirectory = directory;
        }

        public virtual string GetModuleDirectory(ExpertModule module) {
            return module.FullPath ?? string.Empty;
        }

        public virtual string GetDependenciesDirectory(IDependencyRequirement requirement, bool replicationDisabled = false) {
            if (!string.IsNullOrWhiteSpace(dependenciesDirectory)) {
                return dependenciesDirectory;
            }

            ExpertModule module = GetOrAdd(requirement).Module;
            if (module == null) {
                throw new InvalidOperationException(string.Format("Resolver error. Unable to determine the directory to place requirement: {0} into.", requirement.Name));
            }

            if (replicationDisabled) {
                return GetModuleDirectory(module);
            }

            return Path.Combine(GetModuleDirectory(module), "Dependencies");
        }

        public virtual void AddModule(string fullPath) {
            Logger.Info($"Adding module {fullPath}", null);

            if (!Path.IsPathRooted(fullPath)) {
                throw new InvalidOperationException("Path must be rooted");
            }

            fullPath = PathUtility.TrimTrailingSlashes(fullPath);

            foreach (var entry in modules) {
                if (string.Equals(entry.Item.FullPath, fullPath, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            }

            string name = PathUtility.GetFileName(fullPath);

            ExpertModule resolvedModule = null;
            if (ModuleFactory != null) {
                resolvedModule = ModuleFactory.GetModule(name);
            }

            if (resolvedModule == null) {
                resolvedModule = new ExpertModule(name);
            }

            resolvedModule.FullPath = fullPath;

            modules.Add(new ModuleState<ExpertModule>(resolvedModule) { IsInBuildChain = true, RequiresThirdPartyReplication = true });
        }

        public virtual void Unresolved(IDependencyRequirement requirement, object resolver) {
            var stateItem = GetOrAdd(requirement);

            stateItem.State = DependencyState.Unresolved;
            stateItem.Resolver = resolver;
        }

        public virtual void Resolved(IDependencyRequirement requirement, object resolver) {
            var stateItem = GetOrAdd(requirement);

            stateItem.State = DependencyState.Resolved;
            stateItem.Resolver = resolver;
        }

        internal DependencyState<IDependencyRequirement> GetOrAdd(IDependencyRequirement requirement) {
            DependencyState<IDependencyRequirement> dependency = dependencies.FirstOrDefault(s => requirement.Equals(s.Item));

            if (dependency == null) {
                dependency = new DependencyState<IDependencyRequirement>();
                dependency.Item = requirement;

                dependencies.Add(dependency);
            }
            return dependency;
        }

        /// <summary>
        /// Gets the resolved requirements.
        /// </summary>
        public virtual IEnumerable<IDependencyRequirement> GetResolvedRequirements() {
            return dependencies.Where(s => s.State == DependencyState.Resolved).Select(s => s.Item);
        }

        /// <summary>
        /// Gets the resolved or unresolved requirements.
        /// </summary>
        public virtual IEnumerable<IDependencyRequirement> GetRequirementsByType(DependencyState type) {
            return dependencies.Where(s => s.State == type).Select(s => s.Item);
        }

        public void AssociateRequirements(ExpertModule module, IEnumerable<IDependencyRequirement> requirements) {
            foreach (var requirement in requirements) {
                var dependency = GetOrAdd(requirement);

                dependency.Module = module;
            }
        }

        internal static ICollection<ExpertModule> GetDependenciesRequiredForBuild(IEnumerable<ModuleDependency> moduleDependencyGraph, List<ExpertModule> inBuild) {
            // This unique set of modules we need to build the current build queue.
            var dependenciesRequiredForBuild = new HashSet<ExpertModule>();

            foreach (var module in inBuild) {
                IEnumerable<ExpertModule> dependenciesRequiredForModule = moduleDependencyGraph
                    .Where(dependency => dependency.Consumer.Equals(module)) // Find the module in the dependency graph
                    .Select(dependency => dependency.Provider);

                foreach (var dependency in dependenciesRequiredForModule) {
                    // Don't add the self pointer (Module1 <==> Module1)
                    if (!string.Equals(dependency.Name, module.Name, StringComparison.OrdinalIgnoreCase)) {
                        // Test if the current build set contains the dependency - if it does we will be building the dependency
                        // rather than getting it from the drop
                        if (!inBuild.Contains(dependency)) {
                            dependenciesRequiredForBuild.Add(dependency);
                        }
                    }
                }
            }

            return dependenciesRequiredForBuild;
        }

           /// <summary>
        /// Captures any .NET framework restrictions and the group they are associated with.
        /// </summary>
        public void AddFrameworkRestriction(string group, string frameworkVersion) {
            HashSet<string> set;
            if (!restrictions.TryGetValue(group, out set)) {
                restrictions.Add(group, set = new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }

            set.Add(frameworkVersion);
        }

        public Dictionary<string, IReadOnlyCollection<string>> GetFrameworkRestrictions() {
            return restrictions.ToDictionary(s => s.Key, s => (IReadOnlyCollection<string>)s.Value.ToList());
        }

        public IEnumerable<ExpertModule> GetModulesInBuild() {
            return modules.Where(m => m.IsInBuildChain).Select(s => s.Item).ToList();
        }
    }

    internal class ItemWrapper<T> {
        public T Item { get; set; }
    }

    internal class ModuleState<T> : ItemWrapper<T> {
        public ModuleState(T module) {
            Item = module;
        }

        public bool IsInBuildChain { get; set; }
        public bool RequiresThirdPartyReplication { get; set; }
    }

    internal enum DependencyState {
        Unknown,
        Unresolved,
        Resolved,
    }

    internal class DependencyState<T> : ItemWrapper<T> {
        public DependencyState State { get; set; }
        public object Resolver { get; set; }
        public ExpertModule Module { get; set; }
    }
}
