using System;
using System.Collections.Generic;
using System.IO;

namespace Aderant.Build.DependencyAnalyzer {
    public interface IDependencyRef : IEquatable<IDependencyRef> {
        string Name { get; }
        ReferenceType Type { get; }

        ICollection<IDependencyRef> DependsOn { get; }
        
        void Accept(GraphVisitorBase visitor, StreamWriter outputFile);
    }

    public enum ReferenceType {
        AssemblyRef,
        DirectoryNode,
        ExpertModule,
        ModuleRef,
        ProjectRef,
        VisualStudioProject,
        ThirdPartyModule,
        WebModule,
        TextTemplate
    }
}
