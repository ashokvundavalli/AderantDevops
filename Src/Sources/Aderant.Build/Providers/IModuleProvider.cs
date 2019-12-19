using System.Collections.Generic;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Providers {

    public interface IModuleProvider {

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
        /// Gets the module with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns></returns>
        ExpertModule GetModule(string moduleName);

    }

    public interface IModuleGroupingSupport {
        /// <summary>
        /// Tries the a container module for this module. For example a module might have an alias or be part of a group. 
        /// This will return the parent.
        /// </summary>
        bool TryGetContainer(string component, out ExpertModule container);
    }
}