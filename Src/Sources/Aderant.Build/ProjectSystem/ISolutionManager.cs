using System;

namespace Aderant.Build.ProjectSystem {
    internal interface ISolutionManager {
        SolutionSearchResult GetSolutionForProject(string projectFilePath, Guid projectGuid);
    }
}
