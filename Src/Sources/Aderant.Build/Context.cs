using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.Services;

namespace Aderant.Build {

    [Serializable]
    public class Context {
        private BuildMetadata buildMetadata;

        private string buildScriptsDirectory;
        private bool isDesktopBuild = true;

        [NonSerialized]
        private IContextualServiceProvider serviceProvider;

        private BuildSwitches switches = default(BuildSwitches);

        public Context() {
            Configuration = new Dictionary<object, object>();
            TaskDefaults = new Dictionary<string, IDictionary>();
            TaskIndex = -1;
            Variables = new Dictionary<string, object>();
            Environment = "";
            PipelineName = "";
            TaskName = "";
        }

        public string BuildScriptsDirectory {
            get {
                if (string.IsNullOrWhiteSpace(buildScriptsDirectory)) {
                    throw new ArgumentNullException(nameof(buildScriptsDirectory));
                }

                return buildScriptsDirectory;
            }
            set {
                value = Path.GetFullPath(value);
                buildScriptsDirectory = value;
            }
        }

        public DirectoryInfo BuildRoot { get; set; }

        public string BuildSystemDirectory { get; set; }

        public bool IsDesktopBuild {
            get { return isDesktopBuild; }
            set { isDesktopBuild = value; }
        }

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
                    if (!string.IsNullOrWhiteSpace(value.BuildId)) {
                        IsDesktopBuild = false;
                    } else {
                        IsDesktopBuild = true;
                    }
                }
            }
        }

        public BuildSwitches Switches {
            get { return switches; }
            set {
                switches = value;
            }
        }

        internal IContextualServiceProvider ServiceProvider {
            get {
                if (serviceProvider != null) {
                    return serviceProvider;
                }

                return serviceProvider = ServiceContainer.Default;
            }
        }

        /// <summary>
        /// Creates a new instance of T.
        /// </summary>
        public T GetService<T>() where T : class {
            IFlexService svc = ServiceProvider.GetService(typeof(T)) as IFlexService;
            if (svc != null) {
                svc.Initialize(this);
            }

            return (T)svc;
        }

        public object GetService(string contract) {
            IFlexService svc = ServiceProvider.GetService<object>(this, contract, null) as IFlexService;
            if (svc != null) {
                svc.Initialize(this);
            }

            return svc;
        }
    }

    [Serializable]
    public struct BuildSwitches {

        public bool PendingChanges { get; set; }
        public bool Downstream { get; set; }
        public bool Transitive { get; set; }
        public bool Everything { get; set; }
        public bool Clean { get; set; }
        public bool Release { get; set; }
        public bool DryRun { get; set; }
        public bool Resume { get; set; }
    }

}
