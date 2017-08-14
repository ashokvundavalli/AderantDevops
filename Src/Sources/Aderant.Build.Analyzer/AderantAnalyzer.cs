using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AderantAnalyzer : DiagnosticAnalyzer {
        private readonly List<RuleBase> rules = new List<RuleBase> {
            new InvalidRegexRule(),
            new InvalidLogMessageRule(),
            new PropertyChangedNoStringRule(),
            new PropertyChangedNoStringNonFixableRule(),
            new SetPropertyValueNoStringRule(),
            new SetPropertyValueNoStringNonFixableRule(),
            new SqlInjectionErrorRule(),
            new QueryServiceQueryAllRule()
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer"/> class.
        /// </summary>
        /// <param name="injectedRules">The rules to inject (for unit testing of possibly disabled rules).</param>
        internal AderantAnalyzer(params RuleBase[] injectedRules) {
            foreach (var injectedRule in injectedRules) {
                if (rules.All(ruleBase => ruleBase.GetType() != injectedRule.GetType())) {
                    rules.Add(injectedRule);
                }
            }
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
            get { return ImmutableArray.CreateRange(rules.Select(d => d.Descriptor)); }
        }

        public override void Initialize(AnalysisContext context) {
            foreach (var descriptor in rules) {
                descriptor.Initialize(context);
            }
        }
    }
}
