using System;
using System.Collections.Generic;

namespace Aderant.Build.DependencyAnalyzer.Model {
    internal interface IDependencyRef : IEquatable<IDependencyRef> {
        string Name { get; }

        IReadOnlyCollection<IDependencyRef> DependsOn { get; }
    }
}
