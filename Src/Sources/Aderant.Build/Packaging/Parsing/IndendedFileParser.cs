using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aderant.Build.Packaging.Parsing {
    internal class IndendedFileParser {
        internal enum ParserState {
            Initial,
            ReadingList
        }

        private List<Section> sections;
        private Section currentSection;

        /// <summary>
        /// Gets the state of the parser.
        /// </summary>
        public ParserState State { get; private set; }

        /// <summary>
        /// Parses the specified text and populates the <see cref="Sections"/> property.
        /// </summary>
        /// <param name="textRepresentation">The text representation.</param>
        public void Parse(string textRepresentation) {
            var reader = new StringReader(textRepresentation);

            State = ParserState.Initial;

            sections = new List<Section>();

            string line;

            while ((line = reader.ReadLine()) != null) {
                if (line.StartsWith(" ")) {
                    State = ParserState.ReadingList;

                    Section section = currentSection;
                    section.AddEntry(line);
                } else {
                    State = ParserState.Initial;

                    sections.Add(currentSection = new Section(line));
                }
            }

            currentSection = null;
        }

        public Section this[string key] {
            get {
                var foundSection = sections.FirstOrDefault(section => section.Key == key);
                if (foundSection != null) {
                    return foundSection;
                }
                return null;
            }
        }

        public IEnumerable<Section> Sections {
            get { return sections; }
        }

        public void AddSection(Section section) {
            if (section == null) {
                throw new ArgumentNullException(nameof(section));
            }
            sections.Add(section);
        }
    }
}