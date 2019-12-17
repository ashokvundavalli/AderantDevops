using System.Collections.Generic;

namespace Aderant.Build.Packaging {
    public sealed class ProductAssemblyResult : IProductAssemblyResult {
        public IReadOnlyCollection<string> ThirdPartyLicenses { get; internal set; }
    }
}
