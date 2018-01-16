using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class InvalidLogMessageRule : RuleBase {
        internal const string DiagnosticId = "Aderant_InvalidInvalidLogMessage";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        internal override string Id => DiagnosticId;
        
        internal override string Title => "Invalid log message template";
        internal override string MessageFormat => "{0} message parameters were provided for the invalid log message template '{1}'";
        internal override string Description => "The number of message parameters does not match the log message template.";


        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context) {

            var invocationExpression = (InvocationExpressionSyntax)context.Node;

            string logTemplate;
            LiteralExpressionSyntax logTemplateLiteral;
            int? paramsCount;
            bool lastParameterIsException;
            if (!TryGetLogTemplate(invocationExpression, context.SemanticModel, out logTemplate, out logTemplateLiteral, out paramsCount, out lastParameterIsException)) {
                return;
            }

            var logMessageTemplateIsInvalid = false;

            // Test if the provided log message template is a valid one.
            for (int i = 0; i < paramsCount; i++) {
                if (!Regex.IsMatch(logTemplate, string.Concat("{", i, "(:.+)?}"))) {

                    // allow last parameter to be an exception
                    if (i == paramsCount - 1 && lastParameterIsException) {
                        break;
                    }

                    // If the log message template is invalid, produce a diagnostic.
                    logMessageTemplateIsInvalid = true;
                    break;
                }
            }

            // If the log message template is invalid (too many or too less parameters), produce a diagnostic.
            if (logMessageTemplateIsInvalid || logTemplate.Contains(string.Concat("{", paramsCount, "}"))) {
                ReportDiagnostic(context, Descriptor, invocationExpression.GetLocation(), invocationExpression, paramsCount, logTemplate);
            }
        }

        internal static bool TryGetLogTemplate(
            InvocationExpressionSyntax invocationExpression, 
            SemanticModel semanticModel, 
            out string logTemplate, 
            out LiteralExpressionSyntax logTemplateLiteral,
            out int? paramsCount,
            out bool lastParameterIsException) {

            logTemplate = null;
            logTemplateLiteral = null;
            paramsCount = 0;
            lastParameterIsException = false;

            // Check if this is a call to a method named Log as in ILogger.Log.
            var memberAccessExpression = invocationExpression.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpression?.Name.ToString() != "Log") {
                return false;
            }

            // Check if the overloaded method Log(LogLevel, string, params object[]) is being called.
            var memberSymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol as IMethodSymbol;
            if (memberSymbol?.ToDisplayString() != "Aderant.Framework.Logging.ILogWriter.Log(Aderant.Framework.Logging.LogLevel, string, params object[])") {
                return false;
            }

            // Check if there are at least 2 arguments (or more) provided.
            ArgumentListSyntax argumentList = invocationExpression.ArgumentList;
            if ((argumentList?.Arguments.Count ?? 0) < 2) {
                return false;
            }
            paramsCount = argumentList?.Arguments.Count - 2;

            // Check if the second argument is a string literal which we can further examine.
            logTemplateLiteral = argumentList?.Arguments[1].Expression as LiteralExpressionSyntax;
            if (logTemplateLiteral == null) {
                return false;
            }
            var logTemplateOptional = semanticModel.GetConstantValue(logTemplateLiteral);
            if (!logTemplateOptional.HasValue) {
                return false;
            }
            logTemplate = logTemplateOptional.Value as string;
            if (logTemplate == null) {
                return false;
            }

            var lastParameterIdentifier = argumentList?.Arguments.Last().Expression as IdentifierNameSyntax;
            if (lastParameterIdentifier != null) {

                var symbolType = semanticModel.GetTypeInfo(lastParameterIdentifier).Type;

                if (symbolType != null) { 
                    while (symbolType.ContainingNamespace?.Name != "System" || symbolType.Name != "Object") {

                        // check if the type inherits from System.Exception
                        if (symbolType.ContainingNamespace?.Name == "System" && symbolType.Name == "Exception") {
                            lastParameterIsException = true;
                            break;
                        }

                        // check if the type is an interface
                        if (symbolType.BaseType == null) {
                            break;
                        }

                        // check the base type
                        symbolType = symbolType.BaseType;
                    }
                }
            }

            return true;
        }
    }
}
