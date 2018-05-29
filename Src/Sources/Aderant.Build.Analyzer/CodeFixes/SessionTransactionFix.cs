using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Aderant.Build.Analyzer.Extensions;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Aderant.Build.Analyzer.CodeFixes {
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SessionTransactionFix)), Shared]
    public class SessionTransactionFix : CodeFixProvider {
        #region Fields

        private const string title = "Split 'GetSession()' and 'BeginTransaction()'.'";

        #endregion Fields

        #region Properties

        /// <summary>
        /// A list of diagnostic IDs that this provider can provide fixes for.
        /// </summary>
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(CodeQualitySessionTransactionRule.DiagnosticId);

        #endregion Properties

        #region Methods

        /// <summary>
        /// Gets an optional <see cref="T:Microsoft.CodeAnalysis.CodeFixes.FixAllProvider" />
        /// that can fix all/multiple occurrences of diagnostics fixed by this code fix provider.
        /// Return null if the provider doesn't support fix all/multiple occurrences.
        /// Otherwise, you can return any of the well known fix all providers from
        /// <see cref="T:Microsoft.CodeAnalysis.CodeFixes.WellKnownFixAllProviders" />
        /// or implement your own fix all provider.
        /// </summary>
        public sealed override FixAllProvider GetFixAllProvider() {
            return WellKnownFixAllProviders.BatchFixer;
        }

        /// <summary>
        /// Computes one or more fixes for the specified <see cref="T:Microsoft.CodeAnalysis.CodeFixes.CodeFixContext" />.
        /// </summary>
        /// <param name="context">
        /// A <see cref="T:Microsoft.CodeAnalysis.CodeFixes.CodeFixContext" /> containing context information about the diagnostics to fix.
        /// The context must only contain diagnostics with an <see cref="P:Microsoft.CodeAnalysis.Diagnostic.Id" />
        /// included in the <see cref="P:Microsoft.CodeAnalysis.CodeFixes.CodeFixProvider.FixableDiagnosticIds" /> for the current provider.
        /// </param>
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context) {
            // Validate document.
            var document = context.Document;

            if (document == null) {
                return;
            }

            // Retrieve the syntax root.
            SyntaxNode syntaxRoot = await document.GetSyntaxRootAsync(context.CancellationToken);

            if (syntaxRoot == null) {
                return;
            }

            // Iterate through each diagnostic and register a code action that will invoke the code fix.
            foreach (var diagnostic in context.Diagnostics) {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        cancellationToken => SplitSessionAndTransactionAsync(
                            diagnostic,
                            document,
                            syntaxRoot,
                            cancellationToken),
                        title),
                    diagnostic);
            }
        }

        /// <summary>
        /// Asynchronously splits the session and transaction,
        /// returning a modified copy of the original code document.
        /// </summary>
        /// <param name="diagnostic">The diagnostic.</param>
        /// <param name="document">The document.</param>
        /// <param name="syntaxRoot">The syntax root.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private static async Task<Document> SplitSessionAndTransactionAsync(
            Diagnostic diagnostic,
            Document document,
            SyntaxNode syntaxRoot,
            CancellationToken cancellationToken) {
            // Validate task results.
            if (syntaxRoot == null) {
                return document;
            }

            // Get the 'broken' using statement.
            int location = diagnostic.Location.SourceSpan.Start;

            var usingStatementSyntax = syntaxRoot
                .FindToken(location)
                .Parent
                .GetAncestorOrSelfOfType<UsingStatementSyntax>();

            // Validate the statement was found.
            if (usingStatementSyntax == null) {
                return document;
            }

            // Get the expression representing the using expression's code block.
            var codeBlocks = new List<BlockSyntax>(RuleBase.DefaultCapacity);
            RuleBase.GetExpressionsFromChildNodes(ref codeBlocks, usingStatementSyntax);

            // Validate that there was code to wrap.
            if (codeBlocks.Count < 1) {
                return document;
            }

            var codeBlock = codeBlocks[0];

            // Recursively examine the using statement's child nodes,
            // looking for a variable declarator expression syntax.
            // If one is found, then the transaction object is being assigned to a variable,
            // and the new using statement must do the same. The 'StopNode' is used to ensure
            // that only the contents of the 'using ()' expression are examined,
            // and not the contents of the code block within the using statement.
            var variableDeclaratorExpressions = new List<VariableDeclaratorSyntax>(1);
            RuleBase.GetExpressionsFromChildNodes(ref variableDeclaratorExpressions, usingStatementSyntax, codeBlock);

            // Attempt to find the name of the transaction variable.
            string transactionVariableName = variableDeclaratorExpressions.Count > 0
                ? variableDeclaratorExpressions[0].Identifier.Text
                : string.Empty;

            // Create a new using statement.
            // If a variable name is provided, it will be included,
            // otherwise there will be no local variable created..
            var newUsingStatementSyntax = await Task.Run(
                () => CreateUsingStatementSyntax(
                    codeBlock,
                    transactionVariableName),
                cancellationToken);

            // Create a new syntax tree, after replacing the existing single using statement,
            // with the new dual using statement fixed syntax.
            var newSyntaxRoot = syntaxRoot.ReplaceNode(usingStatementSyntax, newUsingStatementSyntax);

            // Return a new compilation document, built from the new and fixed syntax tree.
            return document.WithSyntaxRoot(newSyntaxRoot);
        }

        /// <summary>
        /// Creates the using statement syntax.
        /// </summary>
        /// <param name="codeBlock">The code block.</param>
        /// <param name="transactionVariableName">Name of the transaction variable.</param>
        private static UsingStatementSyntax CreateUsingStatementSyntax(
            StatementSyntax codeBlock,
            string transactionVariableName) {
            // Final result:
            // If a variable name exists:
            // using (var session = context.Repository.GetSession()) {
            //     using (var transactionVariableName = session.BeginTransaction()) {
            //         // Existing code.
            //     }
            // }
            //
            // If a variable name does not exist:
            // using (var session = context.Repository.GetSession()) {
            //     using (session.BeginTransaction()) {
            //         // Existing code.
            //     }
            // }

            UsingStatementSyntax usingStatement = UsingStatement(codeBlock);

            if (string.IsNullOrWhiteSpace(transactionVariableName)) {
                // using (session.BeginTransaction())
                usingStatement = usingStatement
                    .WithExpression(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName("session"),
                                IdentifierName("BeginTransaction"))))
                    .WithCloseParenToken(
                        Token(
                            TriviaList(),
                            SyntaxKind.CloseParenToken,
                            TriviaList(
                                Space)));
            } else {
                // using (var transactionVariableName = session.BeginTransaction())
                usingStatement = usingStatement
                    .WithDeclaration(
                        VariableDeclaration(
                                IdentifierName(
                                    Identifier(
                                        TriviaList(),
                                        "var",
                                        TriviaList(
                                            Space))))
                            .WithVariables(
                                SingletonSeparatedList(
                                    VariableDeclarator(
                                            Identifier(
                                                TriviaList(),
                                                transactionVariableName,
                                                TriviaList(
                                                    Space)))
                                        .WithInitializer(
                                            EqualsValueClause(
                                                    InvocationExpression(
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName("session"),
                                                            IdentifierName("BeginTransaction"))))
                                                .WithEqualsToken(
                                                    Token(
                                                        TriviaList(),
                                                        SyntaxKind.EqualsToken,
                                                        TriviaList(
                                                            Space)))))));
            }

            usingStatement = usingStatement
                .WithUsingKeyword(
                    Token(
                        TriviaList(),
                        SyntaxKind.UsingKeyword,
                        TriviaList(
                            Space)))
                .WithCloseParenToken(
                    Token(
                        TriviaList(),
                        SyntaxKind.CloseParenToken,
                        TriviaList(
                            Space)));

            usingStatement = UsingStatement(
                    Block(
                            SingletonList<StatementSyntax>(usingStatement))
                        .WithOpenBraceToken(
                            Token(
                                TriviaList(),
                                SyntaxKind.OpenBraceToken,
                                TriviaList(
                                    LineFeed))))
                .WithUsingKeyword(
                    Token(
                        TriviaList(),
                        SyntaxKind.UsingKeyword,
                        TriviaList(
                            Space)))
                .WithDeclaration(
                    VariableDeclaration(
                            IdentifierName(
                                Identifier(
                                    TriviaList(),
                                    "var",
                                    TriviaList(
                                        Space))))
                        .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                        Identifier(
                                            TriviaList(),
                                            "session",
                                            TriviaList(
                                                Space)))
                                    .WithInitializer(
                                        EqualsValueClause(
                                                InvocationExpression(
                                                    MemberAccessExpression(
                                                        SyntaxKind.SimpleMemberAccessExpression,
                                                        MemberAccessExpression(
                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                            IdentifierName("context"),
                                                            IdentifierName("Repository")),
                                                        IdentifierName("GetSession"))))
                                            .WithEqualsToken(
                                                Token(
                                                    TriviaList(),
                                                    SyntaxKind.EqualsToken,
                                                    TriviaList(
                                                        Space)))))))
                .WithCloseParenToken(
                    Token(
                        TriviaList(),
                        SyntaxKind.CloseParenToken,
                        TriviaList(
                            Space)));

            return usingStatement.WithAdditionalAnnotations(Formatter.Annotation);
        }

        #endregion Methods
    }
}
