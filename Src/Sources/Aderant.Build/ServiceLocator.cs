using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Reflection;

namespace Aderant.Build {
    public static class ServiceLocator {
        private static object gate = new object();

        private static CompositionContainer container;

        private static AggregateCatalog catalog;

        static ServiceLocator() {
            Initialize();
        }

        internal static ExportMode ExportMode {
            get {
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BuildAgentName"))) {
                    return ExportMode.Server;
                }
                return ExportMode.Desktop;
            }
        }

        public static void Initialize() {
            lock (gate) {
                if (container == null) {
                    VisualStudioEnvironmentContext.SetupContext();

                    var asm = typeof(ServiceLocator).Assembly;

                    Type[] types;
                    try {
                        types = asm.GetTypes();
                    } catch (ReflectionTypeLoadException ex) {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    catalog = new AggregateCatalog(new TypeCatalog(types));
                    container = new CompositionContainer(catalog);

                    VisualStudioEnvironmentContext.Shutdown();
                }
            }
        }

        public static T GetInstance<T>() where T : class {
            var instance = container.GetExportedValue<T>();

            if (instance == null) {
                throw new InvalidOperationException("No instance of type:" + typeof(T) + " in container");
            }

            return instance;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
    internal class ContextualExport : ExportAttribute {
        public ContextualExport(Type type, ExportMode exportMode)
            : base(GetType(type, exportMode)) {
        }

        private static Type GetType(Type type, ExportMode mode) {
            if (mode == ServiceLocator.ExportMode) {
                return type;
            }
            return typeof(NoType);
        }
    }

    internal enum ExportMode {
        Desktop,
        Server,
    }

    class NoType {
    }
}