using System;

namespace Aderant.Build.DependencyAnalyzer {
    internal static class WellKnownProjectTypeGuids {
        public static Guid[] WebProjectGuids = new[] {
            new Guid("{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}"),
            new Guid("{603C0E0B-DB56-11DC-BE95-000D561079B0}"),
            new Guid("{F85E285D-A4E0-4152-9332-AB1D724D3325}"),
            new Guid("{E53F8FEA-EAE0-44A6-8774-FFD645390401}"),
            new Guid("{E3E379DF-F4C6-4180-9B81-6769533ABE47}"),
            new Guid("{349C5851-65DF-11DA-9384-00065B846F21}"),
        };

        public static Guid TestProject = new Guid("{3AC096D0-A1C2-E12C-1390-A8335801FDAB}");
    }

}
