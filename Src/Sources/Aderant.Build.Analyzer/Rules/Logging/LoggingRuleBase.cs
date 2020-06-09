using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Aderant.Build.Analyzer.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aderant.Build.Analyzer.Rules.Logging {
    public abstract class LoggingRuleBase : RuleBase {
        private const string signatureStartsWith = "Aderant.Framework.Logging.ILogWriter.Log(Aderant.Framework.Logging.LogLevel, ";

        private const string signatureException = signatureStartsWith + "System.Exception)";
        private const string signatureMessage = signatureStartsWith + "string)";
        private const string signatureMessageException = signatureStartsWith + "string, System.Exception)";
        private const string signatureMessageParams = signatureStartsWith + "string, params object[])";

        private const string searchPattern = @"(?<!\{)(?>\{\{)*\{\d(.*?)";

        /// <summary>
        /// Enumeration of possible log methods.
        /// </summary>
        protected enum LogMethodSignature {
            INVALID = -1,

            None,
            Exception,
            Message,
            MessageException,
            MessageParams,

            MAX
        }

        /// <summary>
        /// Returns a <see cref="LogMethodSignature" /> indicating the type of log method that was provided.
        /// Otherwise returns 'None' if method was not a log method.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        protected static LogMethodSignature GetLogMethodSignature(IMethodSymbol symbol) {
            var originalDefinition = symbol?
                .OriginalDefinition?
                .ToDisplayString();

            switch (originalDefinition) {
                case signatureException: {
                    return LogMethodSignature.Exception;
                }
                case signatureMessage: {
                    return LogMethodSignature.Message;
                }
                case signatureMessageException: {
                    return LogMethodSignature.MessageException;
                }
                case signatureMessageParams: {
                    return LogMethodSignature.MessageParams;
                }
                default: {
                    return LogMethodSignature.None;
                }
            }
        }

        /// <summary>
        /// Gets the interpolation template arguments found
        /// within the specified method <see cref="ArgumentSyntax" />.
        /// </summary>
        /// <param name="argument">The argument.</param>
        /// <param name="model">The model.</param>
        protected static IEnumerable<string> GetInterpolationTemplateArguments(ArgumentSyntax argument, SemanticModel model) {
            string template = GetInterpolationTemplate(argument);

            if (template == null) {
                var identifier = UnwrapParenthesizedExpressionDescending(argument.Expression) as IdentifierNameSyntax;

                if (identifier == null) {
                    return null;
                }

                var symbol = model.GetSymbolInfo(argument.Expression).Symbol;

                if (symbol is ILocalSymbol) {
                    var localSymbol = symbol as ILocalSymbol;

                    if (!localSymbol.IsConst) {
                        return null;
                    }

                    var methodDeclaration = argument
                        .Expression
                        .GetAncestorOfType<MethodDeclarationSyntax>();

                    if (methodDeclaration == null) {
                        return null;
                    }

                    var declarations = new List<LocalDeclarationStatementSyntax>();
                    GetExpressionsFromChildNodes(ref declarations, methodDeclaration);

                    var variable = declarations
                        .SelectMany(declaration => declaration.Declaration.Variables)
                        .FirstOrDefault(declaration => declaration.Identifier.Text == identifier.Identifier.Text);

                    template = (variable?.Initializer.Value as LiteralExpressionSyntax)?.Token.Text;
                }

                if (symbol is IFieldSymbol) {
                    var localSymbol = symbol as IFieldSymbol;

                    if (!localSymbol.IsConst) {
                        return null;
                    }

                    var classDeclaration = argument
                        .Expression
                        .GetAncestorOfType<ClassDeclarationSyntax>();

                    if (classDeclaration == null) {
                        return null;
                    }

                    var declarations = new List<FieldDeclarationSyntax>();
                    GetExpressionsFromChildNodes(ref declarations, classDeclaration);

                    var variable = declarations
                        .SelectMany(declaration => declaration.Declaration.Variables)
                        .FirstOrDefault(declaration => declaration.Identifier.Text == identifier.Identifier.Text);

                    template = (variable?.Initializer.Value as LiteralExpressionSyntax)?.Token.Text;
                }
            }

            return template == null
                ? null
                : GetInterpolationTemplateArguments(template);
        }

        /// <summary>
        /// Gets the interpolation template arguments found
        /// within the specified template <see cref="string" />.
        /// </summary>
        /// <param name="template">The template.</param>
        protected static IEnumerable<string> GetInterpolationTemplateArguments(string template) {
            return template != null
                ? Regex.Matches(template, searchPattern)
                    .OfType<Match>()
                    .Select(match => match.Value)
                    .Distinct()
                : Enumerable.Empty<string>();
        }

        /// <summary>
        /// Determines if the specified <see cref="ArgumentSyntax" />
        /// has a data type of <see cref="System.Exception" />.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="argument">The argument.</param>
        protected bool GetIsArgumentException(SemanticModel model, ArgumentSyntax argument) {
            var symbol = model
                .GetTypeInfo(UnwrapParenthesizedExpressionDescending(argument.Expression))
                .Type;

            if (symbol == null) {
                return false;
            }

            while (true) {
                if (symbol.OriginalDefinition?.ToDisplayString() == "System.Exception") {
                    return true;
                }

                if (symbol.BaseType == null) {
                    return false;
                }

                symbol = symbol.BaseType;
            }
        }

        /// <summary>
        /// Returns the number of items provided as the 'params object[]' argument to the specified Log method.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="method">The method.</param>
        protected int GetLogMethodParametersCount(SemanticModel model, InvocationExpressionSyntax method) {
            var arguments = method.ArgumentList.Arguments;

            // Ignore the first two arguments: the LogLevel, and the string template.
            int argumentCount = arguments.Count - 2;

            // If the last argument is an exception, it won't be interpolated into the template.
            if (GetIsArgumentException(model, arguments[arguments.Count - 1])) {
                --argumentCount;
            }

            return argumentCount;
        }

        /// <summary>
        /// Determines if the specified <see cref="MemberAccessExpressionSyntax"/> is a call to 'TextTranslator.Current'.
        /// </summary>
        /// <param name="memberAccessExpression">The member access expression.</param>
        protected static bool GetIsTextTranslation(MemberAccessExpressionSyntax memberAccessExpression) {
            const string textTranslator = "TextTranslator";
            const string current = "Current";
            const string translate = "Translate";

            if (memberAccessExpression == null) {
                return false;
            }

            var nameSyntax = memberAccessExpression.Expression as IdentifierNameSyntax;

            if (string.Equals(
                nameSyntax?.Identifier.Text,
                textTranslator,
                StringComparison.Ordinal)) {
                return string.Equals(
                    memberAccessExpression.Name.Identifier.Text,
                    current,
                    StringComparison.Ordinal);
            }

            var childMemberAccessExpression = memberAccessExpression.Expression as MemberAccessExpressionSyntax;

            if (childMemberAccessExpression == null) {
                return false;
            }

            nameSyntax = childMemberAccessExpression.Expression as IdentifierNameSyntax;

            return
                string.Equals(
                    nameSyntax?.Identifier.Text,
                    textTranslator,
                    StringComparison.Ordinal) &&
                string.Equals(
                    childMemberAccessExpression.Name.Identifier.Text,
                    current,
                    StringComparison.Ordinal) &&
                string.Equals(
                    memberAccessExpression.Name.Identifier.Text,
                    translate,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets the string interpolation template from the specified <see cref="ArgumentSyntax"/>.
        /// </summary>
        /// <param name="argument">The argument.</param>
        private static string GetInterpolationTemplate(ArgumentSyntax argument) {
            var nodes = new List<SyntaxNode>();
            GetExpressionsFromChildNodes(ref nodes, argument);

            if (nodes.OfType<MemberAccessExpressionSyntax>().Any(GetIsTextTranslation)) {
                return null;
            }

            var LiteralExpressions = new List<LiteralExpressionSyntax>(nodes.OfType<LiteralExpressionSyntax>());

            if (LiteralExpressions.Count < 1) {
                return null;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < LiteralExpressions.Count; ++i) {
                builder.Append(LiteralExpressions[i].Token.Text);
            }

            return builder.ToString();
        }
    }
}
