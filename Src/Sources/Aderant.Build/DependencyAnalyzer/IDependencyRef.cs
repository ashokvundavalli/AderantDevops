using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aderant.Build.DependencyAnalyzer {
    public interface IDependencyRef : IEquatable<IDependencyRef> {
        string Name { get; }

        void Accept(GraphVisitorBase visitor, StreamWriter outputFile);
    }

   
}
