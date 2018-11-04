using System.Collections.Generic;
using System.Xml.Linq;
using Aderant.Build.MSBuild;

namespace Aderant.Build.ProjectSystem {
    internal class BuildPlan {
        private readonly Project project;

        public BuildPlan(Project project) {
            this.project = project;
        }

        public IReadOnlyCollection<string> DirectoriesInBuild { get; set; }

        public XElement CreateXml() {
            return project.CreateXml();
        }
    }
}
