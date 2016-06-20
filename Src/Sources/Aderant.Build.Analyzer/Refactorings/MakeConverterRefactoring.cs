using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;

namespace Aderant.Build.Analyzer.Refactorings {

    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(MakeConverterRefactoring)), Shared]
    internal class MakeConverterRefactoring : CodeRefactoringProvider {

        private const string Title = "Make Converter class";

        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context) {

            // Get the root node of the syntax tree.
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(context.Span);

            // Is this a class statement node?
            var classDeclaration = node as ClassDeclarationSyntax;
            if (classDeclaration == null)
            {
                return;
            }

            // If so, create an action to offer a refactoring.
            var action = CodeAction.Create(
                title: Title, 
                createChangedDocument: c => ReverseTypeNameAsync(context.Document, classDeclaration, c),
                equivalenceKey: Title);

            // Register this code action.
            context.RegisterRefactoring(action);
        }

        private async Task<Document> ReverseTypeNameAsync(Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken) {

            // The class definition represented as source text.
            string newImplementation = @"public class MyConverter : IValueConverter {
    }";

            // 1. ParseSyntaxTree() gets a new SyntaxTree form the source text
            // 2. GetRoot() gets the root node of the tree
            // 3. OfType<ClassDeclarationSyntax>().FirstOrDefault() retrieves the only class definition in the tree
            // 4. WithAdditionalAnnotations() is invoked for code formatting
            var newClassNode = SyntaxFactory.ParseSyntaxTree(newImplementation)
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault()
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation);

            // Get the root SyntaxNode of the document.
            var root = await document.GetSyntaxRootAsync(cancellationToken);

            // Generate a new CompilationUnitSyntac (which represents a code file) replacing the old class with the new one.
            var newRoot = (CompilationUnitSyntax) root.ReplaceNode(classDeclaration, newClassNode).NormalizeWhitespace();

            // Detect if a using System.Windows.Data directive already exists.
            if (!newRoot.Usings.Any(u => u.Name.ToFullString() == "System.Windows.Data")) {

                // If not, add one
                newRoot =
                    newRoot.AddUsings(
                        SyntaxFactory.UsingDirective(
                            SyntaxFactory.QualifiedName(
                                SyntaxFactory.IdentifierName("System"),
                                SyntaxFactory.IdentifierName("Windows.Data"))));
            }

            // Generate a new document based on the new SyntaxNode.
            var newDocument = document.WithSyntaxRoot(newRoot);

            // Return the new document.
            return newDocument;
        }
    }
}