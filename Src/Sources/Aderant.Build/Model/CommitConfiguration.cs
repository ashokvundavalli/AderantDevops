using System;

namespace Aderant.Build.Model {
    internal class CommitConfiguration {
        private string sourceCommit;

        public string SourceCommit {
            get => sourceCommit;
            set {
                if (value == null || value.Length != 40) {
                    throw new ArgumentException("Invalid commit specified.", nameof(sourceCommit));
                }

                sourceCommit = value;
            }
        }

        public string[] ExcludedCommits { get; set; }

        internal CommitConfiguration(string sourceCommit) {
            this.SourceCommit = sourceCommit;
        }
    }
}
