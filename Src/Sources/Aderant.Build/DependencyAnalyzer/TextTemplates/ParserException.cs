using System;

namespace Aderant.Build.DependencyAnalyzer.TextTemplates {
    public class ParserException : Exception {
        internal ParserException(string message, Location location)
            : base(message) {
            Location = location;
        }

        public Location Location { get; private set; }
    }
}
