using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem.SolutionParser {
    internal class ParseResult {
        public string SolutionFile { get; internal set; }
        public Dictionary<Guid, ProjectInSolution> ProjectsByGuid { get; internal set; }
        public IReadOnlyList<ProjectInSolution> ProjectsInOrder { get; internal set; }
        public IReadOnlyList<SolutionConfigurationInSolution> SolutionConfigurations { get; internal set; }

        public void AssociateTo(string projectFullPath) {
            
        }
    }
}
