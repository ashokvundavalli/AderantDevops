using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class LowerCaseTypesRule : RuleBase {
        internal const string DiagnosticId = "Aderant_LowerCaseTypes";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;
        internal override string Id => DiagnosticId;
        
        internal override string Title => "Type name contains lowercase letters";
        internal override string MessageFormat => "Type name '{0}' contains lowercase letters";
        internal override string Description => "Type names should be all uppercase.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            id: Id,
            title: Title,
            messageFormat: MessageFormat,
            category: AnalyzerCategory.Naming,
            defaultSeverity: Severity,
            isEnabledByDefault: true,
            description: Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }
        
        private void AnalyzeSymbol(SymbolAnalysisContext context) {

            var namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            // Find just those named type symbols with names containing lowercase letters.
            if (namedTypeSymbol.Name.ToCharArray().Any(char.IsLower)) {

                // For all such symbols, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Descriptor, namedTypeSymbol.Locations.Last(), namedTypeSymbol.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
