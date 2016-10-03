using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;
using Microsoft.Build.Framework;

namespace Aderant.Build.DependencyResolver {
    internal class NupkgResolver : IDependencyResolver {
        public IEnumerable<IDependencyRequirement> GetDependencyRequirements(ExpertModule module) {
            //if (!string.IsNullOrEmpty(DependencyFile)) {
            //    module.VersionRequirement = new PackageManager(new PhysicalFileSystem(Path.GetDirectoryName(DependencyFile)), null).GetVersionsFor(module.Name);
            //}
            return null;
        } 

        public void Resolve(IEnumerable<IDependencyRequirement> requirements) {

        }
    }

    internal class ExpertModuleDependencyResolver : IDependencyResolver {
        private readonly IFileSystem2 fileSystem;

        public string Root { get; set; }

        public ExpertModuleDependencyResolver(IFileSystem2 fileSystem) {
            this.fileSystem = fileSystem;
            this.Root = fileSystem.Root;
            this.ManifestFinder = LoadManifestFromFile;
        }

        private Stream LoadManifestFromFile(string name) {
            string modulePath = Path.Combine(Root, name);

            if (fileSystem.DirectoryExists(modulePath)) {
                var manifestFile = fileSystem.GetFiles(modulePath, DependencyManifest.DependencyManifestFileName, true).FirstOrDefault();

                return fileSystem.OpenFile(manifestFile);
            }

            return null;
        }

        internal Func<string, Stream> ManifestFinder { get; set; }

        public IEnumerable<IDependencyRequirement> GetDependencyRequirements(ExpertModule module) {
            Stream stream = ManifestFinder(module.Name);

            if (stream != null) {
                DependencyManifest manifest;
                using (stream) {
                    manifest = new DependencyManifest(module.Name, stream);
                }

                foreach (var reference in manifest.ReferencedModules) {
                    yield return new DependencyRequirement(reference);
                }
            }
        }

        public void Resolve(IEnumerable<IDependencyRequirement> requirements) {
        }
    }

    internal class DependencyRequirement : IDependencyRequirement {
        public DependencyRequirement(ExpertModule reference) {
            Name = reference.Name;
            Branch = reference.Branch;
            VersionRequirement = new VersionRequirement {
                Version = reference.AssemblyVersion
            };
        }

        public string Branch { get; set; }

        public string Name { get; }
        public VersionRequirement VersionRequirement { get; }
    }

    internal interface IDependencyResolver {
        IEnumerable<IDependencyRequirement> GetDependencyRequirements(ExpertModule module);

        void Resolve(IEnumerable<IDependencyRequirement> requirements);
    }

    internal interface IDependencyRequirement {
        string Name { get; }
        VersionRequirement VersionRequirement { get; }
    }

    internal class ResolverRequest {
        List<ExpertModule> modules = new List<ExpertModule>();

        public ResolverRequest() {
        }

        public ResolverRequest(ExpertModule expertModule) {
            modules.Add(expertModule);
        }

        public IEnumerable<ExpertModule> Modules {
            get { return modules.AsReadOnly(); }
        }

        public IModuleProvider ModuleFactory { get; set; }

        public void AddModule(string module) {
            if (Path.IsPathRooted(module)) {
                module = Path.GetFileName(module);
            }

            ExpertModule resolvedModule = ModuleFactory.GetModule(module);

            if (resolvedModule == null) {
                throw new InvalidOperationException(string.Format("Unable to resolve module {0}.", module));
            }

            modules.Add(resolvedModule);
        }
    }

    internal class ModuleDependencyResolver2 {
        private List<IDependencyResolver> resolvers = new List<IDependencyResolver>();

        public ModuleDependencyResolver2(params IDependencyResolver[] resolvers) {
            foreach (var resolver in resolvers) {
                this.resolvers.Add(resolver);
            }
        }

        public void ResolveDependencies(ResolverRequest resolverRequest) {
            List<IDependencyRequirement> requirements = new List<IDependencyRequirement>();

            foreach (var module in resolverRequest.Modules) {
                foreach (IDependencyResolver resolver in resolvers) {
                    IEnumerable<IDependencyRequirement> dependencyRequirements = resolver.GetDependencyRequirements(module);

                    if (dependencyRequirements != null) {
                        foreach (IDependencyRequirement dependencyRequirement in dependencyRequirements) {
                            requirements.Add(dependencyRequirement);
                        }
                    }
                }
            }

            foreach (IDependencyResolver resolver in resolvers) {
                resolver.Resolve(requirements);
            }
        }
    }
}