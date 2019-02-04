using System.Linq;
using Aderant.Build.Logging;
using Aderant.Build.ProjectSystem;
using Microsoft.Build.Framework;

namespace Aderant.Build.Tasks {
    public class GatherProjectsToBuild : BuildOperationContextTask {
        private DirectoryGroveler groveler;

        public string[] ExcludedPaths { get; set; }

        [Output]
        public string[] DirectoriesInBuild {
            get { return groveler.DirectoriesInBuild.ToArray(); }
        }

        [Output]
        public string[] ExtensibilityFiles {
            get { return groveler.ExtensibilityFiles.ToArray(); }
        }

        [Output]
        public string[] DirectoryMakeFiles {
            get { return groveler.MakeFiles.ToArray(); }
        }

        [Output]
        public string[] ProjectFiles {
            get { return groveler.ProjectFiles.ToArray(); }
        }

        public override bool ExecuteTask() {
            if (ExcludedPaths != null) {
                ExcludedPaths = ExcludedPaths.Select(PathUtility.GetFullPath).ToArray();

                Log.LogMessage("Excluding paths: " + string.Join(",", ExcludedPaths));
            }

            groveler = new DirectoryGroveler(new PhysicalFileSystem());
            groveler.Logger = new BuildTaskLogger(Log);
            groveler.Grovel(Context.Include, ExcludedPaths);
            groveler.ExpandBuildTree(PipelineService);

            return !Log.HasLoggedErrors;
        }
    }
}