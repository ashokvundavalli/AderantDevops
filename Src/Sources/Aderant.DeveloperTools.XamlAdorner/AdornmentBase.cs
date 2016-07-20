using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Aderant.DeveloperTools.XamlAdorner {

    [Serializable]
    public abstract class AdornmentBase {

        protected IWpfTextView view;
        protected IAdornmentLayer layer;
        private readonly Regex[] regexCollection;
        private static Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();
        private static Dictionary<string, DateTime> lastAssemblyWriteTimes = new Dictionary<string, DateTime>();
        private readonly string assemblyName;
        private readonly string[] resourceDictionaryNames;
        private string tempPath;
        private string assemblyPath;
        private string copiedAssemblyPath;
        private static readonly string randomFileName;
        private static readonly string extensionName = "Aderant.DeveloperTools";

        static AdornmentBase() {
            randomFileName = "XamlAdorner"; //Path.GetRandomFileName();
        }

        protected AdornmentBase(IWpfTextView view, string[] regexPatterns, string adornmentName, string assemblyName, string[] resourceDictionaryNames) {
            regexCollection = regexPatterns.Select(pattern => new Regex(pattern)).ToArray();
            this.view = view;
            this.layer = view.GetAdornmentLayer(adornmentName);
            this.assemblyName = assemblyName;
            this.resourceDictionaryNames = resourceDictionaryNames;

            string expertDevBranchFolder = Environment.GetEnvironmentVariable("ExpertDevBranchFolder");
            assemblyPath = Path.Combine(expertDevBranchFolder, @"Binaries\ExpertSource", string.Concat(assemblyName, ".dll"));
            copiedAssemblyPath = Path.Combine(Path.GetTempPath(), extensionName, randomFileName, Path.GetFileName(assemblyPath));

            tempPath = Path.Combine(Path.GetTempPath(), extensionName);
            if (Directory.Exists(tempPath)) {
                foreach (var subDirectory in Directory.GetDirectories(tempPath).Where(d => Directory.GetCreationTime(d) < DateTime.Now - TimeSpan.FromDays(7))) {
                    try {
                        Directory.Delete(subDirectory, recursive: true);
                    } catch { }
                }
            }

            if (!assemblies.ContainsKey(assemblyName)) {
                assemblies.Add(assemblyName, null);
            }
            if (!lastAssemblyWriteTimes.ContainsKey(assemblyName)) {
                lastAssemblyWriteTimes.Add(assemblyName, DateTime.MinValue);
            }

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            var dispatcher = Dispatcher.CurrentDispatcher;

            System.Threading.Tasks.Task.Factory.StartNew(() => {

                var lastWriteTime = File.GetLastWriteTime(assemblyPath);
                bool updateAssembly = false;
                if (lastAssemblyWriteTimes[this.assemblyName] < lastWriteTime)
                {
                    updateAssembly = true;
                    lastAssemblyWriteTimes[this.assemblyName] = lastWriteTime;
                }
                if (assemblies[this.assemblyName] == null || updateAssembly)
                {
                    CreateNewAssemblyAndCopyToTempFolder(assemblyPath, copiedAssemblyPath);
                    assemblies[this.assemblyName] = Assembly.LoadFrom(copiedAssemblyPath);
                }

                dispatcher.Invoke(() => {

                    OnLayoutChanged(this, new TextViewLayoutChangedEventArgs(new ViewState(view), new ViewState(view), view.TextViewLines.ToList(), new List<ITextViewLine>()));

                    //Listen to any event that changes the layout (text changes, scrolling, etc)
                    this.view.LayoutChanged += OnLayoutChanged;
                });
            });
        }

        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {

            foreach (ITextViewLine line in e.NewOrReformattedLines) {
                CreateVisuals(line);
            }
        }

        private void CopyResource(string resourceName, string destinationFolder) {
            var file = Path.Combine(destinationFolder, resourceName);
            if (!File.Exists(file)) {
                using (Stream resource = GetType().Assembly.GetManifestResourceStream("Aderant.DeveloperTools.XamlAdorner.Assets." + resourceName)) {
                    if (resource == null) {
                        throw new ArgumentException("No such resource", "resourceName");
                    }
                    using (Stream output = File.OpenWrite(file)) {
                        resource.CopyTo(output);
                    }
                }
            }
        }

        private void CreateVisuals(ITextViewLine line) {
            IWpfTextViewLineCollection textViewLines = this.view.TextViewLines;
            string text = null;
            if (TryGetText(this.view, line, out text)) {
                for (int i = 0; i < regexCollection.Length; i++) { 
                    var match = regexCollection[i].Match(text);
                    if (match.Success) {
                        int matchStart = line.Start.Position + match.Index;
                        var span = new SnapshotSpan(this.view.TextSnapshot, Span.FromBounds(matchStart, matchStart + match.Length - 1));
                        try {
                            var expertResourcesType = assemblies[this.assemblyName].GetTypes().Single(t => t.Name.EndsWith(this.resourceDictionaryNames[i]));
                            text = text.Substring(text.IndexOf(this.resourceDictionaryNames[i], StringComparison.Ordinal));
                            var resourceString = text.Split('}', ';')[0].Split('.')[1].Trim();
                            var resourceParts = text.Split('.')[0].Split('+');
                            PropertyInfo prop = null;
                            if (resourceParts.Count() == 2) {
                                var nestedType = expertResourcesType.GetNestedType(resourceParts[1]);
                                try {
                                    prop = nestedType.GetProperty(resourceString);
                                } catch {
                                    return;
                                }
                            } else if (resourceParts.Count() == 3) {
                                var nestedType = expertResourcesType.GetNestedType(resourceParts[1]).GetNestedType(resourceParts[2]);
                                try {
                                    prop = nestedType.GetProperty(resourceString);
                                } catch {
                                    return;
                                }
                            } else {
                                try {
                                    prop = expertResourcesType.GetProperty(resourceString);
                                } catch {
                                    return;
                                }
                            }
                            if (prop == null) {
                                return;
                            }
                            var resource = prop.GetValue(null);

                            AdornmentAction(resource, textViewLines, span);

                        } catch (Exception ex) {
                            // stop watching
                            this.view.LayoutChanged -= OnLayoutChanged;
                            MessageBox.Show(ex.ToString(), "Error in Aderant VS Extension");
                        }
                    }
                }
            }
        }

        Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.Name.Split(',')[0] == "System.Windows.Interactivity" ||
                args.Name.StartsWith("Xceed") ||
                args.Name.StartsWith("Keyoti4") ||
                args.Name.StartsWith("Mindscape") ||
                args.Name.StartsWith("Aderant")) {

                string expertDevBranchFolder = Environment.GetEnvironmentVariable("ExpertDevBranchFolder");
                var assemblyPath = Path.Combine(expertDevBranchFolder, @"Binaries\ExpertSource", string.Concat(args.Name.Split(',')[0], ".dll"));

                var copiedFilePath = Path.Combine(Path.GetDirectoryName(copiedAssemblyPath), Path.GetFileName(assemblyPath));

                if (!File.Exists(copiedFilePath)) {
                    File.Copy(assemblyPath, copiedFilePath);
                    return Assembly.LoadFrom(copiedFilePath);
                }
                //CreateNewAssemblyAndCopyToTempFolder(assemblyPath, copiedFilePath);

                //return Assembly.LoadFrom(copiedFilePath);
            }
            return null;
        }

        private void CreateNewAssemblyAndCopyToTempFolder(string assemblyPath, string copiedAssemblyPath) {
            Directory.CreateDirectory(Path.GetDirectoryName(copiedAssemblyPath));
            var expertBinariesPath = Path.GetDirectoryName(assemblyPath);
            var ilMergedAssemblyPath = Path.Combine(expertBinariesPath, "ILMerge", Path.GetFileName(assemblyPath));
            //var ilMergedAssemblyPath = assemblyPath.Replace(".dll", ".2.dll");

            //if (!File.Exists(ilMergedAssemblyPath)) {
            CopyResource("FrameworkKey.snk", expertBinariesPath);
            CopyResource("ILMerge.exe", expertBinariesPath);
            CopyResource("ILMerge.exe.config", expertBinariesPath);
            Directory.CreateDirectory(Path.Combine(expertBinariesPath, "ILMerge"));
            string ilMergeCommand = string.Format("/C .\\ILMerge.exe {0} /out:ILMerge\\{1} /keyfile:FrameworkKey.snk /targetplatform:v4", Path.GetFileName(assemblyPath), Path.GetFileName(ilMergedAssemblyPath));

            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = ilMergeCommand;
            startInfo.WorkingDirectory = expertBinariesPath + "\\";
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            //}
            try {
                File.Copy(Path.Combine(expertBinariesPath, "ILMerge", Path.GetFileName(assemblyPath)), copiedAssemblyPath, true);
            } catch { }
        }

        protected abstract void AdornmentAction(object resource, IWpfTextViewLineCollection textViewLines, SnapshotSpan span);

        /// <summary>
        /// This will get the text of the ITextView line as it appears in the actual user editable 
        /// document. 
        /// jared parson: https://gist.github.com/4320643
        /// </summary>
        public static bool TryGetText(IWpfTextView textView, ITextViewLine textViewLine, out string text) {
            var extent = textViewLine.Extent;
            var bufferGraph = textView.BufferGraph;
            try {
                var collection = bufferGraph.MapDownToSnapshot(extent, SpanTrackingMode.EdgeInclusive, textView.TextSnapshot);
                var span = new SnapshotSpan(collection[0].Start, collection[collection.Count - 1].End);
                //text = span.ToString();
                text = span.GetText();
                return true;
            } catch {
                text = null;
                return false;
            }
        }
    }
}
