﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.BuildEngine;

namespace DependencyAnalyzer.MSBuild {
    public class BuildElementVisitor {
        /// <summary>
        /// The MSBuild Project namespace
        /// </summary>
        public static readonly XNamespace Xmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

        private Stack<XElement> elementStack = new Stack<XElement>();
        private List<string> visited = new List<string>();
        private XElement document;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuildElementVisitor"/> class.
        /// </summary>
        public BuildElementVisitor() {
            document = CreateRoot(null);
        }

        private XElement CreateRoot(Project project) {
            return new XElement(Xmlns + "Project",
                                new XAttribute("ToolsVersion", "4.0"),
                                new XAttribute("DefaultTargets", project != null && project.DefaultTarget != null ? project.DefaultTarget.Name : string.Empty),
                                new XElement(Xmlns + "Import", new XAttribute("Project", @"$(MSBuildExtensionsPath)\Microsoft\VisualStudio\TeamBuild\Microsoft.TeamFoundation.Build.targets")));
        }

        public void Visit(Project project) {
            document = CreateRoot(project);

            if (project != null) {
                foreach (Element element in project.Elements) {
                    element.Accept(this);
                }
            }
        }

        public virtual void Visit(ItemGroup itemGroup) {
            XElement element = new XElement(Xmlns + "ItemGroup");

            foreach (string item in itemGroup.Include) {
                element.Add(new XElement(Xmlns + itemGroup.Name, new XAttribute("Include", item)));
            }

            Add(element);
        }

        public virtual void Visit(PropertyGroup propertyGroup) {
            XElement element = new XElement(Xmlns + "PropertyGroup");

            foreach (KeyValuePair<string, string> item in propertyGroup.Properties) {
                element.Add(new XElement(Xmlns + item.Key, item.Value));
            }

            Add(element);
        }

        public virtual void Visit(Message message) {
            Add(new XElement(Xmlns + "Message", new XAttribute("Text", message.Text)));
        }

        public virtual void Visit(MSBuildTask msBuildTask) {
            XElement element = new XElement(Xmlns + "MSBuild",
                                            new XAttribute("Projects", msBuildTask.Projects),
                                            new XAttribute("BuildInParallel", msBuildTask.BuildInParallel));

            if (!string.IsNullOrEmpty(msBuildTask.Properties)) {
                element.Add(new XAttribute(new XAttribute("Properties", msBuildTask.Properties)));
            }
            Add(element);
        }

        public virtual void Visit(BuildStep buildStep) {
            if (buildStep.OutputBuildStepId) {
                Add(new XElement(Xmlns + "BuildStep",
                                 new XAttribute("Condition", "('$(IsDesktopBuild)'!='true')"),
                                 new XAttribute("Message", buildStep.Message),
                                 new XAttribute("TeamFoundationServerUrl", "$(TeamFoundationServerUrl)"),
                                 new XAttribute("BuildUri", "$(BuildURI)"),
                                 new XElement(Xmlns + "Output", new XAttribute("TaskParameter", "id"), new XAttribute("PropertyName", "BuildStepId"))));
            }

            if (buildStep.IsSucceededStep) {
                Add(new XElement(Xmlns + "BuildStep",
                                 new XAttribute("Condition", "('$(IsDesktopBuild)'!='true')"),
                                 new XAttribute("TeamFoundationServerUrl", "$(TeamFoundationServerUrl)"),
                                 new XAttribute("BuildUri", "$(BuildURI)"),
                                 new XAttribute("Id", "$(BuildStepId)"),
                                 new XAttribute("Status", "Succeeded")));
            }
        }

        public virtual void Visit(Target target) {
            if (visited.Contains(target.Name)) {
                return;
            }

            foreach (Target dependent in target.DependsOnTargets) {
                Visit(dependent);
                visited.Add(target.Name);
            }

            foreach (Target dependent in target.BeforeTargets) {
                Visit(dependent);
                visited.Add(target.Name);
            }

            foreach (Target dependent in target.AfterTargets) {
                Visit(dependent);
                visited.Add(target.Name);
            }

            // If we enter here already with a item on the stack we must add the item to the root, not as a child of the current item otherwise 
            // we will have nested targets which is not allowed.
            XElement currentTarget = null;
            if (elementStack.Count == 1) {
                currentTarget = elementStack.Pop();
            }

            elementStack.Push(new XElement(Xmlns + "Target",
                                           new XAttribute("Name", target.Name),
                                           target.DependsOnTargets.Count > 0 ? new XAttribute("DependsOnTargets", string.Join(";", target.DependsOnTargets.Select(d => d.Name).ToArray())) : null,
                                           target.BeforeTargets.Count > 0 ? new XAttribute("BeforeTargets", string.Join(";", target.BeforeTargets.Select(d => d.Name).ToArray())) : null,
                                           target.AfterTargets.Count > 0 ? new XAttribute("AfterTargets", string.Join(";", target.AfterTargets.Select(d => d.Name).ToArray())) : null));

            foreach (Element childElement in target.Elements) {
                Type type = childElement.GetType();

                MethodInfo method = GetType().GetMethods().FirstOrDefault(m => m.GetParameters().First().ParameterType == type);
                if (method == null) {
                    throw new InvalidOperationException("No implemenation for type: " + type);
                }

                method.Invoke(this, new object[] {childElement});
            }

            Add(elementStack.Pop());

            if (currentTarget != null) {
                elementStack.Push(currentTarget);
            }

            visited.Add(target.Name);
        }

        /// <summary>
        /// Gets the MSBuild project document.
        /// </summary>
        /// <returns></returns>
        public XElement GetDocument() {
            var p = new Microsoft.Build.BuildEngine.Project();
            p.Load(new StringReader(document.ToString()));

            foreach (BuildItemGroup itemGroup in p.ItemGroups.OfType<BuildItemGroup>()) {
                foreach (BuildItem buildItem in itemGroup) {
                    string finalItemSpec = buildItem.FinalItemSpec;

                    if (finalItemSpec.EndsWith("TFSBuild.proj")) {
                        DirectoryInfo buildDirectory = Directory.GetParent(finalItemSpec);

                        string responseFile = Path.Combine(buildDirectory.FullName, "TFSBuild.rsp");
                        if (File.Exists(responseFile)) {
                            string[] properties = File.ReadAllLines(responseFile);

                            string singlePropertyLine = CreateSinglePropertyLine(properties);
                            buildItem.SetMetadata("Properties", singlePropertyLine);
                        }
                    }
                }
            }

            using (StringWriter writer = new StringWriter()) {
                
                p.Save(writer);

                return XElement.Parse(writer.ToString());
            }
        }

        private void Add(XElement element) {
            if (elementStack.Count == 0) {
                document.Add(element);
                return;
            }

            XElement parent = elementStack.Peek();
            if (parent != null) {
                parent.Add(element);
            }
        }

        private string CreateSinglePropertyLine(string[] properties) {
            IList<string> lines = new List<string>();

            foreach (string property in properties) {
                if (property.StartsWith("/p:")) {
                    string line = property.Substring(property.IndexOf("/p:") + 3).Replace("\"", "").Trim();

                    if (!line.StartsWith("BuildInParallel")) {
                        lines.Add(line);
                    }
                }
            }

            return string.Join(";", lines.ToArray());
        }
    }
}