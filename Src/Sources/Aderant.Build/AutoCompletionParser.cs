using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Text.RegularExpressions;
using Aderant.Build.DependencyAnalyzer;
using Microsoft.Win32;
using Debugger = System.Diagnostics.Debugger;

namespace Aderant.Build {

    public sealed class AutoCompletionParser {
        private readonly string line;
        private string lastWord;

        public AutoCompletionParser(string command, string parameter, CommandAst ast) {
        }

        public AutoCompletionParser(string line, string lastWord, object[] aliases) {
            Debug.WriteLine("Aderant:Auto-Complete Line: {0}, lastWord: {1}", (line ?? string.Empty).Replace(" ", "<space>"), (lastWord ?? string.Empty).Replace(" ", "<space>"));

            Debug.WriteLine("Aderant:Auto-Complete, Alias Count: {0}", aliases.Length);
            if(aliases.Length > 0) {
                aliases.OfType<PSObject>().ToList().ForEach(alias => {
                    Debug.WriteLine("Aderant:Auto-Complete, Alias Name: {0}, Value {1}", alias.Properties["Name"].Value, alias.Properties["Definition"].Value);
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
                && lastParameterName!= null && parameterName.Equals(lastParameterName.TrimStart('-'), StringComparison.InvariantCultureIgnoreCase)) {
                return true;
            }


            return false;
        }

        internal string[] GetModuleMatches(IDependencyBuilder analyzer) {
            List<string> searchStringSplitByCase = SplitModuleNameByCase(lastWord);
            List<string> matches = new List<string>();
            Debug.WriteLine(string.Format("Module name split by case = {0}", searchStringSplitByCase.StringConcat(" ")));

            //if the search string is split into more than one "word" then we want to also split the module names
            //otherwise we match the string to the whole word.
            if (searchStringSplitByCase.Count > 1) {
                foreach (ExpertModule m in analyzer.GetAllModules()) {
                    if (CompareModuleNameToSearchString(m.Name, searchStringSplitByCase)) {
                        matches.Add(m.Name);
                    }
                }
            } else {
                // Check local git repositories.
                matches = CheckGitRepos(lastWord);
                // If not found, try Expert modules.
                if (!matches.Any()) {
                    var allModules = analyzer.GetAllModules().ToList();
                    matches = allModules.Where(
                            m => string.IsNullOrEmpty(lastWord) || m.Name.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase)
                                                                ||
                                                                new string(m.Name.Split('.').Select(s => s.First()).ToArray()).StartsWith(
                                                                    lastWord,
                                                                    StringComparison.OrdinalIgnoreCase))
                        .Select(m => m.Name).ToList();
                }
            }
            //if we still have no matches we match again, but assume that the input consists of only the first letters of a series of words
            if (!matches.Any()) {
                List<string> searchStringSplitByAll = SplitModuleNameByCase(lastWord.ToUpper());
                foreach (ExpertModule m in analyzer.GetAllModules()) {
                    if (CompareModuleNameToSearchString(m.Name, searchStringSplitByAll)) {
                        matches.Add(m.Name);
                    }
                }
            }

            return matches.ToArray();
        }


        /// <summary>
        /// Provides the core auto complete functionality.
        /// </summary>
        /// <param name="modulePath">The module path.</param>
        /// <param name="productManifestPath">The product manifest path.</param>
        /// <remarks>Called from the PowerShell host.</remarks>

        public string[] GetModuleMatches(string modulePath, string productManifestPath = null) {
            ExpertManifest manifest = ExpertManifest.Load(productManifestPath);
            manifest.ModulesDirectory = modulePath;

            return GetModuleMatches(new DependencyBuilder(manifest));
        }

        /// <summary>
        /// Provides the core auto complete functionality under PowerShell v5.
        /// </summary>
        /// <param name="wordToComplete">The word to complete.</param>
        /// <param name="modulePath">The module path.</param>
        /// <param name="productManifestPath">The product manifest path.</param>
        /// <remarks>Called from the PowerShell host.</remarks>
        public string[] GetModuleMatches(string wordToComplete, string modulePath, string productManifestPath = null) {
            lastWord = wordToComplete;

            ExpertManifest manifest = ExpertManifest.Load(productManifestPath);
            manifest.ModulesDirectory = modulePath;

            var autoCompletes = GetModuleMatches(new DependencyBuilder(manifest));
            return autoCompletes;
        }

        /// <summary>
        /// Check locally registered git repositories. If found, provide them to the console for selection.
        /// </summary>
        /// <param name="expectedWord">The searching word.</param>
        /// <returns>A list of all matched path.</returns>
        private List<string> CheckGitRepos(string expectedWord) {
            var result = new List<string>();

            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\TeamFoundation\GitSourceControl\Repositories")) {
                    if (key != null) {
                        var gitReposKeys = key.GetSubKeyNames();
                        foreach (var gitRepoKey in gitReposKeys) {
                            var gitRepo = key.OpenSubKey(gitRepoKey);
                            if (gitRepo != null) {
                                var gitRepoName = gitRepo.GetValue("Name") as string;
                                var gitRepoPath = gitRepo.GetValue("Path") as string;
                                if (gitRepoName != null && gitRepoPath != null && gitRepoName.StartsWith(expectedWord,StringComparison.InvariantCultureIgnoreCase) && Directory.Exists(gitRepoPath)) {
                                    // Check if TFSBuild.rsp exists in .\Build or at any of the subdirectories to make sure it is an Expert repo.
                                    if (File.Exists(Path.Combine(gitRepoPath, @"Build\TFSBuild.rsp")) || Directory.GetDirectories(gitRepoPath).Any(x=>File.Exists(Path.Combine(x, @"Build\TFSBuild.rsp")))) {
                                        result.Add(gitRepoPath);
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) 
            {
                result.Add($"(AutoCompletionParser Error: {ex.Message})");
            }

            return result;
        }

        private static bool CompareModuleNameToSearchString(string moduleName, List<string> searchStringSplitByCase) {
            List<string> moduleNameSplitByCase = SplitModuleNameByCase(moduleName);
            if (moduleNameSplitByCase.Count < searchStringSplitByCase.Count) {
                return false; }
            //truncate the full module name so that we only match the number of parts in the search string
            List<string> moduleNameSplitByCaseTruncated = SplitModuleNameByCase(moduleName).GetRange(0, searchStringSplitByCase.Count);
            
            int i = 0;
            foreach(string searchString in searchStringSplitByCase) {
                string modulePart = moduleNameSplitByCaseTruncated[i];
                if (!modulePart.ToLower().StartsWith(searchString.ToLower())) {
                    return false;
                }
                i++;
            }
            return true;
        }

        private static List<string> SplitModuleNameByCase(string moduleName) {
            //first split by period separator
            string[] parts = moduleName.Split('.');
            List<string> words = new List<string>();
            foreach (var part in parts) {
                //now split by assuming that an upper case letter denotes the beginning of a new word
                StringBuilder stringBuilder = new StringBuilder();
                foreach (char c in part) {
                    if (Char.IsUpper(c) && stringBuilder.Length > 0)
                        stringBuilder.Append('.');
                    stringBuilder.Append(c);
                }
                words.AddRange(stringBuilder.ToString().Split('.'));
            }
            return words;
        }
    }
}
