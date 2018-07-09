using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.Model {
    internal interface IDependencyRef : IEquatable<IDependencyRef> {
        string Name { get; }

        IReadOnlyCollection<IDependencyRef> DependsOn { get; }

        void AddDependency(IDependencyRef dependency);

        void Accept(GraphVisitorBase visitor, StreamWriter outputFile);
    }

}
