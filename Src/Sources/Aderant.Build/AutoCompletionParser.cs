using System;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build {

    public sealed class AutoCompletionParser {
        private readonly string line;
        private readonly string lastWord;

        public AutoCompletionParser(string line, string lastWord, object[] aliases) {
            Debug.WriteLine(string.Format("Aderant:Auto-Complete Line: {0}, lastWord: {1}", (line ?? string.Empty).Replace(" ", "<space>"), (lastWord ?? string.Empty).Replace(" ", "<space>")));

            Debug.WriteLine(string.Format("Aderant:Auto-Complete, Alias Count: {0}", aliases.Length));
            if(aliases.Length > 0) {
                aliases.OfType<PSObject>().ToList().ForEach(alias => {
                    Debug.WriteLine(string.Format("Aderant:Auto-Complete, Alias Name: {0}, Value {1}", alias.Properties["Name"].Value, alias.Properties["Definition"].Value));
                });
            }
            
            this.line = line ?? string.Empty;
            if (this.line.Contains(";")) {
                this.line = this.line.Split(';').Last();
            }
            this.line = this.line.TrimStart();
            this.lastWord = lastWord ?? string.Empty;
            

            RegexOptions options = RegexOptions.None;
            Regex regex = new Regex(@"[ ]{2,}", options);
            this.line = regex.Replace(this.line, @" ");

            string lineCommand = this.line.Split(' ').First();
            string matchingAliasedCommand =
                aliases.OfType<PSObject>().Where(
                    alias =>
                    alias.Properties["Name"].Value != null &&
                    alias.Properties["Definition"].Value != null &&
                    alias.Properties["Name"].Value.ToString().Equals(lineCommand, StringComparison.InvariantCultureIgnoreCase)).Select(alias => alias.Properties["Definition"].Value.ToString()).FirstOrDefault();

            if(matchingAliasedCommand != null) {
                this.line = string.Format("{0}{1}", matchingAliasedCommand, this.line.Substring(lineCommand.Length));
                Debug.WriteLine(string.Format("Aderant:Auto-Complete, Matched the alias Name: {0}, Value {1}. New Line: {2}", lineCommand, matchingAliasedCommand, this.line.Replace(" ", "<space>")));
            }
            if(this.lastWord != null && this.lastWord.Contains(",")) {
                this.lastWord = this.lastWord.Split(',').Select(s => s.Trim()).Last();
            }
        }
        

        public bool IsAutoCompletionForParameter(string commandName, string parameterName, bool isDefaultParameter) {
            if(string.IsNullOrEmpty(line)) {
                return false;
            }

            if(!line.StartsWith(commandName + " ", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            if (lastWord.StartsWith("-", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            string lastParameterName = line.Split(' ').LastOrDefault(s => s.StartsWith("-", StringComparison.OrdinalIgnoreCase));
            
            if (isDefaultParameter && 
                lastParameterName == null) {
                return true;
            }

            

            if (line.StartsWith(commandName, StringComparison.InvariantCultureIgnoreCase)
                && lastParameterName!= null && (parameterName.Equals(lastParameterName.TrimStart('-'), StringComparison.InvariantCultureIgnoreCase))) {
                return true;
            }


            return false;
        }

        public string[] GetModuleMatches(string modulePath) {
            DependencyBuilder analyzer = new DependencyBuilder(modulePath);

            return analyzer
                .GetAllModules()
                .Where(m => string.IsNullOrEmpty(lastWord) || m.Name.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)
                            ||
                            new string(m.Name.Split('.').Select(s => s.First()).ToArray()).StartsWith(lastWord,
                                                                                                      StringComparison.
                                                                                                          OrdinalIgnoreCase))
                .Select(m => m.Name).ToArray();
        }
    }
}