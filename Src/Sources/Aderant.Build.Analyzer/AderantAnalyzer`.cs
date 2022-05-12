using Aderant.Build.Analyzer.Rules;

namespace Aderant.Build.Analyzer {
    public class AderantAnalyzer<T> : AderantAnalyzer where T : RuleBase, new() {

        public AderantAnalyzer() : base(new T()) {
        }
    }
}