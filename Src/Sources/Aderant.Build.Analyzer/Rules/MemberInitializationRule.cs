using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SymbolWithInitializer = System.Collections.Generic.KeyValuePair<Microsoft.CodeAnalysis.ISymbol, Microsoft.CodeAnalysis.CSharp.Syntax.EqualsValueClauseSyntax>;

namespace Aderant.Build.Analyzer.Rules {
    /// <summary>
    /// A static field within a C# class deriving from AppShellApplication has no static initializer
    /// of that field
    /// </summary>
    /// <remarks>
    /// <para>A violation of this rule is an issue as it breaks application startup and sign-in.
    /// There is a very particular order to code execution and having static field initialization other than
    /// the preordained startup path in AppShellApplication is not allowed.
    /// </para>
    /// </remarks>
    public class MemberInitializationRule : RuleBase {

        internal const string DiagnosticId = "member_initialization";

        internal override DiagnosticSeverity Severity { get; } = DiagnosticSeverity.Error;

        internal override string Id { get; } = DiagnosticId;

        internal override string Title { get; } = "Illegal member initialization";

        internal override string MessageFormat { get; } = "Remove the member initializer.";

        internal override string Description { get; } = "This type is not allowed to have members initialized outside of the approved method. For a WPF application this ensures the startup and sign-in path is deterministic. For types deriving from AppShellApplication place custom code in OnStartupAsync().";

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(
                c => {
                    var typeList = EditorConfigAppliesToType.GetEditorConfigAppliesToType(c.Options, c.Node.SyntaxTree, DiagnosticId);

                    var declaration = (TypeDeclarationSyntax)c.Node;

                    var symbol = c.SemanticModel.GetDeclaredSymbol(declaration);

                    if (symbol != null) {
                        if (EditorConfigAppliesToType.AppliesToContainsSymbol(typeList, symbol)) {
                            var candidateFields = GetInitializedFieldLikeDeclarations<FieldDeclarationSyntax, IFieldSymbol>(declaration, c.SemanticModel, f => f.Type);
                            var candidateProperties = GetInitializedPropertyDeclarations(declaration, c.SemanticModel);

                            var symbolInitializerPairs = candidateFields.Select(t => new SymbolWithInitializer(t.Symbol, t.Initializer))
                                .Concat(candidateProperties.Select(t => new SymbolWithInitializer(t.Symbol, t.Initializer)))
                                .ToDictionary(t => t.Key, t => t.Value);

                            foreach (var declaredSymbol in symbolInitializerPairs.Keys) {
                                c.ReportDiagnostic(Diagnostic.Create(Descriptor, symbolInitializerPairs[declaredSymbol].GetLocation()));
                            }
                        }
                    }
                }, SyntaxKind.ClassDeclaration);
        }

        private static IEnumerable<DeclarationTuple<TSymbol>> GetInitializedFieldLikeDeclarations<TDeclarationType, TSymbol>(TypeDeclarationSyntax declaration,
            SemanticModel semanticModel, Func<TSymbol, ITypeSymbol> typeSelector)
            where TDeclarationType : BaseFieldDeclarationSyntax
            where TSymbol : class, ISymbol {
            return declaration.Members
                .OfType<TDeclarationType>()
                .Where(fd => !fd.Modifiers.Any(IsConst))
                .SelectMany(fd => fd.Declaration.Variables
                    .Where(v => v.Initializer != null)
                    .Select(v =>
                        new DeclarationTuple<TSymbol> {
                            Initializer = v.Initializer,
                            SemanticModel = semanticModel,
                            Symbol = semanticModel.GetDeclaredSymbol(v) as TSymbol
                        }))
                .Where(t => t.Symbol != null);
        }


        private static bool IsConst(SyntaxToken token) {
            return token.IsKind(SyntaxKind.ConstKeyword);
        }

        // Retrieves the class members which are initialized - instance or static ones, depending on the given modifiers.
        private static IEnumerable<DeclarationTuple<IPropertySymbol>> GetInitializedPropertyDeclarations(TypeDeclarationSyntax declaration,
            SemanticModel semanticModel) {
            return declaration.Members
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => !p.Modifiers.Any(IsConst) &&
                            p.Initializer != null &&
                            IsAutoProperty(p))
                .Select(p =>
                    new DeclarationTuple<IPropertySymbol> {
                        Initializer = p.Initializer,
                        SemanticModel = semanticModel,
                        Symbol = semanticModel.GetDeclaredSymbol(p)
                    })
                .Where(t => t.Symbol != null);
        }

        internal static bool IsAutoProperty(PropertyDeclarationSyntax propertyDeclaration) {
            return propertyDeclaration.AccessorList != null &&
                   propertyDeclaration.AccessorList.Accessors.All(
                       accessor => accessor.Body == null && accessor.ExpressionBody == null);
        }


        private class DeclarationTuple<TSymbol>
            where TSymbol : ISymbol {

            public EqualsValueClauseSyntax Initializer { get; set; }
            public SemanticModel SemanticModel { get; set; }
            public TSymbol Symbol { get; set; }

        }

    }
}