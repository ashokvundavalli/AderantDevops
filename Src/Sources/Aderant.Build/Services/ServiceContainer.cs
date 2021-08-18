using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.Services {
    internal class ServiceContainer : IServiceProvider, IContextualServiceProvider {

        private CompositionContainer container;
        private MethodInfo svcMethod = typeof(ServiceContainer).GetMethod("GetService", new Type[] { typeof(BuildOperationContext), typeof(string), typeof(string) });

        public ServiceContainer(ILogger logger, IReadOnlyCollection<Assembly> catalogAssemblies) {
            List<Type> types = new List<Type>();

            foreach (var asm in catalogAssemblies) {
                try {
                    types.AddRange(asm.GetTypes());
                } catch (ReflectionTypeLoadException ex) {
                    types.AddRange(ex.Types.Where(t => t != null));
                }
            }

            var catalog = new AggregateCatalog(new TypeCatalog(types));

            var unfilteredCatalog = catalog.Filter(cpd => !cpd.ExportDefinitions.Any(ed => ed.Metadata.ContainsKey("Scope")));
            var filteredCatalog = catalog.Filter(cpd => cpd.ExportDefinitions.Any(ed => ed.Metadata.ContainsKey("Scope") && ed.Metadata["Scope"].ToString() == nameof(ConfiguredProject)));

            var scopeDefinition = new CompositionScopeDefinition(
                unfilteredCatalog,
                new[] { new CompositionScopeDefinition(filteredCatalog, null) });

            container = new CompositionContainer(scopeDefinition, CompositionOptions.IsThreadSafe);
            if (logger != null) {
                container.ComposeExportedValue(logger);
            }

            VisualStudioEnvironmentContext.Shutdown();
        }

        public object GetService(Type serviceType) {
            return svcMethod.MakeGenericMethod(serviceType).Invoke(this, new object[] { null, null, null });
        }

        public T GetExportedValue<T>() {
            return container.GetExportedValue<T>();
        }
    }
}
