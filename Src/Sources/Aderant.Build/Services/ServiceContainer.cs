using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;

namespace Aderant.Build.Services {
    internal class ServiceContainer : IServiceProvider, IContextualServiceProvider {

        public static ServiceContainer Default = new ServiceContainer(new[] { typeof(ServiceContainer).Assembly });
        private CompositionContainer container;
        private MethodInfo svcMethod = typeof(ServiceContainer).GetMethod("GetService", new Type[] { typeof(Context), typeof(string), typeof(string) });

        public ServiceContainer(IReadOnlyCollection<Assembly> catalogAssemblies) {
            List<Type> types = new List<Type>();

            VisualStudioEnvironmentContext.SetupContext();

            foreach (var asm in catalogAssemblies) {
                try {
                    types.AddRange(asm.GetTypes());
                } catch (ReflectionTypeLoadException ex) {
                    types.AddRange(ex.Types.Where(t => t != null));
                }
            }

            var catalog = new AggregateCatalog(new TypeCatalog(types));
            //container = new CompositionContainer(catalog)

            // TODO: rename
            var appLevelCatalog = catalog.Filter(cpd => !cpd.ExportDefinitions.Any(ed => ed.Metadata.ContainsKey("Scope")));
            var docLevelCatalog = catalog.Filter(cpd => cpd.ExportDefinitions.Any(ed => ed.Metadata.ContainsKey("Scope") && ed.Metadata["Scope"].ToString() == "ConfiguredProject"));

            var scopeDefinition = new CompositionScopeDefinition(
                appLevelCatalog,
                new[] { new CompositionScopeDefinition(docLevelCatalog, null) });

            container = new CompositionContainer(scopeDefinition);

            VisualStudioEnvironmentContext.Shutdown();
        }

        public object GetService(Type serviceType) {
            return svcMethod.MakeGenericMethod(serviceType).Invoke(this, new object[] { null, null, null });
        }

        public T GetService<T>(Context context, string contractName = null, string scope = null) {
            var batch = new CompositionBatch();

            var currentContext = container.GetExportedValueOrDefault<Context>();
            if (currentContext == null) {
                AttributedModelServices.AddExportedValue(batch, context);
            }

            container.Compose(batch);

            if (scope != null) {
                // Try bind to an instance with a specific export context (e.g a scope, or key with a particular value)
                var export = container.GetExports<T, IDictionary<string, object>>()
                    .FirstOrDefault(
                        e => {
                            object value;
                            if (e.Metadata.TryGetValue(CompositionProperties.AppliesTo, out value)) {
                                if (Equals(value, scope)) {
                                    return true;
                                }
                            }

                            return false;
                        });

                if (export != null) {
                    return export.Value;
                }
            }

            if (contractName != null && typeof(T) == typeof(object)) {
                return container.GetExportedValue<T>(contractName);
            }

            return container.GetExportedValue<T>();
        }

        public T GetExportedValue<T>() {
            return container.GetExportedValue<T>();
        }


        public static T CreateDefaultImplementation<T>(IReadOnlyCollection<Assembly> assemblies, bool throwCompositionErrors = false) {
            return GetExport<T>(CreateSelfHostContainer(assemblies, throwCompositionErrors));
        }

        private static T GetExport<T>(ServiceContainer createSelfHostContainer) {
            return createSelfHostContainer.GetExportedValue<T>();
        }

        private static ServiceContainer CreateSelfHostContainer(IReadOnlyCollection<Assembly> assemblies, bool throwCompositionErrors) {
            return new ServiceContainer(assemblies);
        }
    }
}
