using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {
    /// <summary>
    /// Replace text in file(s) using a Regular Expression.
    /// </summary>
    /// <example>Search for a version number and update the revision.
    /// <code><![CDATA[
    /// <FileUpdate Files="version.txt"
    ///     Regex="(\d+)\.(\d+)\.(\d+)\.(\d+)"
    ///     ReplacementText="$1.$2.$3.123" />
    /// ]]></code>
    /// </example>
    public class FileUpdate2 : TrackedSourcesBuildTask {
        private string _regex;
        private bool _ignoreCase;
        private bool _multiline;
        private bool _singleline;
        private int _replacementCount = -1;
        private string _replacementText;
        private Encoding _encoding = System.Text.Encoding.UTF8;

        protected override string WriteTLogFilename {
            get { return "FileUpdate2.write.TLog"; }
        }

        protected override string[] ReadTLogFilenames {
            get {
                return new[] {
                    "FileUpdate2.read.TLog"
                };
            }
        }

        /// <summary>
        /// Gets or sets the regex.
        /// </summary>
        /// <value>The regex.</value>
        public string Regex {
            get { return this._regex; }
            set { this._regex = value; }
        }

        /// <summary>
        /// Gets or sets a value specifies case-insensitive matching. .
        /// </summary>
        /// <value><c>true</c> if [ignore case]; otherwise, <c>false</c>.</value>
        public bool IgnoreCase {
            get { return this._ignoreCase; }
            set { this._ignoreCase = value; }
        }

        /// <summary>
        /// Gets or sets a value changing the meaning of ^ and $ so they match at the beginning and end, 
        /// respectively, of any line, and not just the beginning and end of the entire string.
        /// </summary>
        /// <value><c>true</c> if multiline; otherwise, <c>false</c>.</value>
        public bool Multiline {
            get { return this._multiline; }
            set { this._multiline = value; }
        }

        /// <summary>
        /// Gets or sets a value changing the meaning of the dot (.) so it matches 
        /// every character (instead of every character except \n). 
        /// </summary>
        /// <value><c>true</c> if singleline; otherwise, <c>false</c>.</value>
        public bool Singleline {
            get { return this._singleline; }
            set { this._singleline = value; }
        }

        /// <summary>
        /// Gets or sets the maximum number of times the replacement can occur.
        /// </summary>
        /// <value>The replacement count.</value>
        public int ReplacementCount {
            get { return this._replacementCount; }
            set { this._replacementCount = value; }
        }

        /// <summary>
        /// Gets or sets the replacement text.
        /// </summary>
        /// <value>The replacement text.</value>
        public string ReplacementText {
            get { return this._replacementText; }
            set { this._replacementText = value; }
        }

        /// <summary>
        /// The character encoding used to read and write the file.
        /// </summary>
        /// <remarks>Any value returned by <see cref="P:System.Text.Encoding.WebName" /> is valid input.
        /// <para>The default is <c>utf-8</c></para></remarks>
        public string Encoding {
            get { return this._encoding.WebName; }
            set { this._encoding = System.Text.Encoding.GetEncoding(value); }
        }

        protected override bool ExecuteInternal() {
            RegexOptions regexOptions = RegexOptions.None;
            if (_ignoreCase) {
                regexOptions |= RegexOptions.IgnoreCase;
            }
            if (_multiline) {
                regexOptions |= RegexOptions.Multiline;
            }
            if (_singleline) {
                regexOptions |= RegexOptions.Singleline;
            }
            if (_replacementCount == 0) {
                _replacementCount = -1;
            }
            Regex regex = new Regex(this._regex, regexOptions);

            try {
                ITaskItem[] files = this.Sources;

                for (int i = 0; i < files.Length; i++) {
                    ITaskItem taskItem = files[i];
                    string itemSpec = taskItem.ItemSpec;

                    Log.LogMessage("Updating File \"{0}\".", itemSpec);

                    string text = File.ReadAllText(itemSpec, _encoding);
                    text = regex.Replace(text, _replacementText, _replacementCount);

                    if (File.Exists(OutputFile)) {
                        string outputContents = File.ReadAllText(OutputFile);

                        // Test if we actually need to write the file
                        if (string.Equals(text, outputContents)) {
                            continue;
                        }
                    }

                    File.WriteAllText(OutputFile, text, _encoding);
                    
                    Log.LogMessage("  Replaced matches with \"{0}\".", _replacementText);
                }
            } catch (Exception exception) {
                Log.LogErrorFromException(exception);
                return false;
            }
            return true;
        }
    }
}