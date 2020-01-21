using System.Collections.Generic;

namespace Aderant.Build.AzurePipelines {
    /// <summary>
    /// Provides optional data associated with a Azure Pipelines logging message.
    /// </summary>
    internal sealed class VsoBuildMessageData {
        /// <summary>
        /// Gets or sets the source file path the message should originate from.
        /// </summary>
        /// <value>
        /// The path of the originating file.
        /// </value>
        public string SourcePath { get; set; }

        /// <summary>
        /// Gets or sets the line number the message relates to.
        /// </summary>
        /// <value>
        /// The line number.
        /// </value>
        public int? LineNumber { get; set; }

        /// <summary>
        /// Gets or sets the column number the message relates to.
        /// </summary>
        /// <value>
        /// The column number.
        /// </value>
        public int? ColumnNumber { get; set; }

        /// <summary>
        /// Gets or sets the error code of the warning or error message.
        /// </summary>
        /// <value>
        /// The error code of the warning or error.
        /// </value>
        public int? ErrorCode { get; set; }

        internal Dictionary<string, string> GetProperties() {
            var properties = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(SourcePath)) {
                properties.Add("sourcepath", SourcePath);
            }
            if (LineNumber.HasValue) {
                properties.Add("linenumber", LineNumber.ToString());
            }
            if (ColumnNumber.HasValue) {
                properties.Add("columnnumber", ColumnNumber.ToString());
            }
            if (ErrorCode.HasValue) {
                properties.Add("code", ErrorCode.ToString());
            }
            return properties;
        }
    }
}