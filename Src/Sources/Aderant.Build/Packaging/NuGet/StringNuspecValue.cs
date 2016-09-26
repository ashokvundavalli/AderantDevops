using System.Linq;

namespace Aderant.Build.Packaging.NuGet {
    internal class StringNuspecValue : NuspecValue<string> {
        /// <summary>
        /// Gets a value indicating whether this instance represents a single replacement token (variable).
        /// </summary>        
        public override bool IsVariable {
            get {
                if (!string.IsNullOrEmpty(Value)) {
                    return Value[0] == '$' && Value[Value.Length - 1] == '$';
                }

                return string.IsNullOrWhiteSpace(Value);
            }
        }

        public override bool HasReplacementTokens {
            get {
                if (!string.IsNullOrEmpty(Value)) {
                    return NuspecTokenProcessor.GetTokens(Value).Any();
                }
                return false;
            }
        }

        public void ReplaceToken(string token, string tokenValue) {
            Value = NuspecTokenProcessor.ReplaceToken(Value, token, tokenValue);
        }
    }
}