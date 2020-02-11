using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Aderant.Build.Analyzer.Rules.IDisposable;
using Aderant.Build.Analyzer.Rules.Logging;
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
        public AderantAnalyzer() {
            rules = new List<RuleBase>(
                new RuleBase[] {
                    // Regex
                    new InvalidRegexRule(),

                    // Logging
                    new LoggingArgumentCountRule(),
                    new LoggingBanExceptionWithoutMessageRule(),
                    new LoggingInterpolationRule(),
                    new LoggingInvalidTemplateRule(),

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
                    new CodeQualitySessionTransactionRule(),
                    new CodeQualitySqlQueryRule(),
                    new CodeQualityNewExceptionRule(),
                    new CodeQualityMathRoundRule(),
                    new CodeQualityDataProviderIllegalPublicPropertiesRule(),

                    // IDisposable
                    new IDisposableClassRule(),
                    new IDisposableFieldPropertyRule(),
                    new IDisposableLocalVariableRule(),
                    new IDisposableMethodInvocationRule(),
                    new IDisposableObjectCreationRule(),
                    new IDisposableFactoryRegistrationRule(),
                    new IDisposableConstructorRule()
                });

            if (!string.IsNullOrWhiteSpace(GetBuildId())) {
                var serverRules = new List<RuleBase>(new RuleBase[] {
                    // System Diagnostics
                    new CodeQualitySystemDiagnosticsRule(),

                    // Approvals Diff Reporter
                    new CodeQualityApprovalsReporterRule()
                });

                foreach (var serverRule in serverRules) {
                    if (rules.All(ruleBase => ruleBase.GetType() != serverRule.GetType())) {
                        rules.Add(serverRule);
                    }
                }
            }

            SupportedDiagnostics = ImmutableArray.CreateRange(rules.Select(rule => rule.Descriptor));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantAnalyzer"/> class.
        /// </summary>
        internal AderantAnalyzer(params RuleBase[] injectedRules) {
            rules = new List<RuleBase>(injectedRules);

            SupportedDiagnostics = ImmutableArray.CreateRange(rules.Select(rule => rule.Descriptor));
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Returns a set of descriptors for the diagnostics that this analyzer is capable of producing.
        /// </summary>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }

        /// <summary>
        /// Called once at session start to register actions in the analysis context.
        /// </summary>
        /// <param name="context"></param>
        public override void Initialize(AnalysisContext context) {
            foreach (var descriptor in rules) {
                descriptor.Initialize(context);
            }
        }

        /// <summary>
        /// Gets the build identifier.
        /// Note: This value will only be present during builds on the build server.
        /// </summary>
        private static string GetBuildId() {
            string buildId = Environment.GetEnvironmentVariable("BUILD_BUILDID");

            return string.IsNullOrWhiteSpace(buildId)
                ? Environment.GetEnvironmentVariable("TF_BUILD")
                : buildId;
        }

        #endregion Methods
    }
}
