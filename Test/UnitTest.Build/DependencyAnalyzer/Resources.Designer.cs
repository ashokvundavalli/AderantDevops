﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace UnitTest.Build.DependencyAnalyzer {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("UnitTest.Build.DependencyAnalyzer.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Group 0
        /// └─AderantExpertLauncher.Pre
        ///Group 1
        /// ├─ExpertLauncher
        /// │  ├─Path: C:\Git\ExpertSuite\AderantExpertLauncher\Src\Aderant.ExpertLauncherProtocol\Aderant.ExpertLauncher.csproj
        /// │  └─Flags: CachedBuildNotFound
        /// └─Aderant.ExpertLauncherCO
        ///    ├─Path: C:\Git\ExpertSuite\AderantExpertLauncher\Src\AderantExpertLauncherProtocolCO\Aderant.ExpertLauncherCO.csproj
        ///    └─Flags: CachedBuildNotFound
        ///Group 2
        /// ├─ExpertLauncherMsiPerMachine
        /// │  ├─Path: C:\Git\ExpertSuite\AderantExpertLauncher\Src\ExpertLau [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string BuildTree {
            get {
                return ResourceManager.GetString("BuildTree", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;?xml version=&quot;1.0&quot; encoding=&quot;utf-8&quot;?&gt;
        ///&lt;DependencyManifest&gt;
        ///  &lt;ReferencedModules&gt;
        ///    &lt;ReferencedModule Name=&quot;Build.T4Task&quot; TreatAsDependency=&quot;true&quot; /&gt;
        ///  &lt;/ReferencedModules&gt;
        ///&lt;/DependencyManifest&gt;.
        /// </summary>
        internal static string DependencyManifest {
            get {
                return ResourceManager.GetString("DependencyManifest", resourceCulture);
            }
        }
    }
}
