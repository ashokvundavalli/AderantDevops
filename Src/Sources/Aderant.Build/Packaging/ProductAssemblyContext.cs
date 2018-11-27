using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Packaging {
    public sealed class ProductAssemblyContext : IPackageContext {
        private string productDirectory;
        private string rootDirectory;
        public IReadOnlyCollection<ExpertModule> Modules { get; internal set; }

        public string ProductDirectory {
            get { return productDirectory; }
            internal set {
                productDirectory = value;
                rootDirectory = Path.Combine(productDirectory, "..\\");
            }
        }

        public IEnumerable<string> BuildOutputs { get; internal set; }

        public ExpertModule GetModuleByPackage(string packageDirectory, string group) {
            var name = Path.GetFileName(packageDirectory);

            if (string.IsNullOrEmpty(group)) {
                return Modules.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase) && m.IsInDefaultDependencyGroup);
            }
            return Modules.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase) && string.Equals(m.DependencyGroup, group, StringComparison.OrdinalIgnoreCase));
        }

        public string ResolvePackageRelativeDirectory(ExpertModule module) {
            if (RequiresContentProcessing(module)) {
                string path = module.Target.Replace("$", rootDirectory);
                return Path.GetFullPath(path);
            }

            if (!string.IsNullOrEmpty(module.Target)) {
                return Path.Combine(ProductDirectory, module.Target);
            }

            if (!module.IsInDefaultDependencyGroup && !string.IsNullOrEmpty(module.DependencyGroup)) {
                return Path.Combine(ProductDirectory, module.DependencyGroup);
            }

            return ProductDirectory;
        }

        public bool RequiresContentProcessing(ExpertModule module) {
            if (!string.IsNullOrEmpty(module.Target)) {
                return module.Target.IndexOf("$", StringComparison.Ordinal) >= 0;
            }
            return false;
        }

        public IEnumerable<string> ResolvePackageRelativeDestinationDirectories(ExpertModule module) {
            if (RequiresContentProcessing(module)) {
                if (!string.IsNullOrEmpty(module.Target)) {
                    var parts = module.Target.Split(';');

                    foreach (var part in parts) {
                        string path = part.Replace("$", rootDirectory);

                        yield return Path.GetFullPath(path);
                    }
                }
                yield break;
            }

            if (!string.IsNullOrEmpty(module.Target)) {
                var parts = module.Target.Split(';');
                foreach (var part in parts) {
                    yield return Path.Combine(ProductDirectory, part);

                }
                yield break;
            }

            yield return ProductDirectory;
        }
    }
}