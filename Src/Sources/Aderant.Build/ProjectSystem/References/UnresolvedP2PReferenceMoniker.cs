using System;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {
    internal class UnresolvedP2PReferenceMoniker {

        internal UnresolvedP2PReferenceMoniker(string projectPath, Guid projectGuid) {
            ProjectPath = projectPath;
            ProjectGuid = projectGuid;
        }

        internal string ProjectPath { get; private set; }
        public Guid ProjectGuid { get; }

        public override string ToString() {
            return this.ProjectPath;
        }

        public static UnresolvedP2PReferenceMoniker Create(ProjectItem projectItem) {
            // Project is the foreign project guid
            string metadataValue = projectItem.GetMetadataValue("Project");

            Guid projectGuid;
            Guid.TryParse(metadataValue, out projectGuid);

            // Evaluated include is a project relative path like "..\\ProjectA\\ProjectA.csproj"
            return new UnresolvedP2PReferenceMoniker(projectItem.EvaluatedInclude, projectGuid);
        }
    }
}
