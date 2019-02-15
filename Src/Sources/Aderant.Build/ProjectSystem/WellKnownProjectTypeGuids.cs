using System;
using System.Linq;

namespace Aderant.Build.ProjectSystem {
    internal static class WellKnownProjectTypeGuids {
        public static Guid[] WebProjectGuids { get; } = new[] {
            new Guid("{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}"),
            new Guid("{603C0E0B-DB56-11DC-BE95-000D561079B0}"),
            new Guid("{F85E285D-A4E0-4152-9332-AB1D724D3325}"),
            new Guid("{E53F8FEA-EAE0-44A6-8774-FFD645390401}"),
            new Guid("{E3E379DF-F4C6-4180-9B81-6769533ABE47}"),
            new Guid("{349C5851-65DF-11DA-9384-00065B846F21}")
        };

        public static Guid WorkflowFoundation { get; } = new Guid("{32f31d43-81cc-4c15-9de6-3fc5453562b6}");
        public static Guid TestProject { get; } = new Guid("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}");
        public static Guid VisualStudioToolsForOffice { get; } = new Guid("{BAA0C2D2-18E2-41B9-852F-F413020CAA33}");
    }

    internal static class ConfiguredProjectExtensions {

        public static bool IsWorkflowProject(this ConfiguredProject project) {
            return project.ProjectTypeGuids != null && project.ProjectTypeGuids.Contains(WellKnownProjectTypeGuids.WorkflowFoundation);
        }
    }
}
