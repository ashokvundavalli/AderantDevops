using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Aderant.Build.DependencyAnalyzer {
    public static class NamingExtensions {

        /// <summary>
        /// Makes an identifier name from the provided string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="casing">The casing.</param>
        /// <param name="adornment">Adorned will place an @ symbol before the name.</param>
        /// <returns></returns>
        public static string MakeIdentifier(this string name, IdentifierCasing casing, IdentifierAdornment adornment) {
            StringBuilder sb = new StringBuilder();

            // Split the name into words
            string spacedWords = System.Text.RegularExpressions.Regex.Replace(
                name,
                "([A-Z_\\s])",
                " $1",
                System.Text.RegularExpressions.RegexOptions.Compiled).Trim();

            string[] words = spacedWords.Split(' ');

            for (int i = 0; i < words.Length; i++) {
                // Get the valid character only representation of the word
                StringBuilder sbWord = new StringBuilder();
                for (int j = 0; j < words[i].Length; j++) {
                    
                        sbWord.Append(words[i][j]);
                }

                string word = sbWord.ToString();

                if (word.Trim().Length == 0) {
                    continue;
                }
                else {
                    if (i == 0 && casing == IdentifierCasing.CamelCase) {
                        // Convert the first character to lower case
                        sb.Append(char.ToLower(word[0], CultureInfo.InvariantCulture));
                        sb.Append(word.Substring(1).Trim());
                    }
                    else {
                        // Convert the first character to upper case
                        sb.Append(char.ToUpper(word[0], CultureInfo.InvariantCulture));
                        sb.Append(word.Substring(1).Trim());
                    }
                }
            }

            if (adornment == IdentifierAdornment.Adorned) {
                return "@" + sb.ToString();
            }
            else {
                return sb.ToString();
            }
        }

        /// <summary>
        /// Makes an identifier name from the provided string with Pascal Casing
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>A Pascal Case version of the name</returns>
        public static string MakeIdentifier(this string name) {
            return MakeIdentifier(name, IdentifierCasing.PascalCase, IdentifierAdornment.Plain);
        }

        /// <summary>
        /// Makes an identifier name from the provided string.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="casing">The casing.</param>
        /// <returns>The name with the appropriate casing</returns>
        public static string MakeIdentifier(this string name, IdentifierCasing casing) {
            return MakeIdentifier(name, casing, IdentifierAdornment.Plain);
        }

        public static string ToCamelCase(this string identifier) {
            return MakeIdentifier(identifier, IdentifierCasing.CamelCase, IdentifierAdornment.Plain);
        }

        public static string ToPascalCase(this string identifier) {
            return MakeIdentifier(identifier, IdentifierCasing.PascalCase, IdentifierAdornment.Plain);
        }



        /// <summary>
        /// Concatenates the strings in the sequence
        /// </summary>
        /// <param name="sequence">The sequence.</param>
        /// <returns>The Concatenated string</returns>
        public static string StringConcat(this IEnumerable<string> sequence) {
            return sequence.StringConcat("");
        }

        /// <summary>
        /// Concatenates the strings in the sequence using the supplied separator between each string
        /// </summary>
        /// <param name="sequence">The sequence.</param>
        /// <param name="separator">The separator.</param>
        /// <returns>The concatenated string</returns>
        public static string StringConcat(this IEnumerable<string> sequence, string separator) {
            return string.Join(separator, sequence.ToArray());
        }


        /// <summary>
        /// Splits an identifier into name parts based on underscores and case differences.
        /// </summary>
        /// <param name="identifier">The identifier name parts.</param>
        /// <returns></returns>
        public static string[] SplitIdentifier(this string identifier) {

            if (identifier == null) {
                throw new ArgumentNullException("identifier");
            }

            // Find all the places where a change in case happens
            List<int> caseChangeIndexes = new List<int>();

            for (int i = 1; i < identifier.Length; i++) {
                if (Char.IsUpper(identifier[i]) && Char.IsLower(identifier[i - 1])) {
                    caseChangeIndexes.Add(i);
                }

                if (i > 1 && Char.IsLower(identifier[i]) && Char.IsUpper(identifier[i - 1]) && !caseChangeIndexes.Contains(i - 1)) {
                    caseChangeIndexes.Add(i - 1);
                }
            }

            for (int i = 0; i < caseChangeIndexes.Count; i++) {
                identifier = identifier.Insert(caseChangeIndexes[i] + i, " ");
            }

            identifier = identifier.Replace('_', ' ');
            string[] parts = identifier.Split(' ');
            return parts;
        }



    
    }

    public enum IdentifierCasing {
        PascalCase,
        CamelCase
    }

    public enum IdentifierAdornment {
        Plain,
        Adorned
    }
    
}