using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Aderant.Build.Analyzer {
    internal static class INamedTypeSymbolExtensions {

        public static IEnumerable<INamedTypeSymbol> GetBaseTypesAndThis(this INamedTypeSymbol type) {
            INamedTypeSymbol current = type;
            while (current != null) {
                yield return current;
                current = current.BaseType;
            }
        }

    }
}