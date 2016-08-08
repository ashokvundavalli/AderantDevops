using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aderant.Build.Logging;
using Aderant.Build.Versioning;

namespace Aderant.Build.Packaging {

    public class CreatePaketTemplatesAction {
        private ILogger logger;

        /// <summary>
        /// Executes this action.
        /// </summary>
        /// <param name="loggerInstance">The logger instance.</param>
        /// <param name="buildScriptsDirectory">The build scripts directory.</param>
        /// <param name="buildToolsDirectory">The build tools directory.</param>
        /// <param name="rootFolder">The root folder.</param>
        /// <param name="executeInParallel">if set to <c>true</c> this action is executed in parallel.</param>
        /// <returns>
        /// true, if executed successfully, otherwise false
        /// </returns>
        public bool Execute(ILogger loggerInstance, string buildScriptsDirectory, string buildToolsDirectory, string rootFolder, bool executeInParallel = false) {
            this.logger = loggerInstance;
            var sw = new Stopwatch();
            sw.Start();

            try {
                this.logger.Info("Creating paket templates for third party modules.");

                // version analyzer setup
                var analyzer = new FileVersionAnalyzer(buildToolsDirectory);
                analyzer.OpenFile = File.OpenRead;

                var tasks = new List<Task>();

                // inspect third party folder
                foreach (var moduleFolder in Directory.EnumerateDirectories(rootFolder).Where(d => !d.EndsWith("Build.Infrastructure", StringComparison.InvariantCultureIgnoreCase))) {

                    var moduleName = moduleFolder.Split('\\').Last();

                    var info = moduleName + ":";
                    this.logger.Info(string.Empty);
                    this.logger.Info(info);

                    var moduleVersion = new Version(1, 0, 0);

                    // check, if the version number has been set manually via postfix of the third party module folder
                    var match = Regex.Match(moduleName, @"(.+)(\.\d+\.\d+\.\d+)");
                    if (match.Success) {
                        moduleName = match.Groups[1].Value;
                        Version.TryParse(match.Groups[2].Value.Substring(1), out moduleVersion);
                    } else {
                        var binFolder = Path.Combine(moduleFolder, "bin");

                        // get highest version number of any file in the bin folder of each module
                        foreach (var file in Directory.EnumerateFiles(binFolder)) {
                            var version = analyzer.GetVersion(file);
                            if (version != null) {

                                var fileVersion = new Version();
                                if (version.FileVersion != null) {
                                    Version.TryParse(version.FileVersion, out fileVersion);
                                } else if (version.AssemblyVersion != null) {
                                    fileVersion = version.AssemblyVersion;
                                }

                                if (fileVersion != null && fileVersion > moduleVersion) {
                                    moduleVersion = fileVersion;
                                }

                                info = " * " + Path.GetFileName(file) + " - " + version.FileVersion + " (" + version.AssemblyVersion + ")";
                                this.logger.Info(info);
                            }
                        }
                    }

                    // strip version to 3 relevant numbers
                    var major = Math.Max(0, moduleVersion.Major);
                    var minor = Math.Max(0, moduleVersion.Minor);
                    var build = Math.Max(0, moduleVersion.Build);
                    moduleVersion = new Version(major, minor, build, 0);
                    if (moduleVersion.Build > 999) {
                        moduleVersion = new Version(major, minor, 0, 0);
                    }

                    var semVer = moduleVersion.ToString(3);

                    info = " ==> " + semVer;
                    this.logger.Info(info);

                    // create paket.template file and empty paket.dependencies and paket.lock files because they are required for the pack command
                    var paketTemplateFile = Path.Combine(moduleFolder, "paket.template");
                    var paketDependenciesFile = Path.Combine(moduleFolder, "paket.dependencies");
                    var paketLockFile = Path.Combine(moduleFolder, "paket.lock");

                    File.WriteAllText(paketDependenciesFile, string.Empty);
                    File.WriteAllText(paketLockFile, string.Empty);

                    var templateContentBuilder = new StringBuilder(string.Format(@"type file
id {0}
version {1}
authors Aderant
description
    {2}
files
", moduleName, semVer, string.Concat(moduleName, " package")));

                    var attributionFile = Path.Combine(moduleFolder, "attribution.txt");
                    var licenseFile = Path.Combine(moduleFolder, "license.txt");
                    if (File.Exists(attributionFile)) {
                        if (File.Exists(licenseFile)) {
                            File.SetAttributes(licenseFile, FileAttributes.Normal);
                            File.Delete("license.txt");
                        }
                        File.Copy(attributionFile, licenseFile, true);
                        templateContentBuilder.AppendLine("    license.txt ==> .");
                    }

                    templateContentBuilder.AppendLine("    bin ==> lib");

                    File.WriteAllText(paketTemplateFile, templateContentBuilder.ToString());

                    // create nuspec file
                    var arguments = string.Format(@"pack output {0} templatefile {1} version {2}", moduleFolder, paketTemplateFile, semVer);
                    var processFilePath = Path.Combine(buildScriptsDirectory, "paket.exe");
                    if (executeInParallel) {
                        tasks.Add(BuildInfrastructureHelper.StartProcessAsync(processFilePath, arguments, moduleFolder, OnReceiveStandardErrorOrOutputData));
                    } else {
                        BuildInfrastructureHelper.StartProcessAndWaitForExit(processFilePath, arguments, moduleFolder, OnReceiveStandardErrorOrOutputData);
                    }
                }

                Task.WaitAll(tasks.ToArray());

                sw.Stop();
                this.logger.Info(string.Empty);
                this.logger.Info("Finished packaging in {0} seconds.", (sw.ElapsedMilliseconds / 1000.0).ToString("F1"));

            } catch (Exception ex) {
                this.logger.Error("Error creating paket templates:");
                this.logger.Error(ex.ToString());
                return false;
            }
            return true;
        }

        private void OnReceiveStandardErrorOrOutputData(DataReceivedEventArgs e, bool isError, System.Diagnostics.Process process) {
            if (e.Data != null) {
                if (isError) {
                    this.logger.Error("{0}: {1}", process.Id.ToString(), e.Data);
                } else {
                    this.logger.Debug("{0}: {1}", process.Id.ToString(), e.Data);
                }
            }
        }
    }
}
