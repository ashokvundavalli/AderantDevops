using System;
using System.Collections.Immutable;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    /// <summary>
    /// This rule makes sure that the type parameters in IQuery.Lynq<> and IQuery.Get<> are classes instead of interfaces.
    /// The bug that prompted me to develop this rule has since been fixed so it is here mostly as an example.
    /// </summary>
    internal class NHibernateRetrieveTypeNotInterfaceRule : RuleBase {
        internal const string DiagnosticId = "Aderant_NHibernateDoesNotSupportInterface";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        internal override string Id => DiagnosticId;

        internal override string Title => "Avoid Interface with NHibernate Get<> Linq<>";
        internal override string MessageFormat => "Avoid using an interface as a type argument when retrieving data with NHibernate.";
        internal override string Description => "NHibernate does not officially support using an interface as the type argument when retrieving data.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        /// <summary>
        /// Initialize this rule to register it in the context.
        /// </summary>
        /// <param name="context"></param>
        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.TypeArgumentList);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context) {
            TypeArgumentListSyntax node = context.Node as Microsoft.CodeAnalysis.CSharp.Syntax.TypeArgumentListSyntax;
            //Get<> and Linq<> can only take one type argument, so we aren't interested in anything else.
            if (node?.Arguments.Count != 1) {
                return;
            }

            ITypeSymbol actualTypeInGeneric = context.SemanticModel.GetTypeInfo(node.Arguments[0]).Type;
            //If the generic type parameter is not an interface, then this is the correct usage of Linq<> and Get<>
            if (actualTypeInGeneric.TypeKind != TypeKind.Interface) {
                return;
            }

            var memberAccessExpression = node.GetAncestorOfType<MemberAccessExpressionSyntax>();

            //Check that the method name we have is actually Get<> or Linq<>
            string name = memberAccessExpression?.Name?.ToString();
            if (string.IsNullOrWhiteSpace(name) || (!name.StartsWith("Get<") && !name.StartsWith("Linq<"))) {
                return;
            }

            //Check that the containing type of this method is IQuery, or implements IQuery
            var symbolInfoOfMethod = context.SemanticModel.GetSymbolInfo(memberAccessExpression);
            IMethodSymbol memberAccessSymbol = symbolInfoOfMethod.Symbol as IMethodSymbol;
            if (memberAccessSymbol != null) {
                ImmutableArray<INamedTypeSymbol> interfaces = memberAccessSymbol.ReceiverType.AllInterfaces;
                if (!memberAccessSymbol.ReceiverType.ToString().Equals("Aderant.Framework.Persistence.IQuery", StringComparison.Ordinal) && interfaces.All(
                        typeSymbol => !string.Equals("Aderant.Framework.Persistence.IQuery", typeSymbol.OriginalDefinition.ToString(), StringComparison.Ordinal))) {
                    return;
                }
            } else {
                //The type did not resolve correctly, but if we only have one candidate, then it is probably that one.
                ImmutableArray<ISymbol> candidates = symbolInfoOfMethod.CandidateSymbols;
                if (candidates.Length != 1) {
                    return;
                }

                bool thisCandidateIsIquery = false;
                bool thisCandidateImplementsIquery = false;

                INamedTypeSymbol containingType = candidates.FirstOrDefault()?.ContainingType;
                if (containingType == null) { //While this should be impossible, I've put it here in case someone else changes the other if statement.
                    return; //Unable to determine this type if we have no candidate.
                }
                //Check to see if the interface we have is Aderant.Framework.Persistence.IQuery
                thisCandidateIsIquery = containingType.Name.Equals("IQuery", StringComparison.Ordinal) &&
                                        containingType.ContainingNamespace.ToString().Equals("Aderant.Framework.Persistence", StringComparison.Ordinal);
                if (!thisCandidateIsIquery) {
                    //Check to see if the type we have implements Aderant.Framework.Persistence.IQuery
                    foreach (var iface in containingType.AllInterfaces) {
                        if (iface.Name.Equals("IQuery") && iface.ContainingNamespace.ToString().Equals("Aderant.Framework.Persistence")) {
                            thisCandidateImplementsIquery = true;
                            break;
                        }
                    }
                }

                if (!thisCandidateIsIquery && !thisCandidateImplementsIquery) {
                    return; //It is not of the datatype we care about, so this is not one of the methods we are worried about.
                }
            }

            ReportDiagnostic(context, Descriptor, node.Arguments[0].GetLocation(), node.Arguments[0]);
        }

    }
}
