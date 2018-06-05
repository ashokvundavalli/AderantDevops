using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build {
    [Serializable]
    public sealed class Context {
        private BuildMetadata buildMetadata;

        public Context() : this(new BuildMetadata()) {
        }

        public Context(BuildMetadata buildMetadata) {
            this.buildMetadata = buildMetadata;

            Configuration = new Dictionary<object, object>();
            TaskDefaults = new Dictionary<string, IDictionary>();
            TaskIndex = -1;
            Variables = new Dictionary<string, object>();
            PipelineName = "";
            TaskName = "";
        }

        public ComboBuildType ComboBuildType { get; set; }

        public DownStreamType DownStreamType { get; set; }

        public string BuildFrom { get; set; }

        public DirectoryInfo BuildRoot { get; set; }

        public bool IsDesktopBuild => BuildMetadata.HostEnvironment.Equals(HostEnvironment.Developer);

        public IDictionary Configuration { get; set; }

        public FileInfo ConfigurationPath { get; set; }

        public DirectoryInfo DownloadRoot { get; set; }

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
            }
        }

        public IArgumentBuilder CreateArgumentBuilder(string engineType) {
            if (engineType == "MSBuild") {
                return new ComboBuildArgBuilder(this);
            }

            throw new NotImplementedException("No builder for " + engineType);
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
