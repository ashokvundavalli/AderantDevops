using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectDependencyAnalyzer.SolutionParser {
    public class ParseResult {
        public string SolutionFile { get; internal set; }
        public IReadOnlyDictionary<string, ProjectInSolution> ProjectsByGuid { get; internal set; }
        public IReadOnlyList<ProjectInSolution> ProjectsInOrder { get; internal set; }
        public IReadOnlyList<SolutionConfigurationInSolution> SolutionConfigurations { get; internal set; }
    }
}