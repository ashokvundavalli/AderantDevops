using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace Aderant.Build.Analyzer.Rules {
    public class WpfApplicationDefinition : RuleBase {

        internal override DiagnosticSeverity Severity { get; } = DiagnosticSeverity.Error;

        internal override string Id { get; } = "wpf_application_definition";

        internal override string Title { get; } = "WPF application must use ApplicationDefinition for App.xaml";

        internal override string MessageFormat { get; } = "Ensure App.xaml is set to ApplicationDefinition.";

        internal override string Description { get; } = "A WPF application must use ApplicationDefinition to ensure the startup and sign-in path is deterministic. For types deriving from AppShellApplication place custom code in OnStartupAsync().";

        public override void Initialize(AnalysisContext context) {
            context.RegisterCompilationAction(c => {
                IMethodSymbol entryPoint = c.Compilation.GetEntryPoint(CancellationToken.None);
                if (entryPoint != null) {
                    var appliesToType = EditorConfigAppliesToType.GetEditorConfigAppliesToType(c.Options, entryPoint.DeclaringSyntaxReferences[0].SyntaxTree, Id);

                    var baseType = (entryPoint.ContainingSymbol as ITypeSymbol)?.BaseType;
                    if (EditorConfigAppliesToType.AppliesToContainsSymbol(appliesToType, baseType)) {
                        var span = entryPoint.Locations[0].GetMappedLineSpan();
                        if (span.IsValid) {
                            if (span.Path.EndsWith("g.cs")) {
                                var attributes = entryPoint.GetAttributes();
                                if (!attributes.Any(x => x.AttributeClass != null && string.Equals(x.AttributeClass.ToDisplayString(), "System.CodeDom.Compiler.GeneratedCodeAttribute"))) {
                                    ReportDiagnostic(c, Descriptor, entryPoint.Locations[0], entryPoint.DeclaringSyntaxReferences[0].GetSyntax());
                                }
                            } else {
                                ReportDiagnostic(c, Descriptor, entryPoint.Locations[0], entryPoint.DeclaringSyntaxReferences[0].GetSyntax());
                            }
                        }
                    }
                }
            });
        }
    }
}