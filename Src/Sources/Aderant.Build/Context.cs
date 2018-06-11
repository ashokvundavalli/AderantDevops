using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Aderant.Build.Services;

namespace Aderant.Build {
    [Serializable]
    public sealed class Context {
        private readonly ConcurrentDictionary<Type, object> serviceInstances = new ConcurrentDictionary<Type, object>();
        private readonly ConcurrentDictionary<Type, Type> serviceTypes = new ConcurrentDictionary<Type, Type>();

        private BuildMetadata buildMetadata;
     
        public Context() {
            Configuration = new Dictionary<object, object>();
            TaskDefaults = new Dictionary<string, IDictionary>();
            TaskIndex = -1;
            Variables = new Dictionary<string, object>();
            Environment = "";
            PipelineName = "";
            TaskName = "";
        }

        public DirectoryInfo BuildRoot { get; set; }

        public bool IsDesktopBuild { get; set; } = true;

        public IDictionary Configuration { get; set; }

        public FileInfo ConfigurationPath { get; set; }

        public DirectoryInfo DownloadRoot { get; set; }

        public string Environment { get; set; }

        public DirectoryInfo OutputDirectory { get; set; }

        public string PipelineName { get; set; }

        public bool Publish { get; set; }

        public DateTime StartedAt { get; set; }

        public string TaskName { get; set; }

        public int TaskIndex { get; set; }

        public IDictionary TaskDefaults { get; private set; }

        public DirectoryInfo Temp { get; set; }

        public IDictionary Variables { get; private set; }

        public BuildMetadata BuildMetadata {
            get {
                return buildMetadata ?? new BuildMetadata();
            }
            set {
                buildMetadata = value;

                if (value != null) {
                    if (value.HostEnvironment != "developer") {
                        this.IsDesktopBuild = false;
                    }
                }
            }
        }

        public IArgumentBuilder CreateArgumentBuilder(string engineType) {
            if (engineType == "MSBuild") {
                return new ComboBuildArgBuilder(this);
            }

            throw new NotImplementedException("No builder for " + engineType);
        }

        /// <summary>
        /// Creates a new instance of T.
        /// </summary>
        public T CreateService<T>() where T : class, IFlexService {
            Type target;
            if (!serviceTypes.TryGetValue(typeof(T), out target)) {
                // Infer the concrete type from the ServiceLocatorAttribute.
                CustomAttributeData attribute = typeof(T)
                    .GetTypeInfo()
                    .CustomAttributes
                    .FirstOrDefault(x => x.AttributeType == typeof(ExportAttribute));

                if (attribute != null) {
                    foreach (CustomAttributeNamedArgument arg in attribute.NamedArguments) {
                        if (string.Equals(arg.MemberName, nameof(ExportAttribute.ContractType), StringComparison.Ordinal)) {
                            target = arg.TypedValue.Value as Type;
                        }
                    }
                }

                if (target == null) {
                    throw new KeyNotFoundException(string.Format(CultureInfo.InvariantCulture, "Service mapping not found for key '{0}'.", typeof(T).FullName));
                }

                serviceTypes.TryAdd(typeof(T), target);
                target = serviceTypes[typeof(T)];
            }

            // Create a new instance.
            T svc = Activator.CreateInstance(target) as T;
            svc.Initialize(this);
            return svc;
        }

        /// <summary>
        /// Gets or creates an instance of T.
        /// </summary>
        public T GetService<T>() where T : class, IFlexService {
            // Return the cached instance if one already exists.
            object instance;
            if (serviceInstances.TryGetValue(typeof(T), out instance)) {
                return instance as T;
            }

            // Otherwise create a new instance and try to add it to the cache.
           serviceInstances.TryAdd(typeof(T), CreateService<T>());

            // Return the instance from the cache.
            return serviceInstances[typeof(T)] as T;
        }
    }


    public enum ComboBuildType {
        Changed,
        Branch,
        Staged,
        All
    }

    public enum DownStreamType {
        Direct,
        All,
        None
    }
}
