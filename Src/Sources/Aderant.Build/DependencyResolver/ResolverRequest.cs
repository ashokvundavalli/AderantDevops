using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Logging;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyResolver {
    internal class ResolverRequest {
        private readonly ILogger logger;
        private readonly string modulesRootPath;
        private List<ModuleState<ExpertModule>> modules = new List<ModuleState<ExpertModule>>();
        private List<DependencyState<IDependencyRequirement>> dependencies = new List<DependencyState<IDependencyRequirement>>();
        private string dependenciesDirectory;
        private IFileSystem2 physicalFileSystem;

        public ILogger Logger {
            get { return logger; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolverRequest"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="modulesRootPath"></param>
        /// <param name="modules">The modules.</param>
        public ResolverRequest(ILogger logger, string modulesRootPath, params ExpertModule[] modules)
            : this(logger, new PhysicalFileSystem(modulesRootPath), modules) {
        }

        private ResolverRequest(ILogger logger, IFileSystem2 physicalFileSystem, params ExpertModule[] modules) {
            this.logger = logger;
            this.physicalFileSystem = physicalFileSystem;
            this.modulesRootPath = physicalFileSystem.Root;

            if (modules != null) {
                this.modules.AddRange(modules.Select(m => new ModuleState<ExpertModule>(m)));
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
        public bool RequiresReplication {
            get { return modules.Any(s => s.RequiresThirdPartyReplication); }
        }

        /// <summary>
        /// Gets or sets the type of the request.
        /// </summary>
        /// <remarks>
        /// We have XAML continuous integration builds, XAML build all, teambuild desktop build, desktop build VNext, server build VNext.
        /// All of these use different root path flavors, some with the module name, some without. We need a hint from the build system to figure what the root path means to us
        /// so we know how to probe for files
        /// </remarks>
        public string DirectoryContext { get; set; }

        /// <summary>
        /// Sets the dependencies directory to place dependencies into.
        /// </summary>
        /// <value>The dependencies directory.</value>
        public void SetDependenciesDirectory(string directory) {
            dependenciesDirectory = directory;
        }

        public virtual string GetModuleDirectory(ExpertModule module) {
            if (!modulesRootPath.TrimEnd(Path.DirectorySeparatorChar).EndsWith(module.Name, StringComparison.OrdinalIgnoreCase)) {
                return Path.Combine(Path.Combine(modulesRootPath, module.Name));
            }

            return modulesRootPath;
        }

        public virtual string GetDependenciesDirectory(IDependencyRequirement requirement) {
            if (!string.IsNullOrEmpty(dependenciesDirectory)) {
                return dependenciesDirectory;
            }

            ExpertModule module = GetOrAdd(requirement).Module;
            if (module == null) {
                throw new InvalidOperationException(string.Format("Resolver error. Unable to determine locate module directory for requirement {0}.", requirement.Name));
            }

            return Path.Combine(GetModuleDirectory(module), "Dependencies");
        }

        public virtual void AddModule(string module, bool isPartOfBuildChain = false) {
            if (Path.IsPathRooted(module)) {
                module = Path.GetFileName(module);
            }

            ExpertModule resolvedModule = ModuleFactory.GetModule(module);

            bool requiresThirdPartyReplication = true;

            if (resolvedModule == null) {
                if (!physicalFileSystem.DirectoryExists(".git")) {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to resolve module {0}. Does the name exist in the Expert Manifest?", module));
                }
                resolvedModule = new ExpertModule { Name = module };
                requiresThirdPartyReplication = false;
            }

            modules.Add(new ModuleState<ExpertModule>(resolvedModule) { IsInBuildChain = true, RequiresThirdPartyReplication = requiresThirdPartyReplication });
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

        private DependencyState<IDependencyRequirement> GetOrAdd(IDependencyRequirement requirement) {
            DependencyState<IDependencyRequirement> dependency = dependencies.FirstOrDefault(s => Equals(s.Item, requirement));

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

        /// <summary>
        /// Returns the modules from the dependency tree that we are currently building. 
        /// This is done as the dependencies don't need to come from a source but instead they will be produced by this build.
        /// </summary>
        /// <remarks>
        /// This has no knowledge of packaged modules and so will not return NuGet dependencies. This means modules which are hybrids
        /// and have both a packet.dependencies and a DependencyManifest.xml will fail to compile as the dependencies decalred in the paket file will not 
        /// considered an external dependency (effectively paket items are invisible to the expert module system in most cases). 
        /// </remarks>
        public IEnumerable<ExpertModule> GetExternalDependencies() {
            return GetReferencedModulesForBuild(Modules, modules.Where(m => m.IsInBuildChain).Select(s => s.Item).ToList());
        }

        private IEnumerable<ExpertModule> GetReferencedModulesForBuild(IEnumerable<ExpertModule> availableModules, List<ExpertModule> inBuild) {
            var builder = new DependencyBuilder(ModuleFactory);

            var moduleDependencyGraph = builder.GetModuleDependencies().ToList();

            var modulesRequiredForBuild = GetDependenciesRequiredForBuild(availableModules, moduleDependencyGraph, inBuild);

            if (modulesRequiredForBuild.Any()) {
                // We don't require any external dependencies to build - however we need to move the third party modules to the dependency folder always
                inBuild = inBuild.Where(m => m.ModuleType == ModuleType.ThirdParty).ToList();
            } else {
                inBuild = modulesRequiredForBuild.Concat(inBuild.Where(m => m.ModuleType == ModuleType.ThirdParty)).ToList();
            }

            return inBuild;
        }

        internal static ICollection<ExpertModule> GetDependenciesRequiredForBuild(IEnumerable<ExpertModule> modules, IEnumerable<ModuleDependency> moduleDependencyGraph, List<ExpertModule> inBuild) {
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