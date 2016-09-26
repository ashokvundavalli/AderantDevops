using System.Collections.Generic;

namespace Aderant.Build.Packaging {
    public sealed class ProductAssemblyResult : IProductAssemblyResult {
        public IEnumerable<string> ThirdPartyLicenses { get; internal set; }
    }
}