using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build {
    [Serializable]
    public sealed class Context {
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

        public IArgumentBuilder CreateArgumentBuilder(string engineType) {
            if (engineType == "MSBuild") {
                return new ComboBuildArgBuilder(this);
            }

            throw new NotImplementedException("No builder for " + engineType);
        }
    }

}
