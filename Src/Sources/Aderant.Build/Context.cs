using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.Services;

namespace Aderant.Build {

    [Serializable]
    public sealed class Context {
        private BuildMetadata buildMetadata;
        private BuildSwitches switches = default(BuildSwitches);
        private IServiceProviderInternal serviceProvider;

        public Context()
            : this(ServiceContainer.Default) {
            Configuration = new Dictionary<object, object>();
            TaskDefaults = new Dictionary<string, IDictionary>();
            TaskIndex = -1;
            Variables = new Dictionary<string, object>();
            Environment = "";
            PipelineName = "";
            TaskName = "";
        }

        private Context(IServiceProviderInternal serviceProvider) {
            serviceProvider.Initialize(this);
            this.serviceProvider = serviceProvider;
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
            get { return buildMetadata; }
            set {
                buildMetadata = value;

                if (value != null) {
                    if (value.HostEnvironment != "developer") {
                        this.IsDesktopBuild = false;
                    }
                }
            }
        }

        public BuildSwitches Switches {
            get { return switches; }
            set { switches = value; }
        }

        public IArgumentBuilder CreateArgumentBuilder(string engineType) {
            return serviceProvider.GetService<IArgumentBuilder>(engineType);
        }

        /// <summary>
        /// Creates a new instance of T.
        /// </summary>
        public T GetService<T>() where T : class, IFlexService {
            IFlexService svc = serviceProvider.GetService(typeof(T)) as IFlexService;
            if (svc != null) {
                svc.Initialize(this);
            }

            return (T)svc;
        }

        public object GetService(string contract) {
            IFlexService svc = serviceProvider.GetService<object>(contract, null) as IFlexService;
            if (svc != null) {
                svc.Initialize(this);
            }

            return svc;
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

    public struct BuildSwitches {
        public bool Downstream { get; set; }
        public bool Transitive { get; set; }
        public bool Everything { get; set; }
        public bool Clean { get; set; }
        public bool Release { get; set; }
    }

}