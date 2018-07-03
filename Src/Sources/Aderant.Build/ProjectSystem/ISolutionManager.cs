using System;

namespace Aderant.Build.ProjectSystem {
    internal interface ISolutionManager {
        SolutionProject GetSolutionForProject(string projectFilePath, Guid projectGuid);
    }
}
