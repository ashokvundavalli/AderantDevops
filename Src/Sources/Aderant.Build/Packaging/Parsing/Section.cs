using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Packaging.Parsing {
    internal class Section {
        private readonly string line;
        private List<string> entries;

        public Section(string line) {
            this.line = line;
        }

        public string Key {
            get { return line.Split(' ')[0]; }
        }

        public string Value {
            get { return line; }
        }

        public IReadOnlyCollection<string> Values {
            get {
                if (entries != null) {
                    return entries.AsReadOnly();
                }
                return null;
            }
        }

        public void AddEntry(string entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (entries == null) {
                entries = new List<string>();
            }
            entries.Add(entry);
        }

        public void SetEntries(IEnumerable<string> list) {
            if (list == null) {
                throw new ArgumentNullException(nameof(list));
            }

            entries = list.ToList();
        }
    }
}