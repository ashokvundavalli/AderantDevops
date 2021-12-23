﻿using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Utilities;

namespace Aderant.Build.MSBuild {
    public class TargetXmlEmitter : BuildElementVisitor {
        /// <summary>
        /// The MSBuild Project namespace
        /// </summary>
        public static readonly XNamespace Xmlns = "http://schemas.microsoft.com/developer/msbuild/2003";

        private XElement document;

        private Stack<XElement> elementStack = new Stack<XElement>();
        private List<string> visited = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetXmlEmitter" /> class.
        /// </summary>
        public TargetXmlEmitter() {
            document = CreateRoot(null);
        }

        /// <summary>
        /// Gets the tool version the emitter is using such as 14.0, 15.0, Current etc
        /// </summary>
        public string ToolsVersion {
            get { return ToolLocationHelper.CurrentToolsVersion; }
        }

        private static XElement CreateRoot(Project project) {
            return new XElement(
                Xmlns + "Project",
                new XAttribute("ToolsVersion", ToolLocationHelper.CurrentToolsVersion),
                new XAttribute("DefaultTargets", project != null && project.DefaultTarget != null ? project.DefaultTarget.Name : string.Empty),
                new XComment("Properties is a special element understood by the MS Build task and will associate the unique properties to each project"));
        }

        public override void Visit(Project project) {
            document = CreateRoot(project);

            if (project != null) {
                foreach (MSBuildProjectElement element in project.Elements) {
                    element.Accept(this);
                }
            }
        }

        public override void Visit(ItemGroup itemGroup) {
            XElement element = new XElement(Xmlns + "ItemGroup");

            if (itemGroup.Condition != null) {
                element.Add(new XAttribute("Condition", itemGroup.Condition));
            }

            foreach (var item in itemGroup.Include) {
                var itemElement = new XElement(Xmlns + itemGroup.Name);

                if (item.Condition != null) {
                    itemElement.Add(new XAttribute("Condition", item.Condition));
                }

                itemElement.Add(new XAttribute("Include", item.Expression));

                var keys = item.MetadataKeys;

                foreach (var key in keys) {
                    string value;
                    if (item.TryGetValue(key, out value)) {
                        if (!string.IsNullOrEmpty(value)) {
                            itemElement.Add(new XElement(Xmlns + key, value));
                        }
                    }
                }

                element.Add(itemElement);
            }

            Add(element);
        }


        public override void Visit(ImportElement element) {
            Add(new XElement(Xmlns + "Import", new XAttribute("Project", element.Project)));
        }

        public override void Visit(Comment comment) {
            Add(new XComment(comment.Text));
        }

        public override void Visit(PropertyGroup propertyGroup) {
            XElement element = new XElement(Xmlns + "PropertyGroup");

            foreach (KeyValuePair<string, string> item in propertyGroup.Properties) {
                element.Add(new XElement(Xmlns + item.Key, item.Value));
            }

            Add(element);
        }

        public override void Visit(Message message) {
            Add(new XElement(Xmlns + "Message", new XAttribute("Text", message.Text)));
        }

        public override void Visit(MSBuildTask msBuildTask) {
            XElement element = new XElement(
                Xmlns + "MSBuild",
                new XAttribute("Projects", msBuildTask.Projects ?? string.Empty),
                new XAttribute("BuildInParallel", msBuildTask.BuildInParallel));

            if (!string.IsNullOrEmpty(msBuildTask.ProjectToolsVersion)) {
                element.Add(new XAttribute("ToolsVersion", msBuildTask.ProjectToolsVersion));
            }

            if (!string.IsNullOrEmpty(msBuildTask.Targets)) {
                element.Add(new XAttribute("Targets", msBuildTask.Targets));
            }

            if (!string.IsNullOrEmpty(msBuildTask.Properties)) {
                element.Add(new XAttribute(new XAttribute("Properties", msBuildTask.Properties)));
            }

            Add(element);
        }

        public override void Visit(Target target) {
            if (visited.Contains(target.Name)) {
                return;
            }

            foreach (Target dependent in target.DependsOnTargets) {
                dependent.Accept(this);
                visited.Add(target.Name);
            }

            foreach (Target dependent in target.BeforeTargets) {
                dependent.Accept(this);
                visited.Add(target.Name);
            }

            foreach (Target dependent in target.AfterTargets) {
                dependent.Accept(this);
                visited.Add(target.Name);
            }

            // If we enter here already with a item on the stack we must add the item to the root, not as a child of the current item otherwise
            // we will have nested targets which is not allowed.
            XElement currentTarget = null;
            if (elementStack.Count == 1) {
                currentTarget = elementStack.Pop();
            }

            elementStack.Push(
                new XElement(
                    Xmlns + "Target",
                    new XAttribute("Name", target.Name),
                    target.Condition != null ? new XAttribute("Condition", target.Condition) : null,
                    target.Inputs != null ? new XAttribute("Inputs", target.Inputs) : null,
                    target.Outputs != null ? new XAttribute("Outputs", target.Outputs) : null,
                    target.Returns != null ? new XAttribute("Returns", string.Join(PropertyList.Separator, target.Returns.Select(name => $"@({name})"))) : null,
                    target.DependsOnTargets.Count > 0 ? new XAttribute("DependsOnTargets", PropertyList.CreatePropertyListString(target.DependsOnTargets.Select(d => d.Name))) : null,
                    target.BeforeTargets.Count > 0 ? new XAttribute("BeforeTargets", PropertyList.CreatePropertyListString(target.BeforeTargets.Select(d => d.Name))) : null,
                    target.AfterTargets.Count > 0 ? new XAttribute("AfterTargets", PropertyList.CreatePropertyListString(target.AfterTargets.Select(d => d.Name))) : null));

            foreach (MSBuildProjectElement childElement in target.Elements) {
                childElement.Accept(this);
            }

            Add(elementStack.Pop());

            if (currentTarget != null) {
                elementStack.Push(currentTarget);
            }

            visited.Add(target.Name);
        }

        public override void Visit(CallTarget callTarget) {
            Add(new XElement(Xmlns + "CallTarget", new XAttribute("Targets", string.Join(PropertyList.Separator, callTarget.Targets))));
        }

        public override void Visit(ExecElement element) {
            Add(new XElement(Xmlns + "Exec", new XAttribute("Command", element.Command)));
        }

        /// <summary>
        /// Gets the MSBuild project document.
        /// </summary>
        /// <returns></returns>
        public virtual XElement GetXml() {
            return document;
        }

        private void Add(XNode node) {
            if (elementStack.Count == 0) {
                document.Add(node);
                return;
            }

            XElement parent = elementStack.Peek();
            if (parent != null) {
                parent.Add(node);
            }
        }
    }

}
