using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Aderant.Build.Analyzer.CodeFixes {

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InvalidRegexCodeFix)), Shared]
    public class InvalidRegexCodeFix : CodeFixProvider {

        private const string Title = "Fix regex";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(InvalidRegexRule.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider() {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context) {

            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the invocation expression for Regex.Match().
            var invocationExpression = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => FixRegexAsync(context.Document, invocationExpression, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private async Task<Document> FixRegexAsync(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken) {

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);

            string regex;
            LiteralExpressionSyntax regexLiteral;

            // get regex text and literal
            InvalidRegexRule.TryGetRegex(invocationExpression, semanticModel, out regex, out regexLiteral);

            // create new literal by parsing a string expression with all leading/trailing whitespaces and the configured code formatting
            var newLiteral = SyntaxFactory.ParseExpression(@"""valid regex""")
                .WithLeadingTrivia(regexLiteral.GetLeadingTrivia())
                .WithTrailingTrivia(regexLiteral.GetTrailingTrivia())
                .WithAdditionalAnnotations(Formatter.Annotation);

            // get root node from current document's syntax tree
            var root = await document.GetSyntaxRootAsync(cancellationToken);

            // generate new root node and new document with the newly created root node
            var newRoot = root.ReplaceNode(regexLiteral, newLiteral);
            var newDocument = document.WithSyntaxRoot(newRoot);

            return newDocument;
        }
    }
}