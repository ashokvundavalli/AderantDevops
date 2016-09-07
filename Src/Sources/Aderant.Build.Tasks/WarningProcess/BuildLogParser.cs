using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aderant.Build.Tasks.WarningProcess {
    internal class BuildLogParser {
        public IEnumerable<WarningEntry> GetWarningEntries(TextReader reader) {
            JsonTextReader jsonReader = new JsonTextReader(reader);

            JToken jObject = JToken.ReadFrom(jsonReader);
            JArray jArray = jObject["value"].Value<JArray>();

            foreach (var line in jArray.Values<string>()) {
                int warningIndex = line.IndexOf("##[warning]", StringComparison.OrdinalIgnoreCase);

                if (warningIndex > 0) {
                    string datetime = line.Substring(0, warningIndex);
                    string message = line.Substring(warningIndex);

                    WarningEntry entry = new WarningEntry(message.Trim());

                    DateTime timestamp;
                    if (DateTime.TryParse(datetime.Trim(), out timestamp)) {
                        entry.Timestamp = timestamp;
                    }

                    yield return entry;
                }
            }
        }
    }
}