using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem.SolutionParser {
    internal class ParseResult {
        public string SolutionFile { get; internal set; }
        public Dictionary<Guid, ProjectInSolutionWrapper> ProjectsByGuid { get; internal set; }
        public IEnumerable<ProjectInSolutionWrapper> ProjectsInOrder { get; internal set; }
    }
}
