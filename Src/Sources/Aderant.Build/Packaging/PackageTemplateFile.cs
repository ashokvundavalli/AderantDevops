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
        private Section dependencies;

        public IReadOnlyCollection<string> Dependencies {
            get { return dependencies.Values; }
        }

        public PackageTemplateFile(string contents) {
            parser = new IndendedFileParser();
            parser.Parse(contents);

            dependencies = parser["dependencies"];

            if (dependencies == null) {
                dependencies = new Section("dependencies");
                parser.AddSection(dependencies);
            }
        }

        public void AddDependency(Domain.PackageName item) {
            // LOCKEDVERSION is a magic Paket token which is replaced with the resolved package version from the lock file
            string entry = string.Format("{0} <= LOCKEDVERSION", item.Item1);

            var list = InitializeDependenciesList();

            List<int> packageNameIndexes = new List<int>();

            int index;
            while ((index = FindEntryIndex(item, list)) != -1) {
                packageNameIndexes.Add(index);
                list.RemoveAt(index);
            }
            
            if (packageNameIndexes.Any()) {
                list.Insert(packageNameIndexes.First(), entry);
            } else {
                list.Add(entry);
            }

            dependencies.SetEntries(list);
        }

        private List<string> InitializeDependenciesList() {
            List<string> list;
            if (dependencies.Values != null) {
                list = dependencies.Values.ToList();
            } else {
                list = new List<string>();
            }
            return list;
        }

        private static int FindEntryIndex(Domain.PackageName item, List<string> list) {
            return list.FindIndex(element => element.IndexOf(item.Item1, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void Save(Stream stream) {
            if (!stream.CanWrite) {
                throw new InvalidOperationException("A writable stream must be provided");
            }

            var sections = parser.Sections;
            using (var streamWriter = new StreamWriter(stream, Encoding.UTF8, 4096, stream is MemoryStream)) {
                using (var writer = new SectionWriter(streamWriter)) {
                    writer.Write(sections);

                    // Truncate the remainder of the file... 
                    writer.Flush();
                    stream.SetLength(stream.Position);
                }
            }
        }

        public void RemoveSelfReferences() {
            var packageName = parser["id"].GetValueWithoutKey();

            var list = InitializeDependenciesList();

            list.RemoveAll(entry => entry.IndexOf(packageName, StringComparison.OrdinalIgnoreCase) >= 0);

            dependencies.SetEntries(list);
        }
    }
}