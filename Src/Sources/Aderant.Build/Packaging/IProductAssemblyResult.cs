using System.Collections.Generic;

namespace Aderant.Build.Packaging {
    public interface IProductAssemblyResult {
        IEnumerable<string> ThirdPartyLicenses { get; }
    }
}