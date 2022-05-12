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
using System.Collections.Immutable;
using System.Linq;
using Aderant.Build.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.CSharp {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FieldWrittenFromConstructor : FieldWrittenFrom {

        internal override DiagnosticSeverity Severity { get; } = DiagnosticSeverity.Error;
        internal override string Id { get; } = "field_written_from_constructor";
        internal override string Title { get; } = "Field Written From Constructor";
        internal override string MessageFormat { get; } = "Remove assignment of '{0}'.";
        internal override string Description { get; } = "A field is forbidden from being assigned in an constructor. For a WPF application this ensures the startup and sign-in path is deterministic. For types deriving from AppShellApplication place custom code in OnStartupAsync().";

        protected override bool IsValidCodeBlockContext(SyntaxNode node, ISymbol owningSymbol, SemanticModel semanticModel, AnalyzerOptions analyzerOptions) {
            if (node is ConstructorDeclarationSyntax declaration) {
                var classDeclarationSyntax = declaration.Parent as ClassDeclarationSyntax;

                if (classDeclarationSyntax != null) {
                    var typeList = EditorConfigAppliesToType.GetEditorConfigAppliesToType(analyzerOptions, node.SyntaxTree, SupportedDiagnostics.First().Id);

                    if (typeList != null && typeList.Count == 0) {
                        return false;
                    }

                    var classSymbol = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);
                    if (classSymbol != null) {
                        if (EditorConfigAppliesToType.AppliesToContainsSymbol(typeList, classSymbol)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        protected override string GetDiagnosticMessageArgument(SyntaxNode node, ISymbol owningSymbol, IFieldSymbol field) {
            return field.Name;
        }

    }
}