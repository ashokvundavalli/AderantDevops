using System.Collections.Generic;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Providers {
    public interface IModuleProvider {

        /// <summary>
        /// Gets the product manifest path.
        /// </summary>
        /// <value>
        /// The product manifest path.
        /// </value>
        string ProductManifestPath {
            get;
        }

        /// <summary>
        /// Gets the two part branch name
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        string Branch {
            get;
        }

        /// <summary>
        /// Gets the distinct complete list of available modules and those referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ExpertModule> GetAll();

        /// <summary>
        /// Tries to get the Dependency Manifest document from the given module.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <param name="manifest">The manifest.</param>
        /// <returns></returns>
        bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest);

        /// <summary>
        /// Tries to the get the path to the dependency manifest for a given module. 
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifestPath">The manifest path.</param>
        /// <returns></returns>
        bool TryGetDependencyManifestPath(string moduleName, out string manifestPath);

        /// <summary>
        /// Determines whether the specified module is available to the current branch.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns>
        ///   <c>true</c> if the specified module name is available; otherwise, <c>false</c>.
        /// </returns>
        bool IsAvailable(string moduleName);

        /// <summary>
        /// Gets the module with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns></returns>
        ExpertModule GetModule(string moduleName);

        /// <summary>
        /// Adds the specified module to the provider.
        /// </summary>
        /// <param name="module">The new module.</param>
        void Add(ExpertModule module);

        /// <summary>
        /// Removes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        void Remove(IEnumerable<ExpertModule> items);

        /// <summary>
        /// Saves this instance.
        /// </summary>
        string Save();
    }
}