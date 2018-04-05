using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    #region Enumerations

    /// <summary>
    /// The type of the declared collection.
    /// </summary>
    public enum DeclarationCollectionType {
        Collection,
        Dictionary,
        List,
        Queue,
        None
    }

    /// <summary>
    /// The type of expression for use in ordering expressions.
    /// </summary>
    public enum ExpressionType {
        None,
        Assignment,
        AssignmentNull,
        CollectionAdd,
        Dispose,
        Using,
        UsingAssignment,
        Exit
    }

    #endregion Enumerations

    #region Structures

    /// <summary>
    /// Data container for disposable property and field declarations.
    /// </summary>
    public struct DisposableDeclaration {
        public DisposableDeclaration(
            SyntaxNode node,
            string name,
            Location location,
            bool isAssignedAtDeclaration,
            bool isStatic,
            DeclarationCollectionType collection) {
            Node = node;
            Name = name;
            IsAssignedAtDeclaration = isAssignedAtDeclaration;
            IsStatic = isStatic;
            Location = location;
            CollectionType = collection;
        }

        public DeclarationCollectionType CollectionType { get; }

        public Location Location { get; }

        public bool IsAssignedAtDeclaration { get; }

        public bool IsStatic { get; }

        public string Name { get; }

        public SyntaxNode Node { get; }
    }

    /// <summary>
    /// Data container for field and property assignments.
    /// </summary>
    public struct AssignmentData {
        public AssignmentData(
            IdentifierNameSyntax target,
            IEnumerable<IdentifierNameSyntax> values,
            ConstructorDeclarationSyntax constructor,
            IEnumerable<ParameterSyntax> parameters) {
            Constructor = constructor;
            Target = target;
            Values = values.ToArray();
            Parameters = parameters.ToArray();
        }

        public ConstructorDeclarationSyntax Constructor { get; }

        public ParameterSyntax[] Parameters { get; }

        public IdentifierNameSyntax Target { get; }

        public IdentifierNameSyntax[] Values { get; }
    }

    #endregion Structures
}
