using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class AderantAnalyzer : DiagnosticAnalyzer {
        #region Fields

        private readonly List<RuleBase> rules;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer"/> class.
        /// </summary>
        public AderantAnalyzer()
            : this(Environment.GetEnvironmentVariable("BUILD_BUILDID")) {
            // Empty.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer"/> class.
        /// </summary>
        internal AderantAnalyzer(params RuleBase[] injectedRules)
            : this(Environment.GetEnvironmentVariable("BUILD_BUILDID"), injectedRules) {
            // Empty.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer" /> class.
        /// </summary>
        /// <param name="buildId">The build identifier.</param>
        /// <param name="injectedRules">The rules to inject (for unit testing of possibly disabled rules).</param>
        internal AderantAnalyzer(string buildId, params RuleBase[] injectedRules) {
            rules = new List<RuleBase>(
                new RuleBase[] {
                    // Regex
                    new InvalidRegexRule(),
                    new InvalidLogMessageRule(),

                    // Properties
                    new PropertyChangedNoStringRule(),
                    new PropertyChangedNoStringNonFixableRule(),
                    new SetPropertyValueNoStringRule(),
                    new SetPropertyValueNoStringNonFixableRule(),

                    // SQL
                    new SqlInjectionErrorRule(),
                    new QueryServiceQueryAllRule(),

                    // Code Quality
                    new CodeQualityDbConnectionStringRule(),
                    new CodeQualityDefaultTransactionScopeRule(),
                    new CodeQualitySqlQueryRule(),
                    new CodeQualityNewExceptionRule(),

                    // IDisposable
                    new IDisposableClassRule(),
                    new IDisposableFieldPropertyRule(),
                    new IDisposableLocalVariableRule(),
                    new IDisposableMethodInvocationRule(),
                    new IDisposableObjectCreationRule(),
                    new IDisposableFactoryRegistrationRule(),
                    new IDisposableConstructorRule()
                });

            foreach (var injectedRule in injectedRules) {
                if (rules.All(ruleBase => ruleBase.GetType() != injectedRule.GetType())) {
                    rules.Add(injectedRule);
                }
            }

            if (string.IsNullOrWhiteSpace(buildId)) {
                return;
            }

            var diagnosticsRule = new CodeQualitySystemDiagnosticsRule();

            if (rules.All(ruleBase => ruleBase.GetType() != diagnosticsRule.GetType())) {
                rules.Add(diagnosticsRule);
            }
        }

        #endregion Constructors

        #region Methods

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
            get { return ImmutableArray.CreateRange(rules.Select(d => d.Descriptor)); }
        }

        public override void Initialize(AnalysisContext context) {
            foreach (var descriptor in rules) {
                descriptor.Initialize(context);
            }
        }

        #endregion Methods
    }
}
