using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aderant.Build.Packaging.Parsing;

namespace Aderant.Build.Packaging {
    internal class SectionWriter : IndentedTextWriter {
        public SectionWriter(TextWriter writer)
            : base(writer) {
        }

        public void Write(IEnumerable<Section> sections) {
            foreach (var section in sections) {
                Indent = 0;

                // Bug in the base class here, the base class has an private field tabsPending which controls indentation. 
                // Unfortunately it doesn't get set to true until at least one call to WriteLine is made first which is unfortunate as we
                // actually want to write the section value with no indentation which would mean calling WriteLineNoTabs().
                WriteLine(section.Value.Trim());

                if (section.Values != null) {
                    foreach (var entry in section.Values) {
                        Indent = 0;

                        // If the line is already indented, preserve the original indentation 
                        if (entry.StartsWith(" ")) {
                            if (!entry.All(ch => Char.IsWhiteSpace(ch))) {
                                WriteLineNoTabs(entry.TrimEnd());
                            } else {
                                WriteLineNoTabs(entry);
                            }
                        } else {
                            Indent = 1;
                            WriteLine(entry.TrimEnd());
                        }
                    }
                }
            }
        }
    }
}