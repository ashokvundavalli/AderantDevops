using System;
using System.Collections.Generic;
using System.IO;
using Aderant.Build.Packaging;

namespace Aderant.Build.Versioning {
    /// <summary>
    /// Provides a set of discovery services for extracting version number(s) from a source.
    /// </summary>
    public class FileVersionAnalyzer {
        public Func<string, Stream> OpenFile { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileVersionAnalyzer"/> class.
        /// </summary>
        public FileVersionAnalyzer(string assemblyLocation) {
            this.Analyzers = new List<IVersionAnalyzer>();
            this.Analyzers.Add(new DotNetAnalyzer(assemblyLocation));
            this.Analyzers.Add(new JavaScriptAnalyzer());
        }

        /// <summary>
        /// Gets the analyzers.
        /// </summary>
        /// <value>
        /// The analyzers.
        /// </value>
        public ICollection<IVersionAnalyzer> Analyzers { get; private set; }

        public virtual FileVersionDescriptor GetVersion(string file) {
            if (OpenFile == null) {
                throw new InvalidOperationException("OpenFile is not set.");
            }

            FileInfo fileInfo = new FileInfo(file);

            foreach (IVersionAnalyzer analyzer in Analyzers) {
                if (analyzer.CanAnalyze(fileInfo)) {
                    var filePathAnalyzer = analyzer as IVersionAnalyzer<string>;
                    if (filePathAnalyzer != null) {
                        using (var stream = OpenFile(file)) {
                            using (var reader = new StreamReader(stream)) {
                                return filePathAnalyzer.GetVersion(reader.ReadToEnd());
                            }
                        }
                    }

                    var versionAnalyzer = analyzer as IVersionAnalyzer<FileInfo>;
                    if (versionAnalyzer != null) {
                        return versionAnalyzer.GetVersion(fileInfo);
                        
                    }
                }
            }

            return null;
        }
    }
}