/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2022 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using Aderant.Build.Analyzer;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules
{
    public abstract class FieldWrittenFrom : RuleBase
    {
        public virtual ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
            get {
                return ImmutableArray.Create(Descriptor);
            }
        }

        public sealed override void Initialize(AnalysisContext context)
        {
            context.RegisterCodeBlockStartAction<SyntaxKind>(
               cbc => {
                   if (!IsValidCodeBlockContext(cbc.CodeBlock, cbc.OwningSymbol, cbc.SemanticModel, cbc.Options))
                   {
                       return;
                   }

                   var locationsForFields = new Dictionary<IFieldSymbol, List<Location>>();

                   cbc.RegisterSyntaxNodeAction(
                       c =>
                       {
                           var assignment = (AssignmentExpressionSyntax)c.Node;
                           var expression = assignment.Left;

                           var fieldSymbol = c.SemanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
                           if (fieldSymbol != null)
                           {
                               AddFieldLocation(fieldSymbol, expression.CreateLocation(assignment.OperatorToken), locationsForFields);
                           }
                       },
                       SyntaxKind.SimpleAssignmentExpression,
                       SyntaxKind.AddAssignmentExpression,
                       SyntaxKind.SubtractAssignmentExpression,
                       SyntaxKind.MultiplyAssignmentExpression,
                       SyntaxKind.DivideAssignmentExpression,
                       SyntaxKind.ModuloAssignmentExpression,
                       SyntaxKind.AndAssignmentExpression,
                       SyntaxKind.ExclusiveOrAssignmentExpression,
                       SyntaxKind.OrAssignmentExpression,
                       SyntaxKind.LeftShiftAssignmentExpression,
                       SyntaxKind.RightShiftAssignmentExpression);

                   cbc.RegisterSyntaxNodeAction(
                       c =>
                       {
                           var unary = (PrefixUnaryExpressionSyntax)c.Node;
                           CollectLocationOfField(unary.Operand, locationsForFields, c);
                       },
                       SyntaxKind.PreDecrementExpression,
                       SyntaxKind.PreIncrementExpression);

                   cbc.RegisterSyntaxNodeAction(
                       c =>
                       {
                           var unary = (PostfixUnaryExpressionSyntax)c.Node;
                           CollectLocationOfField(unary.Operand, locationsForFields, c);
                       },
                       SyntaxKind.PostDecrementExpression,
                       SyntaxKind.PostIncrementExpression);

                   cbc.RegisterCodeBlockEndAction(c =>
                   {
                       foreach (var fieldWithLocations in locationsForFields)
                       {
                           var firstPosition = fieldWithLocations.Value.Select(loc => loc.SourceSpan.Start).Min();
                           var location = fieldWithLocations.Value.First(loc => loc.SourceSpan.Start == firstPosition);
                           var message = GetDiagnosticMessageArgument(cbc.CodeBlock, cbc.OwningSymbol, fieldWithLocations.Key);
                           var secondaryLocations = fieldWithLocations.Key.DeclaringSyntaxReferences
                                                                      .Select(x => x.GetSyntax().GetLocation());
                           c.ReportDiagnostic(Diagnostic.Create(SupportedDiagnostics[0], location,
                               additionalLocations: secondaryLocations,
                               messageArgs: message));
                       }
                   });
               });
        }

        protected abstract bool IsValidCodeBlockContext(SyntaxNode node, ISymbol owningSymbol, SemanticModel semanticModel, AnalyzerOptions analyzerOptions);

        protected abstract string GetDiagnosticMessageArgument(SyntaxNode node, ISymbol owningSymbol, IFieldSymbol field);

        private void AddFieldLocation(IFieldSymbol fieldSymbol, Location location, Dictionary<IFieldSymbol, List<Location>> locationsForFields)
        {
            if (fieldSymbol.Type.IsValueType) {
                return;
            }

            if (CollectSystemTypes && fieldSymbol.Type.ToString().StartsWith("System.")) {
                return;
            }

            if (!locationsForFields.ContainsKey(fieldSymbol))
            {
                locationsForFields.Add(fieldSymbol, new List<Location>());
            }

            locationsForFields[fieldSymbol].Add(location);
        }

        public bool CollectSystemTypes { get; set; } = true;

        private void CollectLocationOfField(ExpressionSyntax expression, Dictionary<IFieldSymbol, List<Location>> locationsForFields, SyntaxNodeAnalysisContext context)
        {
            var fieldSymbol = context.SemanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
            if (fieldSymbol != null) {
                AddFieldLocation(fieldSymbol, expression.GetLocation(), locationsForFields);
            }
        }
    }
}