using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.Packaging.Parsing;
using Paket;

namespace Aderant.Build.Packaging {
    internal class PackageTemplateFile {
        private IndendedFileParser parser;
        private Section section;

        public IReadOnlyCollection<string> Dependencies {
            get { return section.Values; }
        }

        public PackageTemplateFile(string contents) {
            parser = new IndendedFileParser();
            parser.Parse(contents);

            section = parser["dependencies"];

            if (section == null) {
                section = new Section("dependencies");
                parser.AddSection(section);
            }
        }

        public void AddDependency(Domain.PackageName item, SemVerInfo version) {
            string entry = item.Item1;

            if (version.Major > 0 || version.Minor > 0 || version.Patch > 0) {
                entry = string.Format("{0} ~> LOCKEDVERSION", item.Item1, version.ToString());
            }

            List<string> list;
            if (section.Values != null) {
                list = section.Values.ToList();
            } else {
                list = new List<string>();
            }

            List<int> packageNameIndexes = new List<int>();

            int index;
            while (index = list.FindIndex(element => element.IndexOf(item.Item1, StringComparison.OrdinalIgnoreCase)) != -1) {
                packageNameIndexes.Add(index);
                list.RemoveAt(index);
            }

            //var index = list.FindIndex(x => x.StartsWith(item.Item1, StringComparison.OrdinalIgnoreCase));
            //if (index != -1) {
            if (packageNameIndexes.Any()) { 
                list.Insert(index, entry);
            } else {
                list.Add(entry);
            }

            section.SetEntries(list);
        }

        public void Save(Stream stream) {
            if (!stream.CanWrite) {
                throw new InvalidOperationException("A writable stream must be provided");
            }

            var sections = parser.Sections;
            using (var writer = new SectionWriter(new StreamWriter(stream, Encoding.UTF8, 4096, stream is MemoryStream))) {
                writer.Write(sections);

                // Truncate the remainder of the file... 
                writer.Flush();
                stream.SetLength(stream.Position);
            }
        }
    }
}