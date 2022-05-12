using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AderantAnalyzer<T> : AderantAnalyzer where T : RuleBase, new() {

        public AderantAnalyzer() : base(new T()) {
        }

    }
}
