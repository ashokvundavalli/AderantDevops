using System.Text.RegularExpressions;

namespace Aderant.Build.Packaging.NuGet {
    internal static class NuspecTokenProcessor {

        public static System.Collections.Generic.IEnumerable<string> GetTokens(string text) {
            MatchCollection matches = Regex.Matches(text, @"\$.*?\$");

            foreach (Match match in matches) {
                yield return match.Value;
            }
        }

        public static string ReplaceToken(string value, string token, string tokenValue) {
            // Need to make the token a regex
            var regexPatternFromToken = token.Replace("$", "\\$");

            return Regex.Replace(value, regexPatternFromToken, tokenValue);
        }
    }
}