using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.Packaging.Parsing;

namespace Aderant.Build.Packaging {
    internal class SectionWriter : IndentedTextWriter {
        public SectionWriter(TextWriter writer)
            : base(writer) {
        }

        public void Write(IEnumerable<Section> sections) {
            var queue = new Queue<Section>(sections);

            while (queue.Count > 0) {
                var section = queue.Dequeue();              

                Indent = 0;

                if (section is ListSection) {
                    // If the list section has no values then we do not need the section marker ethier 
                    if (section.Values == null || section.Values.Count == 0) {
                        continue;
                    }
                }

                // Bug in the base class here, the base class has an private field tabsPending which controls indentation. 
                // Unfortunately it doesn't get set to true until at least one call to WriteLine is made first which is unfortunate as we
                // actually want to write the section value with no indentation which would mean calling WriteLineNoTabs().
                WriteLine(section.Value.Trim());

                Action<string> writeLine = WriteLineNoTabs;

                if (section.Values != null) {
                    var sectionValues = new Queue<string>(section.Values);

                    while (sectionValues.Count > 0) {
                        var line = sectionValues.Dequeue();

                        Indent = 0;

                        // If the line is already indented, preserve the original indentation 
                        if (line.StartsWith(" ")) {
                            bool allCharactersWhitespace = true;

                            foreach (var ch in line) {
                                if (!char.IsWhiteSpace(ch)) {
                                    allCharactersWhitespace = false;
                                    break;
                                }
                            }

                            // Have we reached the last section and the last line?
                            if (queue.Count == 0 && sectionValues.Count == 0) {
                                writeLine = Write;
                            }

                            if (!allCharactersWhitespace) {
                                writeLine(line.TrimEnd());
                            } else {
                                writeLine(line);
                            }
                        } else {
                            Indent = 1;
                            WriteLine(line.TrimEnd());
                        }
                    }
                }
            }
        }
    }
}