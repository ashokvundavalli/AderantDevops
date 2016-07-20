using System;

namespace Aderant.Build.Packaging {
    internal class VersionParser {
        private ParseState state;

        /// <summary>
        /// Parses the version into a well defined format (Major.Minor.Build.Revision).
        /// </summary>
        /// <param name="fileVersion">The file version.</param>
        /// <returns></returns>
        public string ParseVersion(string fileVersion) {
            string version = null;

            state = ParseState.Major;

            foreach (char ch in fileVersion) {
                if (char.IsDigit(ch)) {
                    switch (state) {
                        case ParseState.Major:
                            version += ch;
                            break;
                        case ParseState.Minor:
                            version += ch;
                            break;
                        case ParseState.Build:
                            version += ch;
                            break;
                        case ParseState.Revision:
                            version += ch;
                            break;
                    }
                } else if (Char.IsWhiteSpace(ch)) {
                    continue;
                } else {
                    if (state == ParseState.Revision) {
                        break;
                    }

                    version += ".";
                    state++;
                }
            }

            return version;
        }

        internal enum ParseState {
            Major = 1,
            Minor = 2,
            Build = 3,
            Revision = 4,
        }
    }
}