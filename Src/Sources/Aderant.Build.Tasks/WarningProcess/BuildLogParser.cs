using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class BuildLogParser {
        public IEnumerable<WarningEntry> GetWarningEntries(TextReader reader) {
            int i = reader.Peek();
            char ch = (char)i;

            // In TFS 2015 logs where JSON encoded
            if (ch == '{') {
                return ParseJsonLog(reader);
                //yield return warningEntry;
            }

            // And in TFS 2017 they aren't
            return ParseAsPlainText(reader);
        }

        private IEnumerable<WarningEntry> ParseAsPlainText(TextReader reader) {
            string line;
            while ((line = reader.ReadLine()) != null) {
                var entry = ConvertLine(line);
                if (entry != null) {
                    yield return entry;
                }
            }
        }

        private static IEnumerable<WarningEntry> ParseJsonLog(TextReader reader) {
            JsonTextReader jsonReader = new JsonTextReader(reader);

            JToken jObject = JToken.ReadFrom(jsonReader);
            JArray jArray = jObject["value"].Value<JArray>();

            foreach (var line in jArray.Values<string>()) {
                var entry = ConvertLine(line);
                if (entry != null) {
                    yield return entry;
                }
            }
        }

        private static WarningEntry ConvertLine(string line) {
            int warningIndex = line.IndexOf("##[warning]", StringComparison.OrdinalIgnoreCase);

            if (warningIndex > 0) {
                string datetime = line.Substring(0, warningIndex);
                string message = line.Substring(warningIndex);

                WarningEntry entry = new WarningEntry(message.Trim());

                DateTime timestamp;
                if (DateTime.TryParse(datetime.Trim(), out timestamp)) {
                    entry.Timestamp = timestamp;
                }

                return entry;
            }

            return null;
        }
    }
}