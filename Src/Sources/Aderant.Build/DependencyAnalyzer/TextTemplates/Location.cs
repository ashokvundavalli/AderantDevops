using System;

namespace Aderant.Build.DependencyAnalyzer.TextTemplates {
    public struct Location : IEquatable<Location> {
        public Location(string fileName, int line, int column)
            : this() {
            FileName = fileName;
            Column = column;
            Line = line;
        }

        public int Line { get; private set; }
        public int Column { get; private set; }
        public string FileName { get; private set; }

        public Location AddLine() {
            return new Location(FileName, Line + 1, 1);
        }

        public Location AddCol() {
            return AddCols(1);
        }

        public Location AddCols(int number) {
            return new Location(FileName, Line, Column + number);
        }

        public override string ToString() {
            return string.Format("[{0} ({1},{2})]", FileName, Line, Column);
        }

        public bool Equals(Location other) {
            return other.Line == Line && other.Column == Column && other.FileName == FileName;
        }
    }
}
