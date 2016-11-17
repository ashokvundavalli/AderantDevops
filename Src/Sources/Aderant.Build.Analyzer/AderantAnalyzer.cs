using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer {

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AderantAnalyzer : DiagnosticAnalyzer {

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer"/> class.
        /// </summary>
        public AderantAnalyzer() {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer"/> class.
        /// </summary>
        /// <param name="injectedRules">The rules to inject (for unit testing of possibly disabled rules).</param>
        internal AderantAnalyzer(params RuleBase[] injectedRules) {
            foreach (var injectedRule in injectedRules) {
                if (rules.All(r => r.GetType() != injectedRule.GetType())) {
                    rules.Add(injectedRule);
                }
            }
        }

        private readonly List<RuleBase> rules = new List<RuleBase> {
            new InvalidRegexRule(),
            new InvalidLogMessageRule(),
            new InvalidQueryServiceProxyExtensionRule(),
            new PropertyChangedNoStringRule(),
            new PropertyChangedNoStringNonFixableRule(),
            new SetPropertyValueNoStringRule(),
            new SetPropertyValueNoStringNonFixableRule(),
            new SqlInjectionErrorRule(),
            new SqlInjectionWarningRule()
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
            get {
                var supportedDiagnostics = rules.Select(d => d.Descriptor);
                return ImmutableArray.CreateRange(supportedDiagnostics);
            }
        }

        public override void Initialize(AnalysisContext context) {
            foreach (var descriptor in rules) {
                descriptor.Initialize(context);
            }
        }
    }
}
