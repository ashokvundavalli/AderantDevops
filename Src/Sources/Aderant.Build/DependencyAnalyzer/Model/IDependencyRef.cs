﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer.Model {
    internal interface IDependencyRef : IEquatable<IDependencyRef> {
        string Name { get; }
        
        ICollection<IDependencyRef> DependsOn { get; }
        
        void Accept(GraphVisitorBase visitor, StreamWriter outputFile);
    }

}
