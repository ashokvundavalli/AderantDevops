using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;

namespace Aderant.DeveloperTools.XamlAdorner {
    public class ImageCompletionSource : ICompletionSource {

        private ITextBuffer buffer;
        private ITextStructureNavigator textStructureNavigator;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageCompletionSource"/> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="textStructureNavigator">The text structure navigator.</param>
        public ImageCompletionSource(ITextBuffer buffer, ITextStructureNavigator textStructureNavigator) {
            this.buffer = buffer;
            this.textStructureNavigator = textStructureNavigator;
        }


        private static Assembly assembly;
        private static DateTime lastAssemblyWriteTime;
        private string assemblyName = "Aderant.PresentationFramework.Images";
        private string resourceDictionaryName = "ExpertImages";

        /// <summary>
        /// Determines which <see cref="T:Microsoft.VisualStudio.Language.Intellisense.CompletionSet" />s should be part of the specified <see cref="T:Microsoft.VisualStudio.Language.Intellisense.ICompletionSession" />.
        /// </summary>
        /// <param name="session">The session for which completions are to be computed.</param>
        /// <param name="completionSets">The set of <see cref="T:Microsoft.VisualStudio.Language.Intellisense.CompletionSet" /> objects to be added to the session.</param>
        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            try {
                if (!this.isDisposed) {
                    ITextSnapshot currentSnapshot = session.TextView.TextBuffer.CurrentSnapshot;
                    SnapshotPoint? triggerPoint = session.GetTriggerPoint(currentSnapshot);

                    if ((triggerPoint.HasValue && triggerPoint.HasValue) && (triggerPoint.Value.Position != 0)) {
                        ITrackingSpan applicableTo = FindTokenSpanAtPosition(session);

                        if (applicableTo != null) {

                            var startPoint = applicableTo.GetStartPoint(currentSnapshot);
                            var textViewLine = session.TextView.TextViewLines.Single(l => l.Start.Position <= startPoint.Position && l.End.Position >= startPoint.Position);
                            var textStartPosition = textViewLine.Start.Position;
                            var textLength = textViewLine.Length;
                            var text = textViewLine.Snapshot.GetText(new Span(textStartPosition, textLength));
                            var previousText = text.Substring(0, startPoint - textStartPosition);
                            var lastColonIndex = previousText.LastIndexOf(":", StringComparison.InvariantCulture);
                            if (lastColonIndex < 0) {
                                return;
                            }
                            var resourceClass = previousText.Substring(lastColonIndex + 1);
                            if (!resourceClass.StartsWith("ExpertImages")) {
                                return;
                            }

                            string expertDevBranchFolder = Environment.GetEnvironmentVariable("ExpertDevBranchFolder");
                            var assemblyPath = Path.Combine(expertDevBranchFolder, @"Binaries\ExpertSource", string.Concat(assemblyName, ".dll"));
                            var lastWriteTime = File.GetLastWriteTime(assemblyPath);
                            bool updateAssembly = false;
                            if (lastAssemblyWriteTime != lastWriteTime) {
                                updateAssembly = true;
                                lastAssemblyWriteTime = lastWriteTime;
                            }
                            if (assembly == null || updateAssembly) {
                                var copiedAssemblyPath = Path.Combine(Path.GetTempPath(), assemblyName, Path.GetRandomFileName(), Path.GetFileName(assemblyPath));
                                Directory.CreateDirectory(Path.GetDirectoryName(copiedAssemblyPath));
                                File.Copy(assemblyPath, copiedAssemblyPath, true);
                                assembly = Assembly.LoadFrom(copiedAssemblyPath);
                            }
                            var expertResourcesType = assembly.GetTypes().Single(t => t.Name.EndsWith(this.resourceDictionaryName));

                            var list = new List<Completion>();
                            foreach (var c in completionSets[0].Completions) {

                                var resourceString = c.DisplayText;
                                var resourceParts = resourceClass.Split('+');
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


                                var image = resource as DrawingImage;
                                if (image == null) {
                                    break;
                                }
                                list.Add(new Completion(c.DisplayText, string.Concat(".", c.InsertionText), c.Description, image, c.IconAutomationText));
                            }

                            completionSets.Insert(0, new CompletionSet("Images", "Images", applicableTo, list, Enumerable.Empty<Completion>()));
                        }
                    }
                }
            } catch (Exception ex) {
            }
        }

        private ITrackingSpan FindTokenSpanAtPosition(ICompletionSession session) {
            SnapshotPoint currentPosition = session.TextView.Caret.Position.BufferPosition - 1;
            TextExtent extentOfWord = this.textStructureNavigator.GetExtentOfWord(currentPosition);
            return currentPosition.Snapshot.CreateTrackingSpan((Span)extentOfWord.Span, SpanTrackingMode.EdgeInclusive);
        }


        public void Dispose() {
            if (!isDisposed) {
                GC.SuppressFinalize(this);
                isDisposed = true;
            }
        }
    }
}
