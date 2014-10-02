using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Aderant.Build.Tasks {

    /// <summary>
    /// Extracts product metadata from an Expert module build.
    /// </summary>
    public sealed class ProductInfo : Task {

        const string SplashScreenName = "Expert_SplashScreen*";

        /// <summary>
        /// Gets or sets module the sources path.
        /// </summary>
        /// <value>
        /// The source path.
        /// </value>
        public string SourcePath { get; set; }

        /// <summary>
        /// Returns the product metadata extracted from the sources.
        /// </summary>
        /// <value>
        /// The information.
        /// </value>
        [Output]
        public ITaskItem[] Info { get; set; }

        public override bool Execute() {
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";

            List<ITaskItem> productInfo = new List<ITaskItem>();

            string[] entries = Directory.GetFileSystemEntries(SourcePath, "*.csproj", SearchOption.AllDirectories);
            foreach (string project in entries) {
                XDocument projectFile = XDocument.Load(project);
                
                if (projectFile.Root == null) {
                    continue;
                }

               // Hopefully there is just one!
                var outputType = projectFile.Root.Descendants(ns + "OutputType").FirstOrDefault();
                if (outputType != null) {
                    string value = outputType.Value;

                    if (value.Equals("WinExe", StringComparison.OrdinalIgnoreCase)) {
                        string directory = Path.GetDirectoryName(project);
                        var productName = GetProductName(directory);

                        if (!string.IsNullOrEmpty(productName)) {
                            string splashScreen = Directory.GetFileSystemEntries(directory, SplashScreenName, SearchOption.AllDirectories).FirstOrDefault();

                            if (!string.IsNullOrEmpty(splashScreen)) {
                                ITaskItem item = new TaskItem(splashScreen);

                                productName = productName.Replace("Expert", string.Empty).Trim();

                                item.SetMetadata("ProductName", productName);

                                if (productName.EndsWith("Administration")) {
                                    item.SetMetadata("SplashScreenStyle", "Administration");
                                }

                                productInfo.Add(item);
                            }
                        }
                    }
                }
            }

            if (productInfo.Count > 0) {
                Log.LogMessage("Found product name and splash screens...");
                foreach (ITaskItem item in productInfo) {
                    Log.LogMessage("{0}: {1}", item.GetMetadata("ProductName"), Path.GetFileName(item.ItemSpec));
                }

                Info = productInfo.ToArray();
            }

            return !Log.HasLoggedErrors;
        }

        private string GetProductName(string directory) {
            var assemblyTitleExpression = new Regex(@"(AssemblyTitle|AssemblyProduct)\((.*?)\)");
            string[] assemblyInfos = Directory.GetFileSystemEntries(directory, "AssemblyInfo.cs", SearchOption.AllDirectories);

            foreach (string assemblyInfo in assemblyInfos) {
                string text = File.ReadAllText(assemblyInfo);

                GroupCollection match = assemblyTitleExpression.Match(text).Groups;
                string value = match[match.Count - 1].Value;

                if (!string.IsNullOrEmpty(value)) {
                    // Product names for the splash screen don't have Expert in the name, unless you want to run Expert ExpertTime :)
                    return value.Replace("\"", string.Empty); //.Replace("Expert", string.Empty);
                }
            }
            return null;
        }
    }
}