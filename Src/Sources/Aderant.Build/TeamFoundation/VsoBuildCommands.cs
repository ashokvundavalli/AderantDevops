using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Aderant.Build.Logging;

namespace Aderant.Build.TeamFoundation {
    /// <summary>
    /// Responsible for issuing TF Build agent commands (see
    /// <see href="https://github.com/Microsoft/vsts-tasks/blob/master/docs/authoring/commands.md" />).
    /// </summary>
    internal sealed class VsoBuildCommandBuilder {

        private const string MessagePrefix = "##vso[";

        private const string MessagePostfix = "]";
        private Action<string> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="VsoBuildCommandBuilder" /> class.
        /// </summary>
        public VsoBuildCommandBuilder(ILogger logger)
            : this(message => logger.Info(message)) {
        }

        public VsoBuildCommandBuilder(Action<string> logger) {
            this.logger = logger;
        }

        public VsoBuildCommandBuilder() {
        }

        /// <summary>
        /// Log a warning issue to timeline record of current task.
        /// </summary>
        /// <param name="message">The warning message.</param>
        public string WriteWarning(string message) {
            return WriteLoggingCommand(
                "task.logissue",
                new Dictionary<string, string> {
                    ["type"] = "warning"
                },
                message);
        }

        /// <summary>
        /// Log a warning issue with detailed data to timeline record of current task.
        /// </summary>
        /// <param name="message">The warning message.</param>
        /// <param name="data">The message data.</param>
        public string WriteWarning(string message, VsoBuildMessageData data) {
            var properties = data.GetProperties();
            properties.Add("type", "warning");
            return WriteLoggingCommand("task.logissue", properties, message);
        }

        /// <summary>
        /// Log an error to timeline record of current task.
        /// </summary>
        /// <param name="message">The error message.</param>
        public void WriteError(string message) {
            WriteLoggingCommand(
                "task.logissue",
                new Dictionary<string, string> {
                    ["type"] = "error"
                },
                message);
        }

        /// <summary>
        /// Log an error with detailed data to timeline record of current task.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="data">The message data.</param>
        public string WriteError(string message, VsoBuildMessageData data) {
            var properties = data.GetProperties();
            properties.Add("type", "error");
            return WriteLoggingCommand("task.logissue", properties, message);
        }

        /// <summary>
        /// Sets a variable in the variable service of the task context.
        /// </summary>
        /// <remarks>
        /// The variable is exposed to following tasks as an environment variable.
        /// </remarks>
        /// <param name="name">The variable name.</param>
        /// <param name="value">The variable value.</param>
        public string SetVariable(string name, string value) {
            return WriteLoggingCommand(
                "task.setvariable",
                new Dictionary<string, string> {
                    ["variable"] = name
                },
                value);
        }

        /// <summary>
        /// Create an artifact link, such as a file or folder path or a version control path.
        /// </summary>
        /// <param name="name">The artifact name..</param>
        /// <param name="type">The artifact type.</param>
        /// <param name="location">The link path or value.</param>
        public string LinkArtifact(string name, VsoBuildArtifactType type, string location) {
            return WriteLoggingCommand(
                "artifact.associate",
                new Dictionary<string, string> {
                    ["artifactname"] = name,
                    ["type"] = type.ToString()
                },
                location);
        }

        /// <summary>
        /// Update build number for current build.
        /// </summary>
        /// <remarks>
        /// Requires agent version 1.88.
        /// </remarks>
        /// <param name="buildNumber">The build number.</param>
        public string UpdateBuildNumber(string buildNumber) {
            return WriteLoggingCommand("build.updatebuildnumber", buildNumber);
        }

        /// <summary>
        /// Add a tag for current build.
        /// </summary>
        /// <remarks>
        /// Requires agent version 1.95
        /// </remarks>
        /// <param name="tag">The tag.</param>
        public string AddBuildTag(string tag) {
            return WriteLoggingCommand("build.addbuildtag", tag);
        }

        private string WriteLoggingCommand(string actionName, string value) {
            return WriteLoggingCommand(actionName, new Dictionary<string, string>(), value);
        }

        private string WriteLoggingCommand(string actionName, Dictionary<string, string> properties, string value) {
            var props = string.Join(string.Empty, properties.Select(pair => { return string.Format(CultureInfo.InvariantCulture, "{0}={1};", pair.Key, pair.Value); }));

            var commandText = string.Format("{0}{1} {2}{3}{4}", MessagePrefix, actionName, props, MessagePostfix, value);

            if (logger != null) {
                logger(commandText);
            }

            return commandText;
        }
    }
}
