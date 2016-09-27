using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Packaging {
    public sealed class ProductAssemblyContext : IPackageContext {
        private string productDirectory;
        private string rootDirectory;
        public IEnumerable<ExpertModule> Modules { get; internal set; }

        public string ProductDirectory {
            get { return productDirectory; }
            internal set {
                productDirectory = value;
                rootDirectory = Path.Combine(productDirectory, "..\\");
            }
        }

        public bool IncludeDevelopmentDependencies {
            get { return false; }
        }

        public bool AllowExternalPackages {
            get { return false; }
        }

        public IEnumerable<string> BuildOutputs { get; internal set; }

        public ExpertModule GetModuleByPackage(string packageDirectory) {
            var name = Path.GetFileName(packageDirectory);

            return Modules.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public string ResolvePackageRelativeDirectory(ExpertModule module) {
            if (IsRootItem(module)) {
                string path = module.Target.Replace("$", rootDirectory);
                return Path.GetFullPath(path);
            }

            if (!string.IsNullOrEmpty(module.Target)) {
                return Path.Combine(ProductDirectory, module.Target);
            }

            return ProductDirectory;
        }

        public bool IsRootItem(ExpertModule module) {
            if (!string.IsNullOrEmpty(module.Target)) {
                return module.Target[0] == '$';
            }
            return false;
        }
    }
}