using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    public abstract class RuleBase {

        internal abstract DiagnosticSeverity Severity { get; }

        internal abstract string Id { get; }

        internal abstract string Title { get; }

        internal abstract string MessageFormat { get; }

        internal abstract string Description { get; }

        public abstract DiagnosticDescriptor Descriptor { get; }

        public abstract void Initialize(AnalysisContext context);
    }
}