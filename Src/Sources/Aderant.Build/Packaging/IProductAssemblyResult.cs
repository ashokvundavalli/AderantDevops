using System.Collections.Generic;

namespace Aderant.Build.Packaging {
    public interface IProductAssemblyResult {
        IReadOnlyCollection<string> ThirdPartyLicenses { get; }
    }
}
