using System;
using System.Linq;
using Microsoft.Build.Construction;

namespace Aderant.Build.ProjectSystem.SolutionParser {
    internal class SolutionFileParser : ISolutionFileParser {
        public ParseResult Parse(string solutionFile) {
            SolutionFile file = SolutionFile.Parse(solutionFile);

            var result = new ParseResult {
                SolutionFile = solutionFile,
                ProjectsByGuid = file.ProjectsByGuid.ToDictionary(k => Guid.Parse(k.Key), v => v.Value),
                ProjectsInOrder = file.ProjectsInOrder,
                SolutionConfigurations = file.SolutionConfigurations
            };

            RaiseSolutionFileParsed(result);

            return result;
        }

        /// <summary>
        /// Occurs when a solution file has been parsed.
        /// </summary>
        public event EventHandler<ParseResult> SolutionFileParsed;

        protected virtual void RaiseSolutionFileParsed(ParseResult e) {
            SolutionFileParsed?.Invoke(this, e);
        }
    }
}
