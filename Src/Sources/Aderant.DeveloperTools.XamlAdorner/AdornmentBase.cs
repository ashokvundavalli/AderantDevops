using System;
using System.Collections.Generic;
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
        
        private string assemblyPath;
        
        private static readonly string randomFileName;
        private static readonly string extensionName = "Aderant.DeveloperTools";

        static AdornmentBase() {
            randomFileName = "XamlAdorner";
        }

        protected AdornmentBase(IWpfTextView view, string[] regexPatterns, string adornmentName, string assemblyName, string[] resourceDictionaryNames) {
            regexCollection = regexPatterns.Select(pattern => new Regex(pattern)).ToArray();
            this.view = view;
            this.layer = view.GetAdornmentLayer(adornmentName);
            this.assemblyName = assemblyName;
            this.resourceDictionaryNames = resourceDictionaryNames;

            string expertDevBranchFolder = Environment.GetEnvironmentVariable("ExpertDevBranchFolder");
            assemblyPath = Path.Combine(expertDevBranchFolder, @"Binaries\ExpertSource", string.Concat(assemblyName, ".dll"));

            if (!assemblies.ContainsKey(assemblyName)) {
                assemblies.Add(assemblyName, null);
            }
            if (!lastAssemblyWriteTimes.ContainsKey(assemblyName)) {
                lastAssemblyWriteTimes.Add(assemblyName, DateTime.MinValue);
            }

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

            var dispatcher = Dispatcher.CurrentDispatcher;

            System.Threading.Tasks.Task.Run(() => {

                var lastWriteTime = File.GetLastWriteTime(assemblyPath);
                bool updateAssembly = false;
                if (lastAssemblyWriteTimes[this.assemblyName] < lastWriteTime) {
                    updateAssembly = true;
                    lastAssemblyWriteTimes[this.assemblyName] = lastWriteTime;
                }
                if (assemblies[this.assemblyName] == null || updateAssembly) {
                    assemblies[this.assemblyName] = Assembly.Load(File.ReadAllBytes(assemblyPath));
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
                            Type expertResourcesType = GetAssemblyTypes(assemblies[this.assemblyName]).Single(t => t.Name.EndsWith(this.resourceDictionaryNames[i]));
                            
                            text = text.Substring(text.IndexOf(this.resourceDictionaryNames[i], StringComparison.Ordinal));
                            var resourceString = text.Split('}', ';')[0].Split('.')[1].Trim();
                            var resourceParts = text.Split('.')[0].Split('+');
                            PropertyInfo prop = null;
                            if (resourceParts.Length == 2) {
                                var nestedType = expertResourcesType.GetNestedType(resourceParts[1]);
                                try {
                                    prop = nestedType.GetProperty(resourceString);
                                } catch {
                                    return;
                                }
                            } else if (resourceParts.Length == 3) {
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
                            MessageBox.Show(ex.ToString(), "Error in Aderant VS Extension. Please report this issue.");
                        }
                    }
                }
            }
        }

        private static IEnumerable<Type> GetAssemblyTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            } catch (ReflectionTypeLoadException exception) {
                return exception.Types.Where(x => x != null);
            }
        }

        Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.Name.Split(',')[0] == "System.Windows.Interactivity" ||
                args.Name.StartsWith("Xceed") ||
                args.Name.StartsWith("Keyoti4") ||
                args.Name.StartsWith("Mindscape") ||
                args.Name.StartsWith("Aderant") ||
                args.Name.StartsWith("DevComponents") ||
                args.Name.StartsWith("GongSolutions") ||
                args.Name.StartsWith("ICSharpCode")) {

                string expertDevBranchFolder = Environment.GetEnvironmentVariable("ExpertDevBranchFolder");
                var assemblyPath = Path.Combine(expertDevBranchFolder, @"Binaries\ExpertSource", string.Concat(args.Name.Split(',')[0], ".dll"));

                if (File.Exists(assemblyPath)) {
                    var asembly = Assembly.Load(File.ReadAllBytes(assemblyPath));

                    assemblies[asembly.GetName().Name] = asembly;

                    return asembly;
                }
            }
            return null;
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
