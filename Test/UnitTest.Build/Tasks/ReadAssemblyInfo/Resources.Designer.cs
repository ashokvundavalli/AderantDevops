﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace UnitTest.Build.Tasks.ReadAssemblyInfo {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
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
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("UnitTest.Build.Tasks.ReadAssemblyInfo.Resources", typeof(Resources).Assembly);
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
        ///   Looks up a localized string similar to using System.Reflection;
        ///using System.Runtime.InteropServices;
        ///
        ///[assembly: AssemblyTitle(&quot;UnitTest.Build&quot;)]
        ///[assembly: AssemblyDescription(&quot;Unit Tests for the ExpertSuite build process&quot;)]
        ///[assembly: AssemblyCompany(&quot;Aderant&quot;)]
        ///[assembly: AssemblyCopyright(&quot;Copyright © Aderant 2010-2016&quot;)]
        ///[assembly: ComVisible(false)]
        ///
        ///// The following GUID is for the ID of the typelib if this project is exposed to COM
        ///[assembly: Guid(&quot;ff5928c2-8545-483a-8468-ef1dba71d066&quot;)]
        ///
        ///[assembly: AssemblyVersion(&quot;1.0.0.0&quot; [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string AssemblyInfo {
            get {
                return ResourceManager.GetString("AssemblyInfo", resourceCulture);
            }
        }
    }
}