using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using Paket;

namespace Aderant.Build.Packaging {
    internal class PackageTemplateFile {
        private class DependencyTextLine {
            public bool Header { get; set; }

            private int i;
            private string line;

            public DependencyTextLine(int i, string line, bool header = false) {
                Header = header;
                this.i = i;
                this.line = line;
            }

            public string Text {
                get { return line.TrimStart(); }
            }

            public int LineNumber {
                get { return i; }
            }
        }

        // This data structure is a bit crude. Would be nice if Paket used a sightly less shit format. Plain text is annoying to parse with C#.
        private string[] textRepresentation;
        private List<DependencyTextLine> dependencyText = new List<DependencyTextLine> ();
        private List<string> dependencies = new List<string>();

        public string[] Lines {
            get { return this.textRepresentation; }
        }

        public List<string> Dependencies {
            get { return new List<string>(dependencies); }
        }

        public PackageTemplateFile(string contents) {
            contents = contents.Replace("\n", Environment.NewLine);

            textRepresentation = Regex.Split(contents, @"(?<=\r\n)(?!$)");

            FindSection("dependencies");
            ExtractDependenciesFromSection();
        }

        private void ExtractDependenciesFromSection() {
            if (dependencyText.Any()) {
                dependencies.AddRange(dependencyText.Where(text => !text.Header).Select(s => s.Text));

                var lines = textRepresentation.ToList();

                int start = dependencyText[0].LineNumber;

                lines.RemoveRange(start, dependencyText.Last().LineNumber - start + 1);

                textRepresentation = lines.ToArray();
            }
        }

        private void FindSection(string section) {
            bool readSection = false;

            for (int i = 0; i < textRepresentation.Length; i++) {
                string line = textRepresentation[i];

                if (readSection) {
                    if (!string.IsNullOrEmpty(line)) {
                        if (char.IsWhiteSpace(line, 0)) {
                            dependencyText.Add(new DependencyTextLine(i, line));
                        } else {
                            break;
                        }
                    }
                }

                if (!readSection && line.StartsWith(section)) {
                    dependencyText.Add(new DependencyTextLine(i, line, true));
                    readSection = true;
                }
            }
        }

        public void AddDependency(Domain.PackageName item, Paket.VersionRequirement value) {
            if (!dependencies.Any(d => d.StartsWith(item.Item1))) {
                dependencies.Add(string.Format("{0} {1}", item.Item1, value.ToString()));
            }
        }

        public void Save(Stream stream) {
            if (!stream.CanWrite) {
                throw new InvalidOperationException("A writable stream must be provided");
            }

            List<string> lines = textRepresentation.ToList();

            List<string> list = dependencies.ToList();
            list.Insert(0, "dependencies" + Environment.NewLine);

            if (dependencyText.Any()) {
                lines.InsertRange(dependencyText[0].LineNumber, CreateTextRepresentation(list));
            } else {
                lines.Add(Environment.NewLine);
                lines.AddRange(CreateTextRepresentation(list));
            }

            using (var writer = new StreamWriter(stream, Encoding.UTF8, 4096, stream is MemoryStream)) {
                foreach (var line in lines) {
                    writer.Write(line);
                }
            }
        }

        private List<string> CreateTextRepresentation(List<string> list) {
            return list.Select((s, i) => i == 0 ? s : s.PadLeft(s.Length + 4) + Environment.NewLine).ToList();
        }
    }
}